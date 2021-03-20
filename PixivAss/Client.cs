using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PixivAss.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace PixivAss
{
    partial class Client: IBindHandleProvider, IDisposable
    {
        public BindHandleProvider provider { get; set; } = new BindHandleProvider();
        public delegate void Delegate_V_B();
        public string VerifyState { get=>verify_state;
            set
            {
                verify_state = value;
                this.NotifyChangeEx<string>();
            }
        }
        private float SEARCH_PAGE_SIZE = 60;//不知如何获得，暂用常数表示;为计算方便使用float
        private int UPDATE_INTERVAL = 7 * 100;//更新数据库间隔，上次更新时间距今小于该值的illust不会被更新
        private string verify_state="Waiting";
        private string download_dir_root;
        private string user_id;
        private string base_url = "https://www.pixiv.net/";
        private string base_host = "www.pixiv.net";
        private string user_name;
        private string download_dir_bookmark_pub;
        private string download_dir_bookmark_private;
        public  string download_dir_main;
        private string download_dir_ugoira_tmp;
        public string special_dir;
        private CookieServer cookie_server;
        public  Database database;
        private HttpClient httpClient;
        private HttpClient httpClient_anonymous;//不需要登陆的地方使用不带cookie的客户端，以防被网站警告
        private HashSet<string> banned_keyword;
        private Uri aria2_rpc_addr =new Uri("http://127.0.0.1:4322/jsonrpc");
        private string aria2_rpc_secret = "{1BF4EE95-7D91-4727-8934-BED4A305CFF0}";
        private string request_proxy;

        private HashSet<int> illust_fetch_queue = new HashSet<int>();//计划更新的illustid,线程不安全,只在RunSchedule里使用
        private HashSet<int> illust_download_queue = new HashSet<int>();//计划下载的illustid,线程不安全,只在RunSchedule里使用

        //public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public Client(Config config)
        {
            download_dir_root = config.DownloadDir;
            download_dir_bookmark_pub = download_dir_root+"pub";
            download_dir_bookmark_private = download_dir_root+"private";
            download_dir_main = download_dir_root+"tmp";
            download_dir_ugoira_tmp = download_dir_root + "ugoira_tmp";
            special_dir = download_dir_root +"special";
            foreach (var dir in new List<string> { download_dir_root, download_dir_bookmark_pub, download_dir_bookmark_private, download_dir_main, download_dir_ugoira_tmp, special_dir })
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

            database = new Database(config.ConnectStr);
            request_proxy = config.Proxy;
            user_id = config.UserId;
            user_name = config.UserName;
            cookie_server = new CookieServer(database,request_proxy);

            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler()
                                        {
                                            MaxConnectionsPerServer = 256,
                                            UseCookies = false,
                                            Proxy = new WebProxy(request_proxy, false)
                                        };            
            handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            httpClient = new HttpClient(handler);
            //超时必须设短一些，因为有的时候某个请求就是会得不到回应，需要让它尽快超时重来
            httpClient.Timeout = new TimeSpan(0, 0, 35);
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.100 Safari/537.36");
            httpClient.DefaultRequestHeaders.Host = base_host;
            httpClient.DefaultRequestHeaders.Add("Cookie", this.cookie_server.cookie);
//            httpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
//            httpClient.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
//            httpClient.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            httpClient.DefaultRequestHeaders.Add("x-csrf-token", this.cookie_server.csrf_token);

            httpClient_anonymous = new HttpClient(handler);
            httpClient_anonymous.Timeout = new TimeSpan(0, 0, 35);
            httpClient_anonymous.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
            httpClient_anonymous.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
            httpClient_anonymous.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/538.45 (KHTML, like Gecko) Chrome/76.0.4147.100 Safari/538.45");
            httpClient_anonymous.DefaultRequestHeaders.Host = base_host;
            httpClient_anonymous.DefaultRequestHeaders.Add("Connection", "keep-alive");

            CheckHomePage();//会修改属性引发UI更新，需要从主线程调用或使用invoke
            banned_keyword = database.GetBannedKeyword().GetAwaiter().GetResult();
            RunSchedule();
        }
        public void Dispose()
        {
            httpClient.Dispose();
        }
        public async Task<string> Test()
        {
            return "";
        }

        public async Task<List<Tuple<ExploreQueueType,int,string>>> GetExploreQueueName()
        {
            var ret = new List<Tuple<ExploreQueueType, int, string>>();
            ret.Add(new Tuple<ExploreQueueType, int, string>(ExploreQueueType.Fav, 0,""));
            ret.Add(new Tuple<ExploreQueueType, int, string>(ExploreQueueType.FavR, 0,""));
            ret.Add(new Tuple<ExploreQueueType, int, string>(ExploreQueueType.Main, 0,""));
            ret.Add(new Tuple<ExploreQueueType, int, string>(ExploreQueueType.MainR, 0,""));
            foreach(var user in await database.GetQueuedUser())
                ret.Add(new Tuple<ExploreQueueType, int, string>(ExploreQueueType.User, user.userId,user.userName));
            return ret;
        }
        public async Task<List<Illust>> GetExploreQueue(ExploreQueueType type,int userId)
        {
            var list = new List<Illust>();
            if (type == ExploreQueueType.Main || type == ExploreQueueType.MainR)
            {
                bool is_private = type == ExploreQueueType.MainR;
                var id_list = (await database.GetQueue()).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .ToList<string>()
                            .Select<string, int>(x => Int32.Parse(x))
                            .ToList<int>();
                foreach (var illust in await database.GetIllustFull(id_list))
                    if ((illust.xRestrict > 0) == is_private && !illust.readed && !illust.bookmarked)
                        list.Add(illust);
            }
            else if (type == ExploreQueueType.Fav || type == ExploreQueueType.FavR)
            {
                bool is_private = type == ExploreQueueType.FavR;
                list = await database.GetIllustFull(await database.GetBookmarkIllustId(!is_private));
            }
            else
            {
                foreach (var illust in await database.GetIllustFullSortedByUser(userId))
                    if (illust.bookmarked || !illust.readed)
                        list.Add(illust);
            }
            return list;
        }

        public async Task RunSchedule()
        {
            int last_daily_task = DateTime.Now.Day;//启动的第一天不执行dailyTask，防止反复重启时执行很多次dailytask
            int process_speed = 140;

            foreach (var id in await database.GetAllIllustId("where readed=0"))
                illust_download_queue.Add(id);
            do
            {
                if(DateTime.Now.Day!=last_daily_task)
                {
                    last_daily_task = DateTime.Now.Day;
                    await DailyTask();
                }
                //每小时处理下载和更新队列
                await ProcessIllustFetchQueue(process_speed);
                await ProcessIllustDownloadQueue(60);
                if (illust_fetch_queue.Count / process_speed > 24 * 7)//积压量大于一周时逐渐加速
                    process_speed++;
                else if (illust_fetch_queue.Count / process_speed < 24 &&process_speed>140)//积压量小于一天时逐渐减速
                    process_speed--;
                await Task.Delay(new TimeSpan(1, 0, 0));//每隔一个小时执行一次
            }
            while (true);
        }
        public async Task DailyTask()
        {
            Console.WriteLine("Start Fetch Task ");
            //第一次运行之后，FollowedUser和BookmarkIllust由本地向远程单向更新
            var illust_list_bytime = new HashSet<int>();//根据时间更新的列表
            var illust_list_bylike = new Dictionary<int,int>();//根据like数更新的列表
            bool do_week_task = DateTime.Now.DayOfWeek == System.DayOfWeek.Sunday;//每周一次

            /*获得需要更新的Illust的id*/
            if (do_week_task)
                //更新关注和入列作者的作品
                illust_list_bytime.UnionWith(await RequestAllQueuedAndFollowedUserIllust());
            //更新1/700的数据库，每两年更新一轮
            illust_list_bytime.UnionWith(await database.GetIllustIdByUpdateTime(DateTime.UtcNow.AddDays(-UPDATE_INTERVAL), 1.0f / UPDATE_INTERVAL));
            //关键字搜索。分散成若干块进行
            illust_list_bylike.Union(await RequestAllKeywordSearchIllustBlock((DateTime.Now-new DateTime(2000,1,1)).Days));
            //排行榜
            illust_list_bytime.UnionWith(await RequestAllCurrentRankIllust());
            Console.WriteLine("Got {0}+{1} illusts:", illust_list_bytime.Count, illust_list_bylike.Count);

            /*获取Illust信息*/
            await AddToIllustFetchQueue(illust_list_bytime, illust_list_bylike);

            /*其它*/
            if (do_week_task)//需要在FetchIllust之后
            {
                await GenerateExplorerQueue();
                await FetchAllUnfollowedUserStatus();
                await SyncBookmarkDirectory();
            }

            /*下载*/
            await DownloadIllustsInExplorerQueue();
            Console.WriteLine("All Fetch Task Done");
        }
        //初次执行，将收藏的作者和图同步到本地
        public async Task InitTask()
        {
            var illust_list = new HashSet<int>();
            await FetchAllFollowedUser();
            await FetchAllBookMarkIllust(true);
            await FetchAllBookMarkIllust(false);
            illust_list.UnionWith(await RequestAllQueuedAndFollowedUserIllust());
            await AddToIllustFetchQueue(illust_list,new Dictionary<int, int>());
            await FetchAllUnfollowedUserStatus();
            await DownloadIllustsInExplorerQueue();
            Console.WriteLine("Fetch Task Done");
        }

        private async Task GenerateExplorerQueue(bool force=false)
        {
            const int UpdateInterval = 7 * 24 * 60 * 60;//每超过这个时间才刷新
            const int MaxSize = 10000;
            if (force||(await database.GetQueueUpdateInterval())>UpdateInterval||(await database.GetQueue()).Length<2)
            {
                var illust_list = (await database.GetAllUnreadedIllustFull()).FindAll((Illust illust) =>
                                     {
                                         foreach (var tag in illust.tags)
                                             if (this.banned_keyword.Contains(tag.ToString()))
                                                 return false;
                                         return true;
                                    });
                var followed_user = new HashSet<int>();
                foreach (var user in await database.GetFollowedUser())
                    followed_user.Add(user.userId);
                foreach (var illust in illust_list)
                {
                    illust.score = 0;
                    if (followed_user.Contains(illust.userId))
                        illust.score += 1000000;
                    illust.score += illust.bookmarkCount / 100 + illust.likeCount / 1000;
                    if (illust.xRestrict > 0)//R18倒序排序
                        illust.score = -illust.score;
                }
                illust_list.Sort((l, r) => r.score.CompareTo(l.score));
                if (illust_list.Count > MaxSize*2)//取头尾各固定长，如果R18部分数量不足会取到评分低的非R图，但是无所谓
                    illust_list=illust_list.Take<Illust>(MaxSize).Concat(illust_list.Reverse<Illust>().Take<Illust>(MaxSize)).ToList<Illust>();
                string queue = "";
                foreach (var illust in illust_list)
                    queue += " "+illust.id;
                await database.UpdateQueue(queue);
                Console.WriteLine("Generate Queue Done");
            }            
        }

        /*
         * Request:从远端查询并返回结果
         * Fetch:从远端查询并存储到数据库
         * Push:将本地结果推送到远端
         */
        //同步图片到本地，要求Illust信息已更新，返回已同步的id
        //limit>=0时，在下载limit个illust后返回
        //IDM下载更快，但是各行为耗时且互相阻塞和报错中断下载的问题，Aria2更加稳定灵活，因此采用Aria2
        //动图在下载后转成GIF
        private async Task<List<int>> DownloadIllusts(HashSet<int> id_list, int limit=-1)
        {
            //注意：有的图本来就是半边虚的！
            try
            {
                //关闭旧的aria2，已有任务没有重复利用的必要，全部放弃
                foreach (var process in System.Diagnostics.Process.GetProcessesByName("aria2c(PixivAss)"))
                    process.Kill();
                {//启动aria2
                    var process = new System.Diagnostics.Process();
                    //右斜杠和左斜杠都可以但是不能混用(不知道为什么)
                    process.StartInfo.WorkingDirectory = System.IO.Directory.GetCurrentDirectory() + @"\aria2";
                    process.StartInfo.FileName = "aria2c(PixivAss).exe";
                    process.StartInfo.RedirectStandardOutput = false;
                    process.StartInfo.UseShellExecute = true;
#if !DEBUG
                    process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
#endif
                    //                    process.StartInfo.Arguments = String.Format(@"--conf-path=aria2.conf --all-proxy=""{0}"" --header=""Cookie:{1}""", download_proxy,cookie_server.cookie);
                    //                    process.StartInfo.Arguments = String.Format(@"--conf-path=aria2.conf --all-proxy=""{0}""", download_proxy);
                    //[del]不需要代理[/del]，由于迷之原因，现在需要referer和代理才能下载了，而且岛风go还不行
                    //不要带cookie，会收到警告信
                    process.StartInfo.Arguments = String.Format(@"--conf-path=aria2.conf --all-proxy=""{0}"" --referer=https://www.pixiv.net/",request_proxy);
                    process.Start();
                }
                //移除临时文件
                foreach (var file in Directory.GetFiles(download_dir_main, "*.aria2"))//下载临时文件
                    File.Delete(file);
                foreach (var file in Directory.GetFiles(download_dir_main, "*.zip"))//动图临时文件
                    File.Delete(file);
                //创建下载任务
                List<Illust> illustList = await database.GetIllustFull(id_list.ToList());
                var download_illusts = new List<Illust>();
                var processed_illusts = new List<int>();
                int download_ct = 0;
                var queue = new TaskQueue<bool>(3000);
                foreach (var illust in illustList)
                {
                    bool downloaded = false;
                    for (int i = 0; i < illust.pageCount; ++i)
                    {
                        string store_file_name = illust.storeFileName(i);
                        bool exist = File.Exists(download_dir_main + "/" + store_file_name);
                        if (illust.shouldDownload(i)&&!exist)//下载时使用downloadFileName
                        {
                            await queue.Add(DownloadIllustByAria2(illust.URL(i), download_dir_main, illust.downloadFileName(i)));
                            download_ct++;
                            downloaded = true;
                        }
                        //我硬盘贼大，没有必要删除多余的图片
                    }
                    if (downloaded)
                        download_illusts.Add(illust);
                    else
                        processed_illusts.Add(illust.id);
                    if (limit >= 0 && download_ct >= limit)
                        break;
                }
                await queue.Done();
                int ct = 0;
                queue.done_task_list.ForEach(task => ct += task.Result ? 1 : 0);
                Console.WriteLine(String.Format("Download Begin {0}", ct));
                //等待完成并查询状态
                while (!await QueryAria2Status()) await Task.Delay(new TimeSpan(0, 0, 60));

                //将动图转为GIF
                {
                    var ugorias = new HashSet<Illust>();
                    foreach (var illust in download_illusts)
                        if (illust.isUgoira())
                        {
                            bool fail = false;
                            for (int i = 0; i < illust.pageCount; ++i)
                                if (!File.Exists(download_dir_main + "/" + illust.downloadFileName(i)))
                                    fail = true;
                            if (fail) continue;
                            ugorias.Add(illust);
                        }
                    await UgoiraToGIF(ugorias);
                }
                //检查结果，以本地文件为准，无视aria2和函数的返回
                {
                    int success_ct = 0;
                    var fail_illusts = new HashSet<int>();
                    foreach (var illust in download_illusts)
                    {
                        bool fail = false;
                        for (int i = 0; i < illust.pageCount; ++i)
                            if (!File.Exists(download_dir_main + "/" + illust.storeFileName(i)))
                                fail = true;
                        if (fail)
                        {
                            //不更新近两周的作品以避免反复Fetch
                            if((DateTime.Now-illust.updateTime).TotalDays>14)
                                fail_illusts.Add(illust.id);
                            Console.WriteLine("D Fail", illust.id);
                        }
                        else
                        {
                            success_ct++;
                            processed_illusts.Add(illust.id);
                        }
                    }
                    Console.WriteLine(String.Format("Download Done, {0}/{1} Success", success_ct, download_illusts.Count));
                    //无法下载可能是因为已删除，重新加入Fetch队列以避免反复下载
                    await AddToIllustFetchQueue(fail_illusts, null, false);
                }
                return processed_illusts;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                throw;
            }
        }
        private async Task DownloadIllustsInExplorerQueue()
        {
            //浏览队列+队列与收藏的作者
            var id_list = new HashSet<int>
                    ((await database.GetQueue()).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .ToList<string>()
                    .Select<string, int>(x => Int32.Parse(x)));
            (await database.GetIllustIdOfQueuedOrFollowedUser()).ForEach(id => id_list.Add(id));
            await DownloadIllusts(id_list, -1);
        }
        private async Task UgoiraToGIF (HashSet<Illust> illustList)
        {
            DateTime t = DateTime.Now;
            var queue = new TaskQueue<bool>(200);
            foreach (var illust in illustList)//耗时间的不是文件读写而是转码，所以要并行
                queue.Add(Task.Run(()=>UgoiraToGIF(illust)));
            await queue.Done();
            int ct = 0;
            queue.done_task_list.ForEach(task => ct += task.Result?1:0);
            Console.WriteLine(String.Format("Ugoira2GIF {0} Done in {1}s",ct, DateTime.Now.Subtract(t).TotalSeconds));
        }
        private async Task<bool> UgoiraToGIF(Illust illust)
        {
            try
            {
                String tmp_dir = String.Format("{0}/{1}", download_dir_ugoira_tmp, illust.id);
                if (!Directory.Exists(tmp_dir))
                    Directory.CreateDirectory(tmp_dir);
                else
                    foreach (var file in Directory.GetFiles(tmp_dir, "*.*"))//下载临时文件
                        File.Delete(file);

                //假定全部是zip且只有1p
                string zip_file = String.Format("{0}/{1}", download_dir_main, illust.downloadFileName(0));
                ZipFile.ExtractToDirectory(zip_file, tmp_dir);
                var frame_info = illust.ugoiraFrames.Split('`');
                var frame_name = new List<string>();
                var frame_interval = new List<int>();
                for (int i = 0; i + 1 < frame_info.Length; i += 2)
                {
                    frame_name.Add(frame_info[i]);
                    frame_interval.Add(int.Parse(frame_info[i + 1]));
                }
                if (frame_name.Count <= 0)
                    return false;
                using (var animated = new SixLabors.ImageSharp.Image<Rgba32>(illust.width, illust.height))
                {
                    for (int i = 0; i < frame_name.Count; ++i)
                    {
                        var path = String.Format("{0}/{1}", tmp_dir, frame_name[i]);
                        using (var img = SixLabors.ImageSharp.Image.Load(path))
                        {
                            if (illust.width != img.Width || illust.height != img.Height)
                                img.Mutate(x => x.Resize(illust.width, illust.height));//每张图的size可能跟illust不一样
                            img.Frames.First().Metadata.GetGifMetadata().FrameDelay = frame_interval[i] / 10;//单位不一致
                            animated.Frames.AddFrame(img.Frames[0]);
                        }
                    }
                    animated.Frames.RemoveFrame(0);//移除初始帧，frames不能为空，所以要最后移除
                    await animated.SaveAsGifAsync(String.Format("{0}/{1}", download_dir_main, illust.storeFileName(0)));
                }
                File.Delete(zip_file);
                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(String.Format("Ugoira cast Fail {0}", illust.id));
                Console.Error.WriteLine(e.Message);
            }
            return false;
        }
        private async Task SyncBookmarkDirectory()
        {
            var private_files = new HashSet<string>();
            var pub_files = new HashSet<string>();
            var illust_ids = await database.GetBookmarkIllustId(true);
            illust_ids.AddRange(await database.GetBookmarkIllustId(false));
            foreach (var illust in await database.GetIllustFull(illust_ids))
            {
                string dir = illust.bookmarkPrivate ? download_dir_bookmark_private : download_dir_bookmark_pub;
                for (int i = 0; i < illust.pageCount; ++i)
                    if(illust.isPageValid(i))
                    {
                        string file_name = illust.storeFileName(i);
                        string dest = String.Format("{0}/{1}", dir, file_name);
                        string src = String.Format("{0}/{1}", download_dir_main, file_name);
                        if (!File.Exists(dest) && File.Exists(download_dir_main + "/" + file_name))
                        {
                            try
                            {
                                File.Copy(src, dest, true);
                            }
                            catch (System.IO.IOException) { }//此时文件可能被Explorer的缓存占用,复制不需要立刻完成，因此忽略该异常
                        }
                        if (illust.bookmarkPrivate)
                            private_files.Add(file_name);
                        else
                            pub_files.Add(file_name);
                    }
            }
            foreach (var file in Directory.GetFiles(download_dir_bookmark_pub, "*.*"))//下载临时文件
                if(!pub_files.Contains(Path.GetFileName(file)))
                    File.Delete(file);
            foreach (var file in Directory.GetFiles(download_dir_bookmark_private, "*.*"))//下载临时文件
                if (!private_files.Contains(Path.GetFileName(file)))
                    File.Delete(file);
        }

        private async Task<Dictionary<int, int>> RequestAllKeywordSearchIllustBlock(int idx_block)
        {
            //like越低分布越密集,
            var BLOCK_LIKE_COUNT      = new List<int> { 1600, 2000, 2500, 3000, 4000, 6000, 15000, 30000, 60000, 120000, 200000,300000,-1 };
            //避免低处分块过密导致Illust多次跨越分块，反复触发Fetch
            var BLOCK_FAKE_LIKE_COUNT = new List<int> { 1600, 1600, 1600, 1600, 4000, 4000, 15000, 30000, 60000, 120000, 200000,300000,-1 };
            //避免高处分块稀疏浪费天数
            var BLOCK_MERGE_LIKE_COUNT= new List<int> { 0,    1,    2,    3,    4,    5,    6, BLOCK_LIKE_COUNT.Count-1 };
            int BLOCKS_WORD = 5;
            int BLOCKS_LIKE = BLOCK_MERGE_LIKE_COUNT.Count-1;

            var ret = new Dictionary<int,int>();

            var key_word_list = new List<string>();
            //关键字分块
            {
                int idx_word = (idx_block/ BLOCKS_LIKE) % BLOCKS_WORD;//先轮换like数再轮换关键字
                var tmp = "";
                var tags = await database.GetFollowedTagsOrdered();
                int tags_count = tags.Count;
                //用or合并关键字可以减少重复
                foreach (var word in tags.Take(tags_count * (idx_word + 1) / BLOCKS_WORD).Skip(tags_count * idx_word / BLOCKS_WORD))
                {
                    tmp += (tmp.Length > 0 ? "%20OR%20" : "") + System.Web.HttpUtility.UrlEncode(word);
                    if (tmp.Length > 1600)
                    {
                        key_word_list.Add(tmp);
                        tmp = "";
                    }
                }
            }
            //like数分块
            int start_idx = BLOCK_MERGE_LIKE_COUNT[idx_block % BLOCKS_LIKE];
            int end_idx   = BLOCK_MERGE_LIKE_COUNT[idx_block % BLOCKS_LIKE +1];
            for (int idx_like= start_idx; idx_like< end_idx; ++idx_like)
            {
                int min_like_count = BLOCK_LIKE_COUNT[idx_like];
                int max_like_count = BLOCK_LIKE_COUNT[idx_like+1];
                var result =await RequestAllKeywordSearchIllust(key_word_list, min_like_count, max_like_count);
                foreach(var id in result)
                {
                    if (ret.ContainsKey(id))
                        ret[id] = BLOCK_FAKE_LIKE_COUNT[idx_like];
                    else
                        ret.Add(id, BLOCK_FAKE_LIKE_COUNT[idx_like]);
                }
            }
            return ret;
        }
        //搜索包含任意指定关键字，且likeCount位于指定区间的Illust
        private async Task<HashSet<int>> RequestAllKeywordSearchIllust(List<string> key_word_list, int like_count_min, int like_count_max)
        {
            var task_list = new Dictionary<string, Task<List<int>>>();
            var ret = new HashSet<int>();
            //按页搜索
            int start_page = 0;
            int step = 50;
            int page_count = 0;
            while(key_word_list.Count>0)
            {
                foreach(var key in key_word_list)
                {
                    var task= RequestSearchResult(key, false, start_page, start_page + step, like_count_min, like_count_max);
                    task_list[key] = task;
                    page_count += step;
                }
                await Task.WhenAll(task_list.Values);
                //丢掉已经搜索完的
                foreach (var key in task_list.Keys)
                    if (task_list[key].Result.Count == 0)
                    {
                        key_word_list.Remove(key);
                        Console.WriteLine("Keyword(Tag) " + key.Substring(0,20)+" Done");
                    }
                //合并结果
                foreach (var task in task_list.Values)
                    foreach (var id in task.Result)
                        if(!ret.Contains(id))
                            ret.Add(id);
                task_list.Clear();
                Console.WriteLine(String.Format("Search Res {0}p: {1}",start_page,ret.Count));
                start_page += step;
            }
            Console.WriteLine(String.Format("Search Done:{0} in {1} pages ",ret.Count,page_count));
            return ret;
        }
        private async Task<HashSet<int>> RequestAllQueuedAndFollowedUserIllust()
        {
            var queue = new TaskQueue<List<int>>(1000);
            (await database.GetFollowedUser()).ForEach(async user => await queue.Add(RequestAllByUserId(user.userId)));
            (await database.GetQueuedUser()  ).ForEach(async user => await queue.Add(RequestAllByUserId(user.userId)));
            return await queue.GetResultSet();
        }
        private async Task<HashSet<int>> RequestAllCurrentRankIllust()
        {
            var queue = new TaskQueue<List<int>>(100);
            //一般每种排行总数在500左右浮动，一页50，RequestRankPage可以获得总数。
            //但是反正页数很少并且只需要固定数量，没有必要知道总共几页
            foreach (var mode in new List<string> { "daily", "weekly","monthly","male","daily_r18","weekly_r18","male_r18"})
                for (int p=0;p<2;++p)//只获取前两页以减少访问量
                    await queue.Add(RequestRankPage(mode,p));
            return await queue.GetResultSet();
        }

        //获取并更新该作者的所有作品
        private async Task<List<int>> RequestAllByUserId(int userId)
        {
            string url = String.Format("{0}ajax/user/{1}/profile/all", base_url, userId);
            string referer = String.Format("{0}member_illust.php?id={1}", base_url, user_id);
            JObject ret = await RequestJsonAsync(url, referer,true);
            if (ret.Value<Boolean>("error"))
            {
                //throw new Exception("Get All By User Fail " + userId + " " + ret.Value<string>("message"));
                Console.Error.WriteLine("Get All By User Fail " + userId + " " + ret.Value<string>("message"));
                return new List<int>();
            }
            var idList = new List<int>();
            foreach (var type in new List<string>{ "illusts"/*,"manga"*/}) //暂时只看插画,不看漫画
                if (ret.Value<JObject>("body").GetValue("illusts").Type==JTokenType.Object)//为空时不是object而是array很坑爹
                    foreach (var illust in ret.Value<JObject>("body").Value<JObject>("illusts"))
                            idList.Add(Int32.Parse(illust.Key));
            return idList;
        }
        //去掉重复以及不必更新的Illust，将剩下的加入illust_update_queue
        private async Task AddToIllustFetchQueue(HashSet<int> list_bytime, Dictionary<int,int> list_bylike, bool only_necessary = true)
        {
            int tmp = illust_fetch_queue.Count;
            if (only_necessary)
            {
                var local_illust = (await database.GetIllustIdAndTimeAndLikeCount()).ToDictionary(illust => illust.id);
                if(list_bytime != null)
                    foreach (var id in list_bytime)//不在本地或更新时间距今UPDATE_INTERVAL以上的图
                        if ((!local_illust.ContainsKey(id))
                            || local_illust[id].updateTime < DateTime.Now.AddDays(-UPDATE_INTERVAL))
                            illust_fetch_queue.Add(id);
                if (list_bylike != null)
                    foreach (var pair in list_bylike)//不在本地或like数明显小于真实值的图
                        if ((!local_illust.ContainsKey(pair.Key))
                            || local_illust[pair.Key].likeCount < pair.Value)
                            illust_fetch_queue.Add(pair.Key);
            }
            else
            {
                if (list_bytime != null)
                    foreach (var id in list_bytime)
                        illust_fetch_queue.Add(id);
                if (list_bylike != null)
                    foreach (var id in list_bylike.Keys)
                        illust_fetch_queue.Add(id);
            }
            Console.WriteLine("Update Illusts Queue {0} + {1} ",tmp, illust_fetch_queue.Count-tmp);
        }
        private async Task ProcessIllustFetchQueue(int count)
        {
            int tmp = illust_fetch_queue.Count;
            var queue = new TaskQueue<Illust>(3000);
            foreach( var id in illust_fetch_queue.ToList().Take(Math.Min(count, illust_fetch_queue.Count)))
                await queue.Add(RequestIllustAsync(id));
            await queue.Done();

            var illustList = new List<Illust>();
            queue.done_task_list.ForEach(task =>
            {
                if (task.Result != null)
                    illustList.Add(task.Result);
            });
            database.UpdateIllustOriginalData(illustList);

            illustList.ForEach(illust=>illust_fetch_queue.Remove(illust.id));
            Console.WriteLine("Process Fetch Queue => {0}-{1} ",tmp,tmp - illust_fetch_queue.Count);
        }
        private async Task ProcessIllustDownloadQueue(int count)
        {
            int tmp = illust_download_queue.Count;
            var ret=await DownloadIllusts(illust_download_queue, count);
            ret.ForEach(id => illust_download_queue.Remove(id));
            Console.WriteLine("Process Download Queue => {0}-{1} ", tmp, tmp - illust_download_queue.Count);
        }

        private async Task<Dictionary<int,Int64>> RequestBookMarkIllust(bool pub,bool get_bookmark_id=false)
        {
            int total = 0;
            int offset = 0;
            int page_size = 48;
            var idList = new Dictionary<int, Int64>();
            //查询收藏作品的id
            while (offset == 0 || offset < total)
            {
                //和获取其它用户的收藏不同，limit不能过高
                string url = String.Format("{0}ajax/user/{1}/illusts/bookmarks?tag=&offset={2}&limit={3}&rest={4}",
                                            base_url, user_id, offset, page_size, pub ? "show" : "hide");
                string referer = String.Format("{0}bookmark.php?id={1}&rest={2}", base_url, user_id, pub ? "show" : "hide");
                JObject ret = await RequestJsonAsync(url, referer,false);
                if (ret.Value<Boolean>("error"))
                    throw new Exception("Get Bookmark Fail");
                //获取总数,仅用于提示
                //实际总数可能更小,因为删除的作品会被剔掉但是不会从总数中扣除
                //已删除的作品仍然会占位,因此每次获取到的数量可能少于page_size
                if (offset == 0)
                    total = ret.GetValue("body").Value<int>("total");
                //获取该页的id
                foreach (var illust in ret.GetValue("body").Value<JArray>("works"))
                    idList[illust.Value<int>("id")]=Int64.Parse(illust.Value<JObject>("bookmarkData").Value<string>("id"));
                offset += page_size;
            }
            return idList;
        }

        //获取并更新所有收藏作品
        //只在最初使用
        private async Task FetchAllBookMarkIllust(bool pub)
        {
            var idList =new HashSet<int>((await RequestBookMarkIllust(pub)).Keys);
            //将当前有效的已收藏作品和可能已无效/已移除收藏的作品(即本地存储的已收藏作品)一起更新状态
            //FetchIllust时会获取Illust是否已收藏状态，所以不需要另行更新收藏状态
            int tmp = idList.Count;
            foreach (var illust_id in await database.GetBookmarkIllustId(pub))
                if (!idList.Contains(illust_id))
                    idList.Add(illust_id);
            await AddToIllustFetchQueue(idList,null);
            Console.WriteLine(String.Format("Fetch {0}/{1}  Bookmarks",pub?"Public":"Private",tmp));
        }
        //WIP
        private async Task PushAllBookMark()
        {
            var remote_list = await RequestBookMarkIllust(true);
            remote_list = remote_list.Union(await RequestBookMarkIllust(false)).ToDictionary<KeyValuePair<int,Int64>,int, Int64>(kv => kv.Key, kv => kv.Value);
            var local_list_pub = await database.GetBookmarkIllustId(true);
            var local_list_private = await database.GetBookmarkIllustId(false);
            var queue = new TaskQueue<bool>(50);
            foreach (var pair in remote_list)
                if ((!local_list_pub.Contains(pair.Key)) && !local_list_private.Contains(pair.Key))
                    await queue.Add(PushBookmark(false,pair.Key,false,pair.Value));
            foreach (var id in local_list_pub)
                await queue.Add(PushBookmark(true, id, true));
            foreach (var id in local_list_private)
                await queue.Add(PushBookmark(true, id, false));
        }

        //获取并更新关注的作者状态
        private async Task FetchAllFollowedUser()
        {
            int total = await RequestFollowedUserCount();
            var userList = new List<User>();
            var queue = new TaskQueue<JObject>(50);
            for (int i = 0; i < total; i += 50)//因为关注作者本来就少，还是一次获取一页，其实可以不用TaskQueue
            {
                string url = String.Format("{0}ajax/user/{1}/following?offset={2}&limit=50&rest=show", base_url, user_id,i);
                string referer = String.Format("{0}bookmark.php?id={1}&rest=show", base_url, user_id);
                await queue.Add(RequestJsonAsync(url, referer,false));
            }
            await queue.Done();
            foreach(var task in queue.done_task_list)
            {
                JObject ret = task.Result;
                if (ret.Value<Boolean>("error"))
                    throw new Exception("Get Bookmark Fail");
                foreach (var user in ret.GetValue("body").Value<JArray>("users"))
                    userList.Add(new User(user.ToObject<JObject>()));
            }
            database.UpdateFollowedUser(userList);
            Console.WriteLine("Fetch " + userList.Count.ToString() + " followings");
        }
        //更新所有已知的未关注作者状态
        private async Task FetchAllUnfollowedUserStatus()
        {
            //每隔70天更新全部，没有名字的立刻更新
            var user_list = await database.GetUnFollowedUserNeedUpdate(DateTime.Now.AddDays(-7 * 10));
            var queue = new TaskQueue<User>(2000);
            foreach (var user in user_list)
                await queue.Add(RequestUserAsync(user.userId));
            await queue.Done();
            user_list.Clear();
            foreach (var task in queue.done_task_list)
                if(task.Result!=null)
                    user_list.Add(task.Result);
            database.UpdateUserName(user_list);
            Console.WriteLine("Done");
        }

        //确认是否成功登录
        private async Task CheckHomePage()
        {
            VerifyState = "Checking";
            string url = base_url;
            string referer = String.Format("{0}", base_url);
            var doc =await RequestHtmlAsync(base_url,referer);
            if(doc != null)
            {
                HtmlNode headNode = doc.DocumentNode.SelectSingleNode("//meta[@id='meta-global-data']");
                if (headNode != null)
                {
                    var json_object = (JObject)JsonConvert.DeserializeObject(headNode.Attributes["content"].Value);
                    if (json_object != null && json_object.Value<JObject>("userData") != null)
                    {
                        var name = json_object.Value<JObject>("userData").Value<String>("name");
                        if (name == this.user_name)
                        {
                            VerifyState = "Login Success";
                            return;
                        }
                    }
                }
            }
            VerifyState = "Login Fail";
            Console.Error.WriteLine("Login Fail");
            throw new TopLevelException("Login Not Success");
        }
    }
}
