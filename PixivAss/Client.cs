using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PixivAss.Data;
using IDManLib;

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
        private string verify_state="Waiting";
        private string download_dir_root;
        private string user_id;
        private string base_url;
        private string base_host;
        private string user_name;
        private string download_dir_bookmark_pub;
        private string download_dir_bookmark_private;
        public string download_dir_main;
        public string special_dir;
        private CookieServer cookie_server;
        public  Database database;
        private HttpClient httpClient;
        private HttpClient httpClient_anonymous;//不需要登陆的地方使用不带cookie的客户端，以防被网站警告
        private HashSet<string> banned_keyword;
        private Uri aria2_rpc_addr =new Uri("http://127.0.0.1:4322/jsonrpc");
        private string aria2_rpc_secret = "{1BF4EE95-7D91-4727-8934-BED4A305CFF0}";
        private string request_proxy;
        //private string download_proxy = "127.0.0.1:8000";下载图片不需要代理

        //public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public Client(Config config)
        {
            download_dir_root = config.DownloadDir;
            download_dir_bookmark_pub = download_dir_root+"pub";
            download_dir_bookmark_private = download_dir_root+"private";
            download_dir_main = download_dir_root+"tmp";
            special_dir = download_dir_root +"special";

            database = new Database("root","pixivAss","pass");
            request_proxy = config.Proxy;
            user_id = config.UserId;
            user_name = config.UserName;
            base_url = "https://www.pixiv.net/";
            base_host = "www.pixiv.net";
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
            //DailyTask();
            //InitTask();
            //RunSchedule();
            //DownloadIllusts(true);
        }
        public void Dispose()
        {
            httpClient.Dispose();
        }
        public async Task<string> Test()
        {/*            
            var list = await RequestAllByUserId(55875);
            var queue = new TaskQueue<Illust>(500);
            foreach (var illust in list)
                await queue.Add(RequestIllustAsync(illust));
            await queue.Done();
            var process = new System.Diagnostics.Process();            
            process.StartInfo.WorkingDirectory = System.IO.Directory.GetCurrentDirectory() + @"\aria2";//右斜杠和左斜杠都可以但是不能混用(不知道为什么)
            process.StartInfo.FileName = "aria2c(PixivAss).exe";
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.Arguments = String.Format(@"--conf-path=aria2.conf --http-proxy=""{0}"" --header=""Cookie:{1}""", download_proxy, cookie_server.cookie);
            process.Start();
            var d_queue = new TaskQueue<bool>(500);
            foreach (var task in queue.done_task_list)
            {
                var illust = task.Result;
                var dir ="G:/p0/" + illust.title.Substring(0,3) + illust.id.ToString();
                Directory.CreateDirectory(dir);
                for (int i = 0; i < illust.pageCount; ++i)
                {
                    string url = String.Format(illust.urlFormat, i);
                    string file_name = GetDownloadFileName(illust, i);
                    await d_queue.Add(DownloadIllustForceAria2(url, dir, file_name));
                }
            }
            await d_queue.Done();*/
            return "";
           // var list = await database.GetBookmarkIllustId(true);
//            await PushAllBookMark();
            var new_list=await RequestBookMarkIllust(true);
            await PushBookmark(false, 258, true,new_list[258]);
            var new_list_2 = await RequestBookMarkIllust(true);
            /*
            Console.WriteLine(list.Count.ToString()+" -> "+new_list.Count.ToString());
            foreach (var i in new_list)
                if (!list.Contains(i.Key))
                    Console.WriteLine(i.Key);
            */
            return "12s3";
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
            //在每日1点执行定时任务
            //假定任务耗时不会超过23小时，即每天都会触发一次
            do
            {
                DateTime next = DateTime.Today.AddDays(1).AddHours(1.0); //次日1：00
                int waiting_time = (int)((next - DateTime.Now).TotalMilliseconds);
                await Task.Delay(waiting_time);
                await DailyTask();
            }
            while (true);
        }
        public async Task DailyTask()
        {
            //第一次运行之后，FollowedUser和BookmarkIllust由本地向远程单向更新
            var illust_list = new HashSet<int>();
            bool do_week_task = DateTime.Now.DayOfWeek == System.DayOfWeek.Tuesday;//每周一次
            if (do_week_task)//需要在FetchIllust之前
            {
                illust_list.UnionWith(await RequestAllQueuedAndFollowedUserIllust());
                illust_list.UnionWith(await RequestAllKeywordSearchIllust());
                illust_list.UnionWith(await database.GetAllIllustIdNeedUpdate(DateTime.UtcNow.AddDays(-21)));
            }
            Console.WriteLine("Fetch Task 1/3 {0}", illust_list.Count);
            //每天一次
            illust_list.UnionWith(await RequestAllCurrentRankIllust());
            //FetchIllust
            await FetchIllustByIdWhenNeccessary(illust_list);
            Console.WriteLine("Fetch Task 2/3 {0}",illust_list.Count);
            if (do_week_task)//需要在FetchIllust之后
            {
                await GenerateQueue();
                await FetchAllUnfollowedUserStatus();
                //await DownloadIllusts();
            }
            await DownloadIllusts(true);
            Console.WriteLine("Fetch Task Done");
        }
        //初次执行，将收藏的作者和图同步到本地
        public async Task InitTask()
        {
            var illust_list = new HashSet<int>();
            await FetchAllFollowedUser();
            await FetchAllBookMarkIllust(true);
            await FetchAllBookMarkIllust(false);
            illust_list.UnionWith(await RequestAllQueuedAndFollowedUserIllust());
            await FetchIllustByIdWhenNeccessary(illust_list);
            await FetchAllUnfollowedUserStatus();
            await DownloadIllusts(true);
            Console.WriteLine("Fetch Task Done");
        }

        private async Task GenerateQueue(bool force=false)
        {
            const int UpdateInterval = 7 * 24 * 60 * 60;//每超过这个时间才刷新
            const int MaxSize = 10000;
            if (force||(await database.GetQueueUpdateInterval())>UpdateInterval||(await database.GetQueue()).Length<2)
            {
                var illust_list =await database.GetAllUnreadedIllustFull();
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
            }            
        }

        /*
         * Request:从远端查询并返回结果
         * Fetch:从远端查询并存储到数据库
         * Push:将本地结果推送到远端
         */
        //同步所有图片到本地，如有必要则进行下载/删除，要求Illust信息都已更新过
        //IDM下载更快，但是各行为耗时且互相阻塞和报错中断下载的问题，Aria2更加稳定灵活，因此采用Aria2
        //所有图片都存储到dir_main,再拷贝到dir_bookmark
        private async Task DownloadIllusts(bool only_necessary=false)
        {
            //注意：有的图本来就是半边虚的！
            try
            {
                //关闭旧的aria2，已有任务没有重复利用的必要，全部放弃
                foreach (var process in System.Diagnostics.Process.GetProcessesByName("aria2c(PixivAss)"))
                    process.CloseMainWindow();
                {//启动aria2
                    var process = new System.Diagnostics.Process();
                    //右斜杠和左斜杠都可以但是不能混用(不知道为什么)
                    process.StartInfo.WorkingDirectory = System.IO.Directory.GetCurrentDirectory() + @"\aria2";
                    process.StartInfo.FileName = "aria2c(PixivAss).exe";
                    process.StartInfo.RedirectStandardOutput = false;
                    process.StartInfo.UseShellExecute = true;
                    process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    //                    process.StartInfo.Arguments = String.Format(@"--conf-path=aria2.conf --all-proxy=""{0}"" --header=""Cookie:{1}""", download_proxy,cookie_server.cookie);
                    //                    process.StartInfo.Arguments = String.Format(@"--conf-path=aria2.conf --all-proxy=""{0}""", download_proxy);
                    //不需要代理和cookie
                    //不要带cookie，会收到警告信
                    process.StartInfo.Arguments = String.Format(@"--conf-path=aria2.conf");
                    process.Start();
                }
                var queue = new TaskQueue<bool>(3000);
                List<Illust> illustList;
                if (only_necessary)
                {
                    //浏览队列+队列与收藏的作者
                    var id_list = (await database.GetQueue()).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                           .ToList<string>()
                           .Select<string, int>(x => Int32.Parse(x))
                           .ToList<int>();
                    illustList =await database.GetIllustFull(id_list);
                    illustList.AddRange(await database.GetAllIllustFullOfQueuedOrFollowedUser());
                }
                else
                    illustList = await database.GetAllIllustFull();
                //移除aria2临时文件
                foreach (var file in Directory.GetFiles(download_dir_main, "*.aria2"))
                    File.Delete(file);
                //创建下载任务
                foreach (var illust in illustList)
                {
                    for (int i = 0; i < illust.pageCount; ++i)
                    {
                        string url = String.Format(illust.urlFormat, i);
                        string file_name = GetDownloadFileName(illust, i);
                        bool exist = File.Exists(download_dir_main + "/" + file_name);
                        if (GetShouldDownload(illust, i))
                        {//别忘了大括号
                            if (!exist)
                                await queue.Add(DownloadIllustForceAria2(url, download_dir_main, file_name));
                        }
                        else if (exist && GetShouldDelete(illust, i))//假定没有垃圾文件
                            File.Delete(download_dir_main + "/" + file_name);
                    }
                }
                await queue.Done();
                int ct = 0;
                queue.done_task_list.ForEach((Task<bool> task)=> { ct+= task.Result ? 1 : 0; });
                Console.WriteLine(String.Format("Downloaded Begin{0}", ct));
                //等待完成并查询状态
                while (!await QueryAria2Status()) await Task.Delay(new TimeSpan(0, 0, 30));
                //完成后拷贝
                foreach (var illust in illustList)
                    if(illust.bookmarked)
                    {
                        string dir = illust.bookmarkPrivate ? download_dir_bookmark_private : download_dir_bookmark_pub;
                        for (int i = 0; i < illust.pageCount; ++i)
                        {
                            string file_name = GetDownloadFileName(illust, i);
                            if(File.Exists(download_dir_main + "/" + file_name))
                                File.Copy(download_dir_main + "/" + file_name, dir+"/"+file_name,true);
                        }
                    }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        private async Task DownloadIllustsTmp()
        {
            try
            {
                var queue = new TaskQueue<bool>(3000);
                List<Illust> illustList = await database.GetIllustFullSortedByUser(13379747);
                //创建下载任务
                foreach (var illust in illustList)
                {
                    for (int i = 0; i < illust.pageCount; ++i)
                    {
                        string url = String.Format(illust.urlFormat, i);
                        string file_name = GetDownloadFileName(illust, i);
                        await queue.Add(DownloadIllustForceAria2(url, download_dir_main, file_name));
                    }
                }
                await queue.Done();
                int ct = 0;
                queue.done_task_list.ForEach((Task<bool> task) => { ct += task.Result ? 1 : 0; });
                Console.WriteLine(String.Format("Downloaded Begin{0}", ct));
                //等待完成并查询状态
                while (!await QueryAria2Status()) await Task.Delay(new TimeSpan(0, 0, 30));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        private async Task<HashSet<int>> RequestAllKeywordSearchIllust()
        {
            var ret =new HashSet<int>();
            var key_word_list=new List<string>();
            var task_list = new Dictionary<string, Task<List<int>>>();
            var tmp = "";
            //用or合并关键字可以减少重复
            foreach (var word in await database.GetFollowedTags())
            {
                tmp += (tmp.Length > 0 ? "%20OR%20" : "") + System.Web.HttpUtility.UrlEncode(word);
                if (tmp.Length > 1600)
                {
                    key_word_list.Add(tmp);
                    tmp = "";
                }
            }
            int start_page = 0;
            int step = 100;
            while(key_word_list.Count>0)
            {
                foreach(var key in key_word_list)
                {
                    var task= RequestSearchResult(key, false, start_page, start_page + step);
                    task_list[key] = task;
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
                        ret.Add(id);
                task_list.Clear();
                Console.WriteLine(String.Format("Search Res {0}: {1}",start_page,ret.Count));
                start_page += step;
            }
            Console.WriteLine("Final Search Res: " + ret.Count.ToString());
            return ret;
        }
        private async Task<HashSet<int>> RequestAllQueuedAndFollowedUserIllust()
        {
            var queue = new TaskQueue<List<int>>(1000);
            foreach (var user in await database.GetFollowedUser())
                await queue.Add(RequestAllByUserId(user.userId));
            foreach (var user in await database.GetQueuedUser())
                await queue.Add(RequestAllByUserId(user.userId));
            return await queue.GetResultSet();
        }
        private async Task<HashSet<int>> RequestAllCurrentRankIllust()
        {
            var queue = new TaskQueue<List<int>>(100);
            //一般每种排行总数在500左右浮动，一页50，RequestRankPage可以获得总数。
            //但是反正页数很少并且只需要固定数量，没有必要知道总共几页
            foreach (var mode in new List<string> { "daily", "weekly","monthly","male","daily_r18","weekly_r18","male_r18"})
                for(int p=0;p<5;++p)
                    await queue.Add(RequestRankPage(mode,p));
            return await queue.GetResultSet();
        }

        //获取并更新该作者的所有作品
        private async Task<List<int>> RequestAllByUserId(int userId)
        {
            string url = String.Format("{0}ajax/user/{1}/profile/all", base_url, userId);
            string referer = String.Format("{0}member_illust.php?id={1}", base_url, user_id);
            JObject ret = await RequestJsonAsync(url, referer);
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get All By User Fail "+userId);
            var idList = new List<int>();
            foreach (var type in new List<string>{ "illusts"/*,"manga"*/}) //暂时只看插画,不看漫画
                if (ret.Value<JObject>("body").GetValue("illusts").Type==JTokenType.Object)//为空时不是object而是array很坑爹
                    foreach (var illust in ret.Value<JObject>("body").Value<JObject>("illusts"))
                            idList.Add(Int32.Parse(illust.Key));
            return idList;
        }
        //先从本地去重再FetchIllustByIdList
        private async Task FetchIllustByIdWhenNeccessary(HashSet<int> id_list)
        {
            var no_need_update_illust =new HashSet<int>(await database.GetAllIllustIdNeedUpdate(DateTime.UtcNow.AddDays(-14), true));
            var all_illust = new List<int>();
            foreach (var id in id_list)
                if (!no_need_update_illust.Contains(id))
                    all_illust.Add(id);
            await FetchIllustByIdForce(all_illust);
        }
        //获取并更新指定的作品
        private async Task FetchIllustByIdForce(List<int> illustIdList)
        {
            Console.WriteLine("Begin Fetch {0}",illustIdList.Count);
            var queue = new TaskQueue<Illust>(3000,50000,task_list=> {
                var illustList = new List<Illust>();
                foreach (var task in task_list)
                    illustList.Add(task.Result);
                database.UpdateIllustOriginalData(illustList);
            });
            foreach (var illustId in illustIdList)
                await queue.Add(RequestIllustAsync(illustId));
            await queue.Done();
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
                JObject ret = await RequestJsonAsync(url, referer);
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
            var idList =(await RequestBookMarkIllust(pub)).Keys.ToList<int>();
            //将当前有效的已收藏作品和可能已无效/已移除收藏的作品(即本地存储的已收藏作品)一起更新状态
            //FetchIllust时会获取Illust是否已收藏状态，所以不需要另行更新收藏状态
            int tmp = idList.Count;
            foreach (var illust_id in await database.GetBookmarkIllustId(pub))
                if (!idList.Contains(illust_id))
                    idList.Add(illust_id);
            await FetchIllustByIdForce(idList);
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
                await queue.Add(RequestJsonAsync(url, referer));
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
            var user_list = await database.GetFollowedUser(false);
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
            HtmlNode headNode = doc.DocumentNode.SelectSingleNode("//meta[@id='meta-global-data']");
            if (headNode != null)
            {
                var json_object = (JObject)JsonConvert.DeserializeObject(headNode.Attributes["content"].Value);
                if (json_object!=null&& json_object.Value<JObject>("userData")!=null)
                {                    
                    var name = json_object.Value<JObject>("userData").Value<String>("name");
                    if (name == this.user_name)
                    {
                        VerifyState = "Login Success";
                        return;
                    }
                }
            }
            Console.WriteLine("Login Fail");
            throw new ArgumentOutOfRangeException("Login Not Success");
        }
    }
}
