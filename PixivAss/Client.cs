using System;
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
using System.ComponentModel;

namespace PixivAss
{
    partial class Client:IDisposable,INotifyPropertyChanged
    {
        public string VerifyState { get=>verify_state;
            set
            {
                verify_state = value;
                PropertyChanged(this, new PropertyChangedEventArgs("VerifyState"));
            }
        }
        private string verify_state="Waiting";
        private const string download_dir="E:/p/";
        private string user_id;
        private string base_url;
        private string base_host;
        private string user_name;
        private string formal_public_dir;
        private string formal_private_dir;
        private string tmp_dir;
        public string special_dir;
        private CookieServer cookie_server;
        public  Database database;
        private HttpClient httpClient;
        private HashSet<string> banned_keyword;

        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public Client()
        {
            formal_public_dir = download_dir+"pub";
            formal_private_dir = download_dir+"private";
            tmp_dir = download_dir+"tmp";
            special_dir = download_dir +"special";

            database = new Database("root","pixivAss","pass");
            user_id = "16428599";
            user_name = "xyzkljl1";
            base_url = "https://www.pixiv.net/";
            base_host = "www.pixiv.net";
            cookie_server = new CookieServer(database);

            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler()
                                        {
                                            MaxConnectionsPerServer = 256,
                                            UseCookies = false,
                                            Proxy = new WebProxy(string.Format("{0}:{1}", "127.0.0.1", 1081), false)
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
            httpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
            httpClient.DefaultRequestHeaders.Add("sec-fetch-site", "none");
            httpClient.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            Task.Run(CheckHomePage);
            banned_keyword =database.GetBannedKeyword().GetAwaiter().GetResult();
        }
        public void Dispose()
        {
            httpClient.Dispose();
        }
        public async Task<string> Test()
        {
            //DownloadBookmarkPrivate();
            Task.Run(()=> { this.DownloadAllIlust(); });
            //await FetchAllKeywordSearchResult();
            //await FetchAllFollowedUserIllust();
            //await FetchAllKnownIllust();
            //DownloadAllIlust();
            //FetchBookMarkIllust(true);
            //FetchBookMarkIllust(false);
            //FetchAllUser();
            //FetchIllustByList(new List<string>{ "76278759"});
            //RequestIllustAsync("44302315");
            return "12s3";
        }

        /*
         * Request:从远端查询并返回结果
         * Fetch:从远端查询并存储到数据库
         */
        //同步所有图片到本地，如有必要则进行下载/删除
        //要求Illust信息都已更新过
        public void DownloadAllIlust()
        {
            /*IDM真是一言难尽,可能是因为任务要排序/一个个添加到表格里，还全阻塞主线程，导致任务一多就各种卡
             * 界面卡，SendLinkToIDM也卡，把任务输出成文件再导入也会卡，一卡几十分钟
             * api又没有清除已完成任务的功能，所以创建任务时必须关掉界面
             * 然后创建任务期间会停掉下载，内存也会疯涨
             * 只能分几次下载了，每下完一波清任务
            */
            var idm = new CIDMLinkTransmitter();
            int ct = 0;
            var illustList = database.GetAllIllustFull();
            foreach (var illust in illustList)
            {
                string dir = GetDownloadDir(illust);
                for (int i = 0; i < illust.pageCount; ++i)
                {
                    string url = String.Format(illust.urlFormat, i);
                    string file_name = GetDownloadFileName(illust, i);
                    bool exist = File.Exists(dir + "/" + file_name);
                    if (GetShouldDownload(illust, i))
                    {
                        if (!exist)
                            ct += DownloadIllustForce(idm,illust.id, url, dir, file_name) ? 1 : 0;
                    }
                    else if (exist && GetShouldDelete(illust, i))
                        File.Delete(dir + "/" + file_name);
                }
            }
        /*
             * 仔细想想似乎并没有监视的必要
            FileSystemWatcher watcher=new FileSystemWatcher();
            watcher.Path = path;
            watcher.IncludeSubdirectories = false;
            watcher.EnableRaisingEvents = true;
            */
            Console.WriteLine(String.Format("Downloaded {0}", ct));
        }
        //更新所有已知的作品状态
        public async Task FetchAllKnownIllust()
        {
            await FetchIllustByIdList(await database.GetAllIllustId());
        }

        //搜索结果太多,必须分段进行
        public async Task FetchAllKeywordSearchResult()
        {            
            var key_word_list=new List<string>();
            var task_list = new Dictionary<string, Task<List<string>>>();
            var tmp = "";
            //用or合并关键字可以减少重复
            foreach (var word in await database.GetAllKeyword())
            {
                tmp += (tmp.Length > 0 ? "%20OR%20" : "") + System.Web.HttpUtility.UrlEncode(word);
                if (tmp.Length > 1600)
                {
                    key_word_list.Add(tmp);
                    tmp = "";
                }
            }
            int start_page = 700;
            int step = 100;
            var set = new HashSet<string>();
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
                        Console.WriteLine("Keyword " + key.Substring(0,20)+" Done");
                    }
                //合并结果
                foreach (var task in task_list.Values)
                    foreach (var id in task.Result)
                        set.Add(id);
                task_list.Clear();
                if (set.Count > 30000)
                {
                    await FetchIllustByIdSetReduce(set);
                    Console.WriteLine("Update Search Res " + set.Count.ToString());
                    set.Clear();
                }
                else
                    Console.WriteLine("Search Res: " + set.Count.ToString());
                start_page += step;
            }
            if (set.Count > 0)
            {
                await FetchIllustByIdSetReduce(set);
                Console.WriteLine("Update Search Res " + set.Count.ToString());
            }
            Console.WriteLine("Update Search Res  Done");
            return;
        }
        public async Task FetchAllFollowedUserIllust()
        {
            var queue=new TaskQueue<List<string>>(1000);
            foreach(var user in await database.GetUser(true,false))
                await queue.Add(RequestAllByUserId(user.userId));
            await queue.Done();
            var set = new HashSet<string>();
            foreach (var task in queue.done_task_list)
                foreach (var id in task.Result)
                    set.Add(id);
            await FetchIllustByIdSetReduce(set);
        }

        //获取并更新该作者的所有作品
        public async Task<List<string>> RequestAllByUserId(string userId)
        {
            string url = String.Format("{0}ajax/user/{1}/profile/all", base_url, userId);
            string referer = String.Format("{0}member_illust.php?id={1}", base_url, user_id);
            JObject ret =await RequestJsonAsync(url, referer);
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get All By User Fail");
            var idList = new List<string>();
            foreach (var illust in ret.GetValue("body").Value<JObject>("illusts"))
                idList.Add(illust.Key.ToString());
            return idList;
        }
        //先从本地去重再FetchIllustByIdList
        public async Task FetchIllustByIdSetReduce(HashSet<string> id_list)
        {
            var local_illust = new HashSet<string>();
            DateTime timeline = DateTime.UtcNow.AddDays(-7);
            foreach (var illust in database.GetAllIllustFull())
                if (illust.updateTime > timeline)
                    local_illust.Add(illust.id);
            var all_illust = new List<string>();
            foreach (var id in id_list)
                if (!local_illust.Contains(id))
                    all_illust.Add(id);
            await FetchIllustByIdList(all_illust);
        }
        //获取并更新指定的作品
        public async Task FetchIllustByIdList(List<string> illustIdList)
        {
            var queue = new TaskQueue<Illust>(3000);
            foreach (var illustId in illustIdList)
                await queue.Add(RequestIllustAsync(illustId));
            await queue.Done();
            var illustList = new List<Illust>();
            foreach (var task in queue.done_task_list)
                illustList.Add(task.Result);
            database.UpdateIllustOriginalData(illustList);
        }
        //获取并更新所有收藏作品
        public async Task FetchBookMarkIllust(bool pub)
        {
            int total = 0;
            int offset = 0;
            int page_size = 48;
            var idList = new List<string>();
            //查询收藏作品的id
            while (offset==0||offset<total)
            {
                //和获取其它用户的收藏不同，limit不能过高
                string url = String.Format("{0}ajax/user/{1}/illusts/bookmarks?tag=&offset={2}&limit={3}&rest={4}",
                                            base_url, user_id,offset,page_size, pub ? "show" : "hide");
                string referer = String.Format("{0}bookmark.php?id={1}&rest={2}", base_url, user_id, pub ? "show" : "hide");
                JObject ret =await RequestJsonAsync(url, referer);
                if (ret.Value<Boolean>("error"))
                    throw new Exception("Get Bookmark Fail");
                //获取总数,仅用于提示
                //实际总数可能更小,因为删除的作品会被剔掉但是不会从总数中扣除
                //已删除的作品仍然会占位,因此每次获取到的数量可能少于page_size
                if (offset == 0)
                    total = ret.GetValue("body").Value<int>("total");
                //获取该页的id
                foreach (var illust in ret.GetValue("body").Value<JArray>("works"))
                    idList.Add(illust.Value<string>("id"));
                offset += page_size;
            }
            //将当前有效的已收藏作品和可能已无效/已移除收藏的作品(即本地存储的已收藏作品)一起更新状态
            //FetchIllust时会获取Illust是否已收藏状态，所以不需要另行更新收藏状态
            int tmp = idList.Count;
            foreach (var illust_id in await database.GetBookmarkIllustId(pub))
                if (!idList.Contains(illust_id))
                    idList.Add(illust_id);
            await FetchIllustByIdList(idList);
            Console.Write(String.Format("Fetch {0}/{1} {2} Bookmarks",pub?"Public":"Private",tmp,total));
        }
        //获取并更新关注的作者状态
        public async Task FetchFollowedUserList()
        {
            int total = await RequestFollowedUserCount();
            var userList = new List<User>();
            for (int i = 0; i < total; i += 100)
            {
                string url = String.Format("{0}ajax/user/{1}/following?offset={2}&limit=100&rest=show", base_url, user_id,i);
                string referer = String.Format("{0}bookmark.php?id={1}&rest=show", base_url, user_id);
                JObject ret =await RequestJsonAsync(url, referer);
                if (ret.Value<Boolean>("error"))
                    throw new Exception("Get Bookmark Fail");
                foreach (var user in ret.GetValue("body").Value<JArray>("users"))
                    userList.Add(new User(user.ToObject<JObject>()));
            }
            database.UpdateFollowedUser(userList);
            Console.Write("Fetch " + userList.Count.ToString() + " followings");
        }
        //更新所有已知的未关注作者状态
        public async Task FetchAllUnfollowedUserStatus()
        {
            var user_list = await database.GetUser(false, true);
            var task_list = new List<Task<User>>();
            foreach (var user in user_list)
            {
                var task = RequestUserAsync(user.userId);
                task_list.Add(task);
            }
            await Task.WhenAll(task_list.ToArray());
            user_list.Clear();
            foreach (var task in task_list)
                user_list.Add(task.Result);
            database.UpdateUserName(user_list);
        }
        public async Task FetchAllUserStatus()
        {
            await FetchFollowedUserList();
            await FetchAllUnfollowedUserStatus();
        }
        //获取搜索结果
        //!:key_word需要以URL编码
        public async Task<List<string>> RequestSearchResult(string key_word, bool text_mode,int start_page, int end_page)
        {
            var ret = new List<string>();
            try
            {
                var queue = new TaskQueue<List<string>>(25);
                for (int i = start_page; i < end_page; ++i)//页数从1开始，在RequestSearchPage里面加1了
                    await queue.Add(RequestSearchPage(key_word, i, text_mode));
                await queue.Done();
                foreach (var task in queue.done_task_list)
                    ret.AddRange(task.Result);
                Console.WriteLine("Search {0}:{1}_{2}", key_word.Substring(0, 20), ret.Count.ToString(),ret.Count>0?ret[0]:"None");
                return ret;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return ret;
        }

        //确认是否成功登录
        public async Task CheckHomePage()
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
