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
using PictureSpider;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using MoreLinq;
using Windows.Web.Http;
using HttpClient = System.Net.Http.HttpClient;
using HttpResponseMessage = System.Net.Http.HttpResponseMessage;
using System.Text.RegularExpressions;
namespace PictureSpider.Pixiv
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    partial class Server : BaseServer, IBindHandleProvider, IDisposable
    {
        public BindHandleProvider provider { get; set; } = new BindHandleProvider();
        public delegate void Delegate_V_B();
        public string VerifyState { get => verify_state;
            set
            {
                verify_state = value;
                this.NotifyChangeEx<string>();
            }
        }
        private float SEARCH_PAGE_SIZE = 60;//不知如何获得，暂用常数表示;为计算方便使用float
        private int UPDATE_INTERVAL = 7 * 100;//更新数据库间隔，上次更新时间距今小于该值的illust不会被更新
        private string verify_state = "Waiting";
        private string download_dir_root;
        private string user_id;
        private string base_url = "https://www.pixiv.net/";
        private string base_host = "www.pixiv.net";
        private string user_name;
        private string download_dir_bookmark_pub;
        private string download_dir_bookmark_private;
        public string download_dir_main;
        private string download_dir_ugoira_tmp;
        public string special_dir;
        public Database database;
        private HttpClient httpClient;
        private HttpClient httpClient_anonymous;//不需要登陆的地方使用不带cookie的客户端，以防被网站警告
        private HttpClient httpClientCSRF;//用于获取csrf的client
        private HashSet<string> banned_keyword;
        Aria2DownloadQueue downloader;
        private string request_proxy;

        private HashSet<int> illust_fetch_queue = new HashSet<int>();//计划更新的illustid,线程不安全,只在RunSchedule里使用
        private HashSet<int> illust_download_queue = new HashSet<int>();//计划下载的illustid,线程不安全,只在RunSchedule里使用
        //public event PropertyChangedEventHandler PropertyChanged = delegate { };

        private Dictionary<int,string> illust_debug_msg= new Dictionary<int,string>();//only for debug

        /*
         * 代理：
         * 既可以使用常规代理，也可以使用绕过SNI的方法"直连"以节约梯子流量
         * 
         * 目前Pixiv的ip并未被屏蔽，只有DNS污染和SNI检测
         * SNI是在连接前明文发送要连接的域名，如果被检测到就会被TCP RST：参考 https://gulut.github.io/gulut-blog/post1/2020/05/31/2020-05-31-by-pass-the-gfw-by-sni/
         * 绕过SNI有若干方法，例如通过Nginx在本地反代，由于nginx的反代不支持SNI就不会发送： https://github.com/mashirozx/Pixiv-Nginx (此时需要将pixiv的ip解析到127.0.0.1或连接时指定ip以使用反代)
         * 其它方法参考 https://github.com/SeaHOH/GotoX https://github.com/bypass-GFW-SNI/main https://github.com/bypass-GFW-SNI/proxy
         * 目前使用的是 https://github.com/URenko/Accesser 在本地的代理,端口号1200(在Accesser目录下config.toml配置)
         */
        public Server(Config config)
        {
            base.tripleBookmarkState = true;
            base.logPrefix = "P";

            download_dir_root = config.PixivDownloadDir;
            download_dir_bookmark_pub = Path.Combine(download_dir_root, "pub");
            download_dir_bookmark_private = Path.Combine(download_dir_root, "private");
            download_dir_main = Path.Combine(download_dir_root, "tmp");
            download_dir_ugoira_tmp = Path.Combine(download_dir_root, "ugoira_tmp");
            special_dir = Path.Combine(download_dir_root, "special");
            foreach (var dir in new List<string> { download_dir_root, download_dir_bookmark_pub, download_dir_bookmark_private, download_dir_main, download_dir_ugoira_tmp, special_dir })
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

            database = new Database(config.PixivConnectStr);
            //request_proxy = config.Proxy;
            request_proxy = config.ProxySNI;
            user_id = config.PixivUserId;
            user_name = config.PixivUserName;
            Log("Use SNI Proxy:"+request_proxy);
            downloader = new Aria2DownloadQueue(Aria2DownloadQueue.Downloader.Pixiv, request_proxy, "https://www.pixiv.net/");
            //初始化httpClient
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = 256,
                UseCookies = false,
                Proxy = new WebProxy(request_proxy, false)
            };
            handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            {
                httpClient = new HttpClient(handler);
                //超时必须设短一些，因为有的时候某个请求就是会得不到回应，需要让它尽快超时重来
                httpClient.Timeout = new TimeSpan(0, 0, 35);
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                httpClient.DefaultRequestHeaders.Host = base_host;
                //httpClient.DefaultRequestHeaders.Add("Cookie", this.cookie_server.cookie);
                //            httpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
                //            httpClient.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
                //            httpClient.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
                httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                // httpClient.DefaultRequestHeaders.Add("x-csrf-token", this.cookie_server.csrf_token);
            }
            {
                httpClient_anonymous = new HttpClient(handler);
                httpClient_anonymous.Timeout = new TimeSpan(0, 0, 35);
                httpClient_anonymous.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
                httpClient_anonymous.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
                httpClient_anonymous.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                httpClient_anonymous.DefaultRequestHeaders.Host = base_host;
                httpClient_anonymous.DefaultRequestHeaders.Add("Connection", "keep-alive");
            }
            {
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var handlerCSRF = new HttpClientHandler()
                {
                    MaxConnectionsPerServer = 256,
                    UseCookies = false,
                    Proxy = new WebProxy(request_proxy, false)
                };
                handlerCSRF.ServerCertificateCustomValidationCallback = delegate { return true; };
                httpClientCSRF = new HttpClient(handlerCSRF);
                httpClientCSRF.Timeout = new TimeSpan(0, 0, 35);
                httpClientCSRF.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
                httpClientCSRF.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
                httpClientCSRF.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.100 Safari/537.36");
                httpClientCSRF.DefaultRequestHeaders.Host = "www.pixiv.net";
                //httpClientCSRF.DefaultRequestHeaders.Add("Cookie", this.cookie);
                httpClientCSRF.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
                httpClientCSRF.DefaultRequestHeaders.Add("sec-fetch-site", "none");
                httpClientCSRF.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
            }
        }
#pragma warning disable CS0162 // 检测到无法访问的代码
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        public override async Task Init()
        {
            banned_keyword = await database.GetBannedKeyword();
#if DEBUG
            await Test();
            return;
#endif
            //设置cookie和csrftoken
            await UpdateHttpClientByDatabaseCookie();
            //会修改属性引发UI更新，需要从主线程调用或使用invoke
            await CheckHomePage();
            RunSchedule();
        }
        public async Task<string> Test()
        {
            //var x = await RequestIllustAsync(74802304);
            //await UpdateHttpClientByDatabaseCookie();
            //var list=await RequestAllByUserId(20446187);
            //await AddToIllustFetchQueue(list.ToHashSet(), new Dictionary<int, int>());
            //await ProcessIllustFetchQueue(100);
            //var res = await RequestIllustAsync(125036771);
            return "";
        }
#pragma warning restore CS4014
#pragma warning restore CS0162
#pragma warning restore CS1998
        public void Dispose()
        {
            httpClient.Dispose();
        }

        public override async Task<List<ExplorerQueue>> GetExplorerQueues()
        {
            var ret = new List<ExplorerQueue>();
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Fav, "0", "Fav"));
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.FavR, "0", "FavR"));
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Main, "0", "Main"));
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.MainR, "0", "MainR"));
            foreach (var user in await database.GetQueuedUser())
                ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.User, user.userId.ToString(), user.userName));
            return ret;
        }
        public override async Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue)
        {
            var illusts = new List<Illust>();
            if (queue.type == ExplorerQueue.QueueType.Main || queue.type == ExplorerQueue.QueueType.MainR)
            {
                bool is_private = queue.type == ExplorerQueue.QueueType.MainR;
                var id_list = (await database.GetQueue()).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .ToList<string>()
                            .Select<string, int>(x => Int32.Parse(x))
                            .ToList<int>();
                foreach (var illust in await database.GetIllustFull(id_list))
                    if ((illust.xRestrict > 0) == is_private && !illust.readed && !illust.bookmarked)
                        illusts.Add(illust);
            }
            else if (queue.type == ExplorerQueue.QueueType.Fav || queue.type == ExplorerQueue.QueueType.FavR)
            {
                bool is_private = queue.type == ExplorerQueue.QueueType.FavR;
                foreach(var illust in await database.GetIllustFull(await database.GetBookmarkIllustId(!is_private)))
                    illusts.Add(illust);
            }
            else
            {
                int id = int.Parse(queue.id);
                foreach (var illust in await database.GetIllustFullSortedByUser(id))
                    if (illust.bookmarked || !illust.readed)
                        illusts.Add(illust);
            }
            var result = new List<ExplorerFileBase>();
            foreach(var illust in illusts)
            {
                if(illust_debug_msg.ContainsKey(illust.id))//debug code
                    illust.debugMsg= illust_debug_msg[illust.id];
                result.Add(new ExplorerFile(illust, download_dir_main));
                //已删除(valid=false)的图也加入队列，以防止valid=0&readed=0且没有下载的图越来越多每次都要检查
                /*
                if (illust.valid)
                    result.Add(new ExplorerFile(illust, download_dir_main));
                else //如果Illust已被删除，检测本地是否有图，如果本地至少有一张图也加入result
                {
                    for (int i = 0; i < illust.pageCount; i++)
                        if (File.Exists(String.Format("{0}/{1}", download_dir_main, illust.storeFileName(i))))
                        {
                            result.Add(new ExplorerFile(illust, download_dir_main));
                            break;
                        }
                }*/
            }
            return result;
        }
        public override void SetReaded(ExplorerFileBase file)//基类中定义的属性在基类中取，未定义的在illust中取
        {
            var illust = (file as ExplorerFile).illust;
            database.UpdateIllustReadedSync(illust.id);
        }
        public override void SetBookmarked(ExplorerFileBase file)
        {
            var illust = (file as ExplorerFile).illust;
            database.UpdateIllustBookmarkedSync(illust.id,file.bookmarked,file.bookmarkPrivate);
        }
        public override void SetBookmarkEach(ExplorerFileBase file)
        {
            var illust = (file as ExplorerFile).illust;
            database.UpdateIllustBookmarkEachSync(illust.id,illust.bookmarkEach);
        }
        public override BaseUser GetUserById(string id)
        {
            int user_id = 0;
            if(int.TryParse(id, out user_id))
                return database.GetUserByIdSync(user_id);
            return null;
        }
        public override void SetUserFollowOrQueue(BaseUser user)
        {
            database.UpdateUserSync(user as User);
        }
        public override Dictionary<string, TagStatus> GetAllTagsStatus() { return database.GetAllTagsStatusSync(); }
        public override Dictionary<string, string> GetAllTagsDesc() { return database.GetAllTagsDescSync(); }
        public override void UpdateTagStatus(string tag, TagStatus status) { database.UpdateTagStatusSync(tag, status); }
        /*Query开头的函数供UI从主线程调用,此处应该只进行数据库操作从而避免线程安全问题*/

        private async Task RunSchedule()
        {
            int last_daily_task = DateTime.Now.Day-1;
            int process_speed = 50;
            var day_of_week = DateTime.Now.DayOfWeek;
            await DownloadIllustsInExplorerQueue();
            foreach (var id in await database.GetAllIllustId("where readed=0"))
                illust_download_queue.Add(id);
            do
            {
                if(DateTime.Now.Day!=last_daily_task)
                {
                    last_daily_task = DateTime.Now.Day;
                    await DailyTask(day_of_week);
                }
                //每小时处理下载和更新队列
                await ProcessIllustFetchQueue(process_speed);
                await ProcessIllustDownloadQueue(process_speed);
                if (illust_fetch_queue.Count / process_speed > 24 * 7 * 2)//积压量大于一周时逐渐加速
                    process_speed++;
                else if (illust_fetch_queue.Count / process_speed < 24 * 2 &&process_speed>140)//积压量小于一天时逐渐减速
                    process_speed--;
                await Task.Delay(new TimeSpan(0, 30, 0));//每隔半小时执行一次
            }
            while (true);
        }
        private async Task DailyTask(DayOfWeek day_of_week)
        {
            Log("Start Fetch Task ");
            //第一次运行之后，FollowedUser和BookmarkIllust由本地向远程单向更新
            var illust_list_bytime = new HashSet<int>();//根据时间更新的列表
            var illust_list_bylike = new Dictionary<int,int>();//根据like数更新的列表
            bool do_week_task = DateTime.Now.DayOfWeek == day_of_week;//每周一次

            /*获得需要更新的Illust的id*/
            if (do_week_task)
                //更新关注和入列作者的作品
                illust_list_bytime.UnionWith(await RequestAllQueuedAndFollowedUserIllust());
            //更新1/700的数据库，每两年更新一轮（实际因为总数在增长，两年不能更新一轮)
            illust_list_bytime.UnionWith(await database.GetIllustIdByUpdateTime(DateTime.UtcNow.AddDays(-UPDATE_INTERVAL), 1.0f / UPDATE_INTERVAL));
            //关键字搜索。分散成若干块进行
            illust_list_bylike.Union(await RequestAllKeywordSearchIllustBlock((DateTime.Now-new DateTime(2000,1,1)).Days));
            //排行榜
            illust_list_bytime.UnionWith(await RequestAllCurrentRankIllust());
            Log($"Got {illust_list_bytime.Count}+{illust_list_bylike.Count} illusts:");

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
            Log("All Fetch Task Done");
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
            Log("Fetch Task Done");
        }
        private async Task GenerateExplorerQueue(bool force=false)
        {
            const int UpdateInterval = 7;//单位:天，每超过这个时间才刷新,
            const int MaxSize = 10000;
            if (force||(await database.GetQueueUpdateInterval())>UpdateInterval||(await database.GetQueue()).Length<2)
            {
                var list_nonprivate = new List<Illust>();
                var list_private = new List<Illust>();

                foreach (var illust in (await database.GetAllUnreadedIllustFull()).FindAll((Illust illust) =>
                                      {
                                          foreach (var tag in illust.tags)
                                              if (this.banned_keyword.Contains(tag.ToString()))
                                                  return false;
                                          return true;
                                      }))
                    if (illust.xRestrict > 0)
                        list_private.Add(illust);
                    else
                        list_nonprivate.Add(illust);
                var followed_user = new HashSet<int>();
                foreach (var user in await database.GetFollowedUser())
                    followed_user.Add(user.userId);
                var followed_tags=await database.GetFollowedTagsOrdered();

                var tmp = new List<List<Illust>> {list_private, list_nonprivate };
                for(int i=0;i<2;++i)
                {
                    int FOLLOWED_USER_MAGIC_NUMBER = 1000000;//关注作者对illust分数的加算加成
                    var illust_list = tmp[i];
                    int follow_ct = 0;
                    //关注作者最优先
                    //收藏、点赞高的优先，浏览量低的优先，时间近的优先
                    foreach (var illust in illust_list)
                    {
                        illust.score = 0;
                        if (followed_user.Contains(illust.userId))
                        {
                            follow_ct++;
                            illust.score += FOLLOWED_USER_MAGIC_NUMBER;
                            illust.debugMsg += "Followed ";
                        }
                        int years = (int)(DateTime.Now - illust.uploadDate).TotalDays / 365;
                        //暂定：浏览量是后加入数据库的，很多图片尚未获取为默认值0，为了让已获取浏览量的图片排在前面，统一将旧图片按200k浏览量计算
                        var viewCount=illust.viewCount==0?200000:illust.viewCount;
                        illust.score += (int)(((illust.bookmarkCount + illust.likeCount / 2)-viewCount/50)*((20-years)/10.0));
                        illust.debugMsg += $"{illust.score}={illust.bookmarkCount}+{illust.likeCount}-{viewCount}*{years};  ";
                    }
                    illust_list.Sort((l, r) => r.score.CompareTo(l.score));
                    /* 小众标签补偿，防止浏览人数少的题材永远不会上队列
                     * 根据每张图所具有的最弱势已关注标签对分数进行乘算加成，标准是每个标签下非关注作者的第一名至少能超过无补偿队列中非关注作者的第500名
                    */
                    if(illust_list.Count>1000&&illust_list.Count> follow_ct+501)
                    {
                        int baseline = illust_list[follow_ct+500].score;
                        var addition_in_tag = new Dictionary<string, float>();
                        foreach (var tag in followed_tags)
                            addition_in_tag.Add(tag, 1.0f);
                        //先用addition_in_tag记下每个标签的最高分
                        foreach (var illust in illust_list)
                            if (illust.score< FOLLOWED_USER_MAGIC_NUMBER)
                                foreach (var tag in illust.tags)
                                    if (followed_tags.Contains(tag))
                                        if (addition_in_tag[tag] < illust.score)
                                            addition_in_tag[tag] = illust.score;
                        foreach (var tag in followed_tags)
                            addition_in_tag[tag] =Math.Max((float)baseline/ addition_in_tag[tag],1.0f);
                        foreach (var illust in illust_list)
                            if (illust.score < FOLLOWED_USER_MAGIC_NUMBER)
                            {
                                float max_addup = 1.0f;
                                string max_tag="";
                                foreach (var tag in illust.tags)
                                    if (followed_tags.Contains(tag))
                                        if(addition_in_tag[tag]>max_addup)
                                        {
                                            max_tag = tag;
                                            max_addup = addition_in_tag[tag];
                                        }
                                /*
                                 * 补偿系数逐渐衰减，防止很多低分数图跟着鸡犬升天
                                 */
                                if(max_tag!="")
                                    addition_in_tag[max_tag] = (addition_in_tag[max_tag] - 1.0f) * 0.9f + 1.0f;
                                illust.debugMsg = $"{(int)((float)illust.score * max_addup)}:{illust.score}*{max_addup};  {illust.debugMsg}";
                                illust.score = (int)((float)illust.score*max_addup);
                            }
                        //重新排序
                        illust_list.Sort((l, r) => r.score.CompareTo(l.score));
                    }
                    /*
                     * 不能用illust_list = illust_list.Take<Illust>(Math.Min(MaxSize, illust_list.Count)).ToList<Illust>();
                     * 这样不会修改list_nonprivate/list_private的值
                    */
                }
                //合并成一个字符串,因为没有混用的场景，混合时不需要排序
                string queue = "";
                foreach (var illust in list_nonprivate.Take<Illust>(Math.Min(MaxSize, list_nonprivate.Count)))
                    queue += " " + illust.id;
                foreach (var illust in list_private.Take<Illust>(Math.Min(MaxSize, list_private.Count)))
                    queue += " " + illust.id;
                //debug code
                {
                    illust_debug_msg.Clear();
                    foreach (var illust in list_nonprivate.Take<Illust>(Math.Min(MaxSize, list_nonprivate.Count)))
                        illust_debug_msg.Add(illust.id,illust.debugMsg);
                    foreach (var illust in list_private.Take<Illust>(Math.Min(MaxSize, list_private.Count)))
                        illust_debug_msg.Add(illust.id, illust.debugMsg);
                }
                await database.UpdateQueue(queue);
                Log("Generate Queue Done");
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
                //移除临时文件
                foreach (var file in Directory.GetFiles(download_dir_main, "*.aria2"))//下载临时文件
                    File.Delete(file);
                foreach (var file in Directory.GetFiles(download_dir_main, "*.zip"))//动图临时文件
                    File.Delete(file);
                //创建下载任务，打乱顺序以防止反复下载尚未fetch成功的illust
                List<Illust> illustList = await database.GetIllustFull(id_list.ToList().Shuffle().ToList());
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
                            //用queue是为了并发发送任务节约时间
                            await queue.Add(downloader.Add(illust.URL(i), download_dir_main, illust.downloadFileName(i)));
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
                Log(String.Format("Download Begin {0}(pages)", ct));
                //等待完成并查询状态
                await downloader.WaitForAll();

                //将动图转为GIF
                {
                    var ugorias = new HashSet<Illust>();
                    foreach (var illust in download_illusts)
                        if (illust.isUgoira())
                        {
                            bool fail = false;
                            for (int i = 0; i < illust.pageCount; ++i)
                            {
                                var path = download_dir_main + "/" + illust.downloadFileName(i);
                                if (File.Exists(path+".aria2")|| !File.Exists(path))//存在.aria2说明下载未完成
                                    fail = true;
                            }
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
                        {
                            var path = download_dir_main + "/" + illust.storeFileName(i);
                            if (File.Exists(path + ".aria2") || !File.Exists(path))//存在.aria2说明下载未完成
                                fail = true;
                        }
                        if (fail)//重新fetch
                        {
                            fail_illusts.Add(illust.id);
                            Log($"Download Fail，Try ReFetch {illust.id}");
                        }
                        else
                        {
                            success_ct++;
                            processed_illusts.Add(illust.id);
                        }
                    }
                    Log(String.Format("Download Done, {0}/{1}(illusts) Success", success_ct, download_illusts.Count));
                    //无法下载可能是因为已删除或已更新，尝试重新Fetch
                    await AddToIllustFetchQueue(fail_illusts, null, false);
                }
                return processed_illusts;
            }
            catch (Exception e)
            {
                LogError(e.Message);
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
                await queue.Add(Task.Run(()=>UgoiraToGIF(illust)));
            await queue.Done();
            int ct = 0;
            queue.done_task_list.ForEach(task => ct += task.Result?1:0);
            Log(String.Format("Ugoira2GIF {0} Done in {1}s",ct, DateTime.Now.Subtract(t).TotalSeconds));
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
                Directory.Delete(tmp_dir,true);
                return true;
            }
            catch (Exception e)
            {
                LogError(String.Format("Ugoira cast Fail {0}", illust.id));
                LogError(e.Message);
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
                        string dest = Path.Combine(dir, file_name);
                        string tmp = Path.Combine(dir, "_tmp");
                        string src = Path.Combine(download_dir_main, file_name);
                        if (!File.Exists(dest) && File.Exists(src))
                        {
                            try
                            {
                                File.Copy(src, tmp, true);
                                File.Move(tmp, dest);
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
                    //出于某种神秘原因，p站对长关键字的容忍大幅降低了，1600->500，保险起见使用300长度
                    //超出长度时正常返回但是不包含任何illust
                    if (tmp.Length > 500)
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
            int step = 10;
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
                        Log("Keyword(Tag) " + key.Substring(0,20)+" Done");
                    }
                //合并结果
                foreach (var task in task_list.Values)
                    foreach (var id in task.Result)
                        if(!ret.Contains(id))
                            ret.Add(id);
                task_list.Clear();
                Log(String.Format("Search Res {0}p: {1}",start_page,ret.Count));
                start_page += step;
            }
            Log(String.Format("Search Done:{0} in {1} pages ",ret.Count,page_count));
            return ret;
        }
        private async Task<HashSet<int>> RequestAllQueuedAndFollowedUserIllust()
        {
            var queue = new TaskQueue<List<int>>(10);
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
            //这里要登录后查询，有的用户不登陆看不到作品如25877697
            JObject ret = await RequestJsonAsync(url, referer,false);
            if (ret.Value<Boolean>("error"))
            {
                //throw new Exception("Get All By User Fail " + userId + " " + ret.Value<string>("message"));
                Log("Get All By User Fail " + userId + " " + ret.Value<string>("message"));
                return new List<int>();
            }
            var idList = new List<int>();
            foreach (var type in new List<string>{ "illusts","manga"})
                if (ret.Value<JObject>("body").GetValue(type).Type==JTokenType.Object)//为空时不是object而是array很坑爹
                    foreach (var illust in ret.Value<JObject>("body").Value<JObject>(type))
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
            Log($"Update Illusts Queue {tmp}+{illust_fetch_queue.Count - tmp} ");
        }
        private async Task ProcessIllustFetchQueue(int count)
        {
            int tmp = illust_fetch_queue.Count;
            var illustList = new List<Illust>();
            /*
            {

                var queue = new TaskQueue<Illust>(15);
                foreach (var id in illust_fetch_queue.ToList().Take(Math.Min(count, illust_fetch_queue.Count)))
                    await queue.Add(RequestIllustAsync(id));
                await queue.Done();

                queue.done_task_list.ForEach(task =>
                {
                    if (task.Result != null)
                        illustList.Add(task.Result);
                });
            }
            */
            {//似乎p站对频繁illust请求的容忍度大幅降低了？被迫改为非并发
                foreach (var id in illust_fetch_queue.ToList().Take(Math.Min(count, illust_fetch_queue.Count)))
                {
                    var res = await RequestIllustAsync(id);
                    if(res != null)
                        illustList.Add(res);
                }
            }
            database.UpdateIllustOriginalData(illustList);

            illustList.ForEach(illust=>illust_fetch_queue.Remove(illust.id));
            Log($"Process Fetch Queue => {tmp}-{tmp - illust_fetch_queue.Count}");
        }
        private async Task ProcessIllustDownloadQueue(int count)
        {
            int tmp = illust_download_queue.Count;
            var ret=await DownloadIllusts(illust_download_queue, count);
            ret.ForEach(id => illust_download_queue.Remove(id));
            Log($"Process Download Queue => {tmp}-{tmp - illust_download_queue.Count}");
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
            Log(String.Format("Fetch {0}/{1}  Bookmarks",pub?"Public":"Private",tmp));
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
            Log("Fetch " + userList.Count.ToString() + " followings");
        }
        //更新所有已知的未关注作者状态
        private async Task FetchAllUnfollowedUserStatus()
        {
            //每隔70天更新全部，没有名字的立刻更新
            var user_list = await database.GetUnFollowedUserNeedUpdate(DateTime.Now.AddDays(-7 * 10));
            var queue = new TaskQueue<User>(1500);
            foreach (var user in user_list)
                await queue.Add(RequestUserAsync(user.userId));
            await queue.Done();
            user_list.Clear();
            foreach (var task in queue.done_task_list)
                if(task.Result!=null)
                    user_list.Add(task.Result);
            database.UpdateUserName(user_list);
            Log("Done");
        }

        //确认是否成功登录
        private async Task CheckHomePage()
        {
            await UpdateHttpClientByDatabaseCookie();
            VerifyState = "Checking";
            string url = base_url;
            string referer = String.Format("{0}", base_url);
            //如果程序关闭期间，cookie改变，刚启动时来不及获得新cookie，就会反复登录失败重启
            //因此登录失败时需要延时等待
            //这是chrome的cookie文件被锁住导致每次网页上都要重新登录产生的问题，由于pixiv正常会一直记住登陆状态，chrome那边解决后应该不会再出现了
            for (int i= 0;i<3;++i)
            {
                var doc = await RequestHtmlAsync(base_url, referer);
                if (doc != null)
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
                VerifyState = "Login Retrying";
                LogError("Login Retry");
                await Task.Delay(1000 * 60 * 10);
            }
            VerifyState = "Login Fail";
            LogError("Login Fail");
            throw new TopLevelException("Login Not Success");
        }
        /*
         * CSRFToken是和cookie中的phpsessionid一一对应的token，随机生成并随登陆表单提交
         * 部分操作如收藏作品必须在请求头附带登录时所用的CSRFToken，该token会在部分网页作为不显示的元素出现
         * x-csrf-token/post_key/tt都可指代CSRFToken
         */
        private async Task FetchCSRFToken()
        {
            httpClientCSRF.DefaultRequestHeaders.Add("Cookie",await database.GetCookie());
            //id为1的作品的编辑收藏页面，这个作品存不存在/是否已加入收藏不影响，设置语言表单里会带token
            var url = "https://www.pixiv.net/bookmark_add.php?type=illust&illust_id=1";
            var csrf_token = "";
            for (int try_ct = 5; try_ct >= 0; --try_ct)
                try
                {
                    using (HttpResponseMessage response = await httpClientCSRF.GetAsync(url))
                    //if(response.StatusCode==HttpStatusCode.OK)
                    {
                        var ret = await response.Content.ReadAsStringAsync();
                        var doc = new HtmlDocument();
                        doc.LoadHtml(ret);
                        HtmlNode headNode = doc.DocumentNode.SelectSingleNode("//input[@name='tt']");
                        if (headNode != null)
                        {
                            csrf_token = headNode.Attributes["value"].Value;
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message + "Re Try " + try_ct.ToString() + " On :" + url);
                    if (try_ct == 0)
                        throw;
                }
            await database.UpdateCSRFToken(csrf_token);
        }
        public override async Task ListenerUtil_SetCookie(string cookie)
        {
            //获取cookie和csrftoken
            var old_cookie = await database.GetCookie();
            if (cookie!=old_cookie)
            {
                await database.UpdateCookie(cookie);
                await UpdateHttpClientByDatabaseCookie();
            }
        }
        public override bool ListenerUtil_IsValidUrl(string url)
        {
            if (url.StartsWith("https://www.pixiv.net"))
                return true;
            return false;
        }
        public override async Task<bool> ListenerUtil_FollowUser(string url)
        {
            if (url.StartsWith("https://www.pixiv.net/artworks/"))
            {
                var regex = new Regex("https://www.pixiv.net/artworks/([0-9]+)");
                var id = Int32.Parse(regex.Match(url).Groups[1].Value);
                var illust = await RequestIllustAsync(id);
                if(illust is not null&&illust.userId!=0)
                    return AddQueuedUser(illust.userId);
            }
            else if (url.StartsWith("https://www.pixiv.net/users/"))
            {
                var regex = new Regex("https://www.pixiv.net/users/([0-9]+)");
                var results = regex.Match(url).Groups;
                if(results.Count > 1)
                {
                    var id = Int32.Parse(results[1].Value);
                    return AddQueuedUser(id);
                }
            }
            return false;
        }
        public bool AddQueuedUser(int id)
        {
            var user = database.GetUserByIdSync(id);
            if (user is null)
                user = new User(id, "", false, false);
            else if (user.followed || user.queued)//已经关注过视作成功
                return true;
            user.queued = true;//默认标记为queued
            database.UpdateUserSync(user);
            return true;
        }
        public async Task UpdateHttpClientByDatabaseCookie()
        {
            var cookie = await database.GetCookie();
            if (!string.IsNullOrEmpty(cookie))
            {
                var csrf_token = await database.GetCSRFToken();
                if (string.IsNullOrEmpty(csrf_token) && !string.IsNullOrEmpty(cookie))
                    await FetchCSRFToken();
                httpClient.DefaultRequestHeaders.Add("Cookie", cookie);
                httpClient.DefaultRequestHeaders.Add("x-csrf-token", await database.GetCSRFToken());
            }
        }
    }
}
