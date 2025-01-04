using Microsoft.AspNetCore.WebUtilities;
using Microsoft.ClearScript.V8;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using PictureSpider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.RegularExpressions;

namespace PictureSpider.Kemono
{
    public partial class Server : BaseServer, IDisposable
    {
        private Database database;
        private HttpClient httpClient;
        //https://kemono.su/api/v1/fanbox/user/7349257/posts-legacy
        private string baseUrl = "https://kemono.su";
        private string baseAPIUrl = "https://kemono.su/api/v1";

        private string download_dir_root = "";
        private string download_dir_tmp = "";
        private string download_dir_fav = "";
        Aria2DownloadQueue downloader;
        private List<Work> downloadQueue = new List<Work>();//计划下载的illustid,线程不安全,只在RunSchedule里使用
        private List<ExternalWork> externalDownloadQueue = new List<ExternalWork>();
        public Server(Config config)
        {
            logPrefix = "K";
            database = new Database(config.KemonoConnectStr);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = 256,
                UseCookies = true,
                Proxy = new WebProxy(config.Proxy, false)
            };
            handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            httpClient = new HttpClient(handler);
            httpClient.Timeout = new TimeSpan(0, 0, 35);
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8,ja;q=0.7");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            //httpClient.DefaultRequestHeaders.Referrer=new Uri("https://kemono.su/fanbox/user/7349257");

            download_dir_root = config.KemonoDownloadDir;
            download_dir_fav = Path.Combine(download_dir_root, "fav");
            download_dir_tmp = Path.Combine(download_dir_root, "tmp");
            downloader = new Aria2DownloadQueue(Aria2DownloadQueue.Downloader.Kemono, config.Proxy, baseUrl);
            foreach (var dir in new List<string> { download_dir_root, download_dir_tmp, download_dir_fav })
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

        }
        public void Dispose()
        {
            httpClient.Dispose();
            database.Dispose();
        }
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
#pragma warning disable CS0162 // 检测到无法访问的代码
        public override async Task Init()
        {
#if DEBUG
            return;
#endif
            //await FetchUser("7349257","fanbox");
            //await FetchWorkGroupListByUser(database.Users.Where(x=>x.id== "7349257").ToList().FirstOrDefault());
            //await FetchUser("3659577", "patreon");
            //await FetchIllustGroupListByUser(database.Users.Where(x=>x.id== "3659577").ToList().FirstOrDefault());
            //await FetchIllustGroup(database.WorkGroups.Where(x=>x.id== "117461502").ToList().First());
            //await FetchUserAndIllustGroups();
            //await AddQueuedUser("115314", "fantia");
            //SyncLocalFile();
            RunSchedule();
            //await ProcessIllustDownloadQueue(downloadQueue);
        }
#pragma warning restore CS0162
#pragma warning restore CS4014
#pragma warning restore CS1998
        //获取作者信息
        private async Task FetchUser(string id,string service)
        {
            /*
             {{
              "id": "7349257",
              "name": "kkkkk20",
              "service": "fanbox",
              "indexed": "2021-12-09T18:24:42.391068",
              "updated": "2024-05-26T07:44:25.932852",
              "public_id": "kkkkk20",
              "relation_id": null
            }}*/
            var doc =await HttpGetJson($"{baseAPIUrl}/{service}/user/{id}/profile");
            if(doc is null||(!doc.ContainsKey("name"))||(!doc.ContainsKey("public_id")))//只会fetch已关注的作者，不应出现失败
            {
                LogError($"Can't Fetch User {service}/{id}");
                return;
            }
            var user = database.Users.Where(x => x.id == id).ToList().FirstOrDefault();
            if (user is null)
            {
                user = new User { id = id, service = service };
                database.Users.Add(user);
                //该Server没有推荐/搜索，user都是通过chrome插件或直接操作数据库加入，先有id才会fetch，所以一定存在于数据库中
#if !DEBUG
                throw new TopLevelException("Why?");
#endif
            }
            user.displayId = doc.Value<string>("name");
            user.displayText = doc.Value<string>("public_id");
            database.SaveChanges();
        }
        //获取该user的作品id并插入数据库
        public async Task FetchWorkGroupListByUser(User user)
        {
            /*
             * {
                "props": {
                    "currentPage": "posts",
                    "id": "7349257",
                    "service": "fanbox",
                    "name": "kkkkk20",
                    "count": 58,
                    "limit": 50,
                    "artist": {
                        "id": "7349257",
                        "name": "kkkkk20",
                        "service": "fanbox",
                        "indexed": "2021-12-09T18:24:42.391068",
                        "updated": "2024-05-26T07:44:25.932852",
                        "public_id": "kkkkk20",
                        "relation_id": null
                    },
                    "display_data": {
                        "service": "Pixiv Fanbox",
                        "href": "https://www.pixiv.net/fanbox/creator/7349257"
                    },
                    "dm_count": 0,
                    "share_count": 0,
                    "has_links": "0"
                },
                "base": {
                    "service": "fanbox",
                    "artist_id": "7349257"
                },
                "results": [
                    {
                        "id": "7928265",
                        "user": "7349257",
                        "service": "fanbox",
                        "title": "玩弄",
                        "substring": "",
                        "published": "2024-05-15T10:03:59",
                        "file": {
                            "name": "DH057ezDhsJktxnJgm0fjQRN.jpeg",
                            "path": "/82/80/8280dffe6057e14100d2a302cbff7b76428cbc124cdafd9afc6c903c89960538.jpg"
                        },
                        "attachments": [
                            {
                                "name": "qytY0nn7ubMkRgIdAtPX0SGt.jpeg",
                                "path": "/62/8d/628d86bd6cde8148943a81e5230825f0b9f339d42541a48371efbc58cc5787c5.jpg"
                            }
                        ]
                    },
                        //中略
                        {
                        "id": "3944351",
                        "user": "7349257",
                        "service": "fanbox",
                        "title": "01-12-22",
                        "substring": "",
                        "published": "2022-06-05T14:38:02",
                        "file": {
                            "name": "xGPJ39yPVV4CCt1lwkVVJVKN.jpeg",
                            "path": "/2d/a7/2da78096621e89e17fd27c70945cde7ff9a73d4d0c3175c1a35c3a9940739622.jpg"
                        },
                        "attachments": [
                            {
                                "name": "01-12-22玩弄赤炼-致幻香料.zip",
                                "path": "/20/5f/205fafece522b3af8aa879bc23acaf9cb32d0c1c911a15d7ad2cc436010e2475.zip"
                            }
                        ]
                    },
                ],
                "result_previews": [
                    [
                        {
                            "type": "thumbnail",
                            "server": "https://n2.kemono.su",
                            "name": "DH057ezDhsJktxnJgm0fjQRN.jpeg",
                            "path": "/82/80/8280dffe6057e14100d2a302cbff7b76428cbc124cdafd9afc6c903c89960538.jpg"
                        },
                        {
                            "type": "thumbnail",
                            "name": "qytY0nn7ubMkRgIdAtPX0SGt.jpeg",
                            "path": "/62/8d/628d86bd6cde8148943a81e5230825f0b9f339d42541a48371efbc58cc5787c5.jpg",
                            "server": "https://n4.kemono.su"
                        }
                    ]
                ],
                //非图片附件会在这里再出现一遍，多了server值,图片附件不会出现
                "result_attachments": [
                    [],
                    //中略
                    [
                        {
                            "path": "/20/5f/205fafece522b3af8aa879bc23acaf9cb32d0c1c911a15d7ad2cc436010e2475.zip",
                            "name": "01-12-22玩弄赤炼-致幻香料.zip",
                            "server": "https://n2.kemono.su"
                        }
                    ],
                ],
                //根据file(实际是封面)决定，和附件无关？
                "result_is_image": [
                    true,
                    true,
                ],
                "disable_service_icons": true
            }
             */
            //默认是按时间倒序
            database.LoadFK(user);
            var existedWorkGroupIds = new HashSet<string>();
            if (user.workGroups is not null)//减少查询次数
                existedWorkGroupIds = user.workGroups.Select(x => x.id).ToHashSet();
            int offset = 0;
            int totalCount = 0;
            string service = user.service;
            DateTime latest= DateTime.MinValue;
            do
            {
                var doc = await HttpGetJson($"{baseAPIUrl}/{user.service}/user/{user.id}/posts-legacy?o={offset}");
                if(doc is null||(!doc.ContainsKey("props"))||(!doc.ContainsKey("results")))
                {
                    LogError($"Can't Fetch posts of {user.service}/{user.id}>>{offset}");
                    break;
                }
                if (offset == 0)
                    totalCount=doc["props"].Value<int>("count");
                int step= doc["props"].Value<int>("limit");
                if (step <= 0) throw new TopLevelException("?");
                offset += step;
                foreach(var obj in doc["results"])
                {
                    string id = obj.Value<string>("id");
                    DateTime date = obj.Value<DateTime>("published");
                    latest = date > latest ? date : latest;
                    WorkGroup workGroup;
                    if (!existedWorkGroupIds.Contains(id))//只录入新增的group,不考虑更新
                    {
                        workGroup = database.WorkGroups.Add(new WorkGroup { id=id,user=user}).Entity;
                        workGroup.title = obj.Value<string>("title");
                        if (obj["file"].ToObject<JObject>().ContainsKey("path"))
                            workGroup.cover =await TryAddWork(obj["file"], service);
                        int index = 1;
                        //work可能重复，例 https://kemono.su/patreon/user/3659577/post/109256192包含了两张一样的图片
                        foreach (var attachment in obj["attachments"])
                        {
                            var work = await TryAddWork(attachment, service);//如果work已在别的group或该group之前的附件中存在，则忽略
                            if (work is null)
                                continue;
                            work.index = index++;
                            work.workGroup = workGroup;
                        }
                    }
                    else if (date<user.fetchedTime)
                    {
                        totalCount = -1;
                        break;
                    }
                }
                foreach (var arr in doc["result_attachments"])//非图片的附件会在result_attachments中指定server(但似乎不管用哪个域名都会重定向到正确的server)
                    foreach (var attachment in arr)
                        await UpdateWork(attachment, service);
            }
            while (offset < totalCount);
            await database.SaveChangesAsync();
            user.fetchedTime = latest;
            await database.SaveChangesAsync();
        }
        //如果不存在则添加，存在则返回null
        public async Task<Work> TryAddWork(JToken token, string service)
        {
            string path = token.Value<string>("path");
            if (database.Works.Count(x => x.urlPath == path && x.service == service) > 0)
                return null;
            var ret = database.Works.Add(new Work { urlPath = path, service = service }).Entity;
            if (token.ToObject<JObject>().ContainsKey("server"))
                ret.urlHost = token.Value<string>("server");
            ret.name = token.Value<string>("name");
            await database.SaveChangesAsync();
            return ret;
        }
        //如果存在则更新server和name，否则什么都不做
        public async Task UpdateWork(JToken token, string service, bool without_save = true)
        {
            string path = token.Value<string>("path");
            Work ret = database.Works.Where(x => x.urlPath == path && x.service == service).ToList().FirstOrDefault();
            if (ret is null)
                return;
            if (token.ToObject<JObject>().ContainsKey("server"))
                ret.urlHost = token.Value<string>("server");
            ret.name = token.Value<string>("name");
            if (!without_save)
                await database.SaveChangesAsync();
        }

        public async Task<JObject> HttpGetJson(string url)
        {
            var r = await HttpGet(url);
            if (r != null)
                return (JObject)JsonConvert.DeserializeObject(r);
            return null;
        }
        public override BaseUser GetUserById(string id)
        {
            return database.Users.Where(x => x.id == id).FirstOrDefault();
        }
        public override void SetReaded(ExplorerFileBase file)
        {
            (file as ExplorerFile).illustGroup.readed = file.readed;
            database.SaveChanges();
        }
        public override void SetBookmarked(ExplorerFileBase file)
        {
            (file as ExplorerFile).illustGroup.fav = file.bookmarked;
            database.SaveChanges();
        }
        public async Task<string> HttpGet(string url)
        {
            for (int try_ct = 8; try_ct >= 0; --try_ct)
            {
                try
                {
                    if (string.IsNullOrEmpty(url))
                        throw new ArgumentNullException("url");
                    if (!url.StartsWith("https"))
                        throw new ArgumentException("Not SSL");
                    using (HttpResponseMessage response = await httpClient.GetAsync(url))
                    {
                        //未知错误
                        CheckStatusCode(response);
                        //正常
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                catch (Exception e)
                {
                    string msg = e.Message;//e.InnerException.InnerException.Message;
                    if (try_ct < 1)
                        LogError(msg + "Re Try " + try_ct.ToString() + " On :" + url);
                    //if (try_ct == 0)
                    //throw;
                }
            }
            return null;
        }
        //获取illustGroup的content以获取外链
        private async Task FetchWorkGroup(WorkGroup illustGroup)
        {
            /*
             {
            "post": {
                "id": "3944351",
                "user": "7349257",
                "service": "fanbox",
                "title": "01-12-22",
                "content": "",
                "embed": {},
                "shared_file": false,
                "added": "2024-05-17T15:53:55.003267",
                "published": "2022-06-05T14:38:02",
                "edited": "2022-09-04T13:34:53",
                "file": {
                    "name": "xGPJ39yPVV4CCt1lwkVVJVKN.jpeg",
                    "path": "/2d/a7/2da78096621e89e17fd27c70945cde7ff9a73d4d0c3175c1a35c3a9940739622.jpg"
                },
                "attachments": [
                    {
                        "name": "01-12-22玩弄赤炼-致幻香料.zip",
                        "path": "/20/5f/205fafece522b3af8aa879bc23acaf9cb32d0c1c911a15d7ad2cc436010e2475.zip"
                    }
                ],
                "poll": null,
                "captions": null,
                "tags": null,
                "next": "3809815",
                "prev": "3944360"
            },
            "attachments": [
                {
                    "server": "https://n2.kemono.su",
                    "name": "01-12-22玩弄赤炼-致幻香料.zip",
                    "extension": ".zip",
                    "name_extension": ".zip",
                    "stem": "205fafece522b3af8aa879bc23acaf9cb32d0c1c911a15d7ad2cc436010e2475",
                    "path": "/20/5f/205fafece522b3af8aa879bc23acaf9cb32d0c1c911a15d7ad2cc436010e2475.zip"
                }
            ],
            "previews": [
                {
                    "type": "thumbnail",
                    "server": "https://n4.kemono.su",
                    "name": "xGPJ39yPVV4CCt1lwkVVJVKN.jpeg",
                    "path": "/2d/a7/2da78096621e89e17fd27c70945cde7ff9a73d4d0c3175c1a35c3a9940739622.jpg"
                }
            ],
            "videos": [],
            "props": {
                "flagged": 0,
            }
        }*/
            database.LoadFK(illustGroup);
            var doc = await HttpGetJson($"{baseAPIUrl}/{illustGroup.service}/user/{illustGroup.user.id}/post/{illustGroup.id}");
            if (doc is null || !doc.ContainsKey("post"))
            {
                Log($"Can't Fetch IllustGroup :{illustGroup.id} {illustGroup.service}");
                return;
            }
            illustGroup.fetched = true;
            illustGroup.desc=doc["post"].Value<string>("content");
            if (doc["post"]["embed"].ToObject<JObject>().ContainsKey("url"))
                illustGroup.embedUrl = doc["post"]["embed"].Value<string>("url");
            await database.SaveChangesAsync();
            Log($"Fetch IllustGroup Done:{illustGroup.id} {illustGroup.title}");
        }
        public override void SetUserFollowOrQueue(BaseUser user)
        {
            database.SaveChanges();
        }
        public override void SetBookmarkEach(ExplorerFileBase file)
        {
            database.SaveChanges();
        }
        public override bool ListenerUtil_IsValidUrl(string url)
        {
            if (url.StartsWith("https://kemono.su"))
                return true;
            return false;
        }
        static public void CheckStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new Exception("HTTP Not Success");
        }

        //下载(加入队列)应当下载的图片，将收藏的作品加入fav文件夹，从fav中删除多余的文件,从tmp中删除已读
        private void SyncLocalFile()
        {
            //下载
            {
                var illustGroups = (from illustGroup in database.WorkGroups
                                    where illustGroup.fetched == true
                                           && (illustGroup.fav || !illustGroup.readed)
                                           && (illustGroup.user.followed == true || illustGroup.user.queued == true)
                                    select illustGroup).ToList();
                var tmp = downloadQueue.Count;
                foreach (var illustGroup in illustGroups)//如果收藏或未读的作品
                {
                    database.LoadFK(illustGroup);
                    if(illustGroup.user.dowloadWorks)
                        foreach (var illust in illustGroup.works)
                        {
                            database.LoadFK(illust);
                            if (illustGroup.fav == false || illust.excluded == false)//没有排除
                                if (!downloadQueue.Contains(illust)) //不在下载队列
                                    if (!File.Exists($"{download_dir_tmp}/{illust.subPath}")) //不在本地
                                        downloadQueue.Add(illust);
                        } 
                    else if(illustGroup.user.dowloadExternalWorks)
                    {
                        //TODO
                    }
                }
                if (downloadQueue.Count > tmp)
                    Log($"Update Download Queue {tmp}=>{downloadQueue.Count}");
            }
            //整理Fav文件夹
            {
                //.ToList()以释放数据库连接
                var existedFiles = Directory.GetFiles(download_dir_fav).Select(x => Path.GetFileName(x)).ToHashSet<string>();
                var illustGroups = (from illustGroup in database.WorkGroups
                                    where illustGroup.fav
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    database.LoadFK(illustGroup);
                    foreach (var illust in illustGroup.works)
                    {
                        var file_name = $"{illust.name}";
                        var tmp_path = $"{download_dir_tmp}/{illust.subPath}";
                        var fav_path = $"{download_dir_fav}/{illust.subPath}";
                        if (!illust.excluded)
                        {
                            if (existedFiles.Contains(file_name))//从existedFiles中移除
                                existedFiles.Remove(file_name);
                            else
                            {
                                if (!Directory.Exists(Path.GetDirectoryName(fav_path)))
                                    Directory.CreateDirectory(Path.GetDirectoryName(fav_path));
                                CopyFile(tmp_path, fav_path);
                            }
                        }
                    }
                }
                foreach (var file in existedFiles)//剩下的都是不需要的文件
                    DeleteFile($"{download_dir_fav}/{file}");
            }
            //清理tmp文件夹
            {
                int ct = 0;
                var illustGroups = (from illustGroup in database.WorkGroups
                                    where illustGroup.readed && illustGroup.fetched && !illustGroup.fav
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    database.LoadFK(illustGroup);
                    foreach (var illust in illustGroup.works)
                        ct += DeleteFile($"{download_dir_tmp}/{illust.subPath}");
                }
                if (ct > 0)
                    Log($"Delete from tmp:{ct}");
            }
        }
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        public async override Task<List<ExplorerQueue>> GetExplorerQueues()
        {
            var ret = new List<ExplorerQueue>();
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Fav, "0", "Kemono-Fav"));
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Main, "0", "Kemono-Main"));
            foreach (var user in database.Users.Where(x => x.queued).ToList())
                ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.User, $"{user.service}/{user.id}", user.displayId));
            return ret;
        }
#pragma warning restore CS1998
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        public async override Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue)
#pragma warning restore CS1998
        {
            var result = new List<ExplorerFileBase>();
            if (queue.type == ExplorerQueue.QueueType.Main)
            {
                var illustGroups = (from illustGroup in database.WorkGroups
                                    where illustGroup.fetched && illustGroup.readed == false && illustGroup.fav == false
                                       && illustGroup.user.followed
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    database.LoadFK(illustGroup);
                    var exploreFile = new ExplorerFile(illustGroup, download_dir_tmp);
                    result.Add(exploreFile);
                }
            }
            else if (queue.type == ExplorerQueue.QueueType.Fav)
            {
                var illustGroups = (from illustGroup in database.WorkGroups
                                    where illustGroup.fetched && illustGroup.fav
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    database.LoadFK(illustGroup);
                    var exploreFile = new ExplorerFile(illustGroup, download_dir_tmp);
                    result.Add(exploreFile);
                }
            }
            else
            {
                string id_text = queue.id;
                string service = id_text.Substring(0, id_text.IndexOf('/'));
                string id = id_text.Substring(id_text.IndexOf('/') + 1);
                var illustGroups = (from illustGroup in database.WorkGroups
                                    where illustGroup.fetched && illustGroup.readed == false
                                       && illustGroup.user.id == id && illustGroup.user.service == service
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    database.LoadFK(illustGroup);
                    if (illustGroup.user.dowloadWorks&&illustGroup.works.Count>0)
                        result.Add(new ExplorerFile(illustGroup, download_dir_tmp));
                    else if (illustGroup.user.dowloadExternalWorks&&illustGroup.externalWorks.Count>0)
                        result.Add(new ExplorerExternalFile(illustGroup, download_dir_tmp));
                }
            }
            result.Sort((l, r) => (l as ExplorerFile).illustGroup.title.CompareTo((r as ExplorerFile).illustGroup.title));
            return result;
        }

        //获取作品列表
        private async Task FetchUserAndIllustGroups()
        {
            //注意linq语句产生的Iqueryable不是立即返回，而是一直占用连接,此期间无法进行其它查询
            //加上ToList令查询完成后再执行循环
            //获取follow/queue作者的作品
            foreach (var user in (from user in database.Users
                                  where user.followed == true || user.queued == true
                                  select user).ToList())
                await FetchWorkGroupListByUser(user);
            Log("Fetch User Done");
            foreach (var illustGroup in (from illustGroup in database.WorkGroups
                                         where illustGroup.fetched == false && (illustGroup.user.followed == true || illustGroup.user.queued == true)
                                         select illustGroup).ToList())
                await FetchWorkGroup(illustGroup);
            Log("Fetch Groups Done");
        }
        public async Task AddQueuedUser(string id, string service)
        {
            User user = database.Users.Where(x => x.id == id && x.service == service).ToList().FirstOrDefault();
            if (user is null)
            {
                user = new User { id = id, service = service };
                user.displayText = user.displayId = $"{id}";
                database.Users.Add(user);
            }
            if (user.followed == false && user.queued == false)
                user.queued = true;
            await database.SaveChangesAsync();
        }
        private async Task RunSchedule()
        {
            //和pixiv不同，请求次数很少，除了下载图片不需要使用队列
            //由于hitomi不提供浏览收藏等数据，通过tag或搜索获得的作品良莠不齐，因此只做关注作者相关功能，不做随机浏览队列
            int last_daily_task = DateTime.Now.Day;
            var day_of_week = DateTime.Now.DayOfWeek;
            SyncLocalFile();
            do
            {
                if (DateTime.Now.Day != last_daily_task)//每日一次
                {
                    last_daily_task = DateTime.Now.Day;
                    await FetchUserAndIllustGroups();
                    SyncLocalFile();
                    if (day_of_week == DayOfWeek.Sunday) //每周一次
                    {
                        foreach (var user in database.Users.ToList())//更新作者
                            await FetchUser(user.id, user.service);
                    }
                }
                //同时下载太多503
                await ProcessIllustDownloadQueue(downloadQueue, 40);
                await Task.Delay(new TimeSpan(0, 30, 0));
            }
            while (true);
        }
        public override async Task<bool> ListenerUtil_FollowUser(string url)
        {
            var regex = new Regex("https://kemono.su/([a-z]+)/user/([^/]+)(/.*)?$");
            var results = regex.Match(url).Groups;
            if (results.Count > 1)
            {
                var service = results[1].Value;
                var id = results[2].Value;
                await AddQueuedUser(id, service);
                return true;
            }
            return false;
        }
        private async Task ProcessIllustDownloadQueue(List<Work> workList, int limit = -1)
        {
            try
            {
                //移除临时文件
                foreach (var file in Directory.GetFiles(download_dir_tmp, "*.aria2"))//下载临时文件
                    File.Delete(file);
                var download_illusts = new List<Work>();
                var ignore_illusts = new List<Work>();
                int download_ct = 0;
                foreach (var illust in workList)
                {
                    var path = Path.Combine(download_dir_tmp, illust.subPath);
                    var dir = Path.GetDirectoryName(path).Replace('\\','/');
                    var filename = Path.GetFileName(path);
                    var ext = Path.GetExtension(filename).ToLower();
                    if(IsImage(ext))
                    {
                        if (!Directory.Exists(dir))
                        {
                            try
                            {
                                Directory.CreateDirectory(dir);
                            }
                            catch(Exception e)
                            {
                                Console.WriteLine(e.Message);
                            }

                        }
                        await downloader.Add(illust.url, dir, filename);
                    }
                    else if (IsZip(ext))
                    {
                        ignore_illusts.Add(illust);//暂定：直接忽略压缩包
                    }
                    else
                    {
                        ignore_illusts.Add(illust);
                    }
                    
                    download_ct++;
                    download_illusts.Add(illust);
                    if (limit >= 0 && download_ct >= limit)
                        break;
                }
                foreach (var illust in ignore_illusts)
                    workList.Remove(illust);
                ignore_illusts.Clear();

                //等待完成并查询状态
                await downloader.WaitForAll();
                //检查结果，以本地文件为准，无视aria2和函数的返回
                {
                    int success_ct = 0;
                    //var fail_illustGroup=new HashSet<WorkGroup>();
                    foreach (var illust in download_illusts)
                    {
                        var path = $"{download_dir_tmp}/{illust.subPath}";
                        if (File.Exists(path + ".aria2") || !File.Exists(path))//存在.aria2说明下载未完成
                        {
//                            Log($"Download Fail: {illust.url}");
                            workList.Remove(illust);//移到队末并重置url
                            workList.Add(illust);                            
                            //fail_illustGroup.Add(illust.workGroup);
                            //throw new Exception("debug");
                        }
                        else
                        {
                            success_ct++;
                            workList.Remove(illust);
                            //转换格式
                        }
                    }
                    Log($"Process Download Queue: {success_ct}/{download_illusts.Count} Success, {downloadQueue.Count} Left.");
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                throw;
            }
        }
        private bool IsImage(string ext)
        {
            return ext == ".jpeg" || ext == ".jpg" || ext == ".png" || ext == ".gif" || ext == ".webp";
        }
        private bool IsZip(string ext)
        {
            return ext == ".zip" || ext == ".rar" || ext == ".7z" ;
        }
        private async Task ProcessExternalIllustDownloadQueue(List<ExternalWork> illustList, int limit = -1)
        {
            return ;
        }

    }
}
