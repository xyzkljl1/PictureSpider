using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SQLite;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PixivAss.Data;
using IDManLib;
using System.Threading;

namespace PixivAss
{
    class Client
    {
        private int bookmarkLimit=1800;
        private string user_id;
        private string base_url;
        private string base_host;
        private string user_name;
        private string formal_public_dir;
        private string formal_private_dir;
        private string tmp_dir;
        private CookieServer cookie_server;
        private PixivAss.Database database;
        private ICIDMLinkTransmitter2 idm;
        private HttpClient httpClient;
        public Client()
        {
            idm = new CIDMLinkTransmitter();
            formal_public_dir = "E:/p/pub";
            formal_private_dir = "E:/p/private";
            tmp_dir = "E:/p/tmp";
            database = new Database("root","pixivAss","pass");
            user_id = "16428599";
            user_name = "xyzkljl1";
            base_url = "https://www.pixiv.net/";
            base_host = "www.pixiv.net";
            cookie_server = new CookieServer();

            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler()
            {
                UseCookies = false,
                Proxy = new WebProxy(string.Format("{0}:{1}", "127.0.01", 1081), false)
            };
            handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            httpClient = new HttpClient(handler);
            httpClient.Timeout = new TimeSpan(0, 1, 0);
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.100 Safari/537.36");
            httpClient.DefaultRequestHeaders.Host = base_host;
            httpClient.DefaultRequestHeaders.Add("Cookie", this.cookie_server.cookie);
            httpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
            httpClient.DefaultRequestHeaders.Add("sec-fetch-site", "none");
            httpClient.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
        }
        public void CheckStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new Exception("HTTP Not Success");
        }
        public string Test()
        {
            //ICIDMLinkTransmitter2 idm=new CIDMLinkTransmitter();
            //idm.SendLinkToIDM("https://www.dlsite.com/maniax/work/=/product_id/RJ273091.html","","","","","", "E:/MyWebsiteHelper","1.html",0);
            //DownloadBookmarkPrivate();
            //RequestSearchResult("染みパン", false);
            //CheckHomePage();
            FetchAllKnownIllust();
            //DownloadAllIlust();
            //FetchBookMarkIllust(true);
            //FetchBookMarkIllust(false);
            //FetchAllUser();
            //FetchIllustByList(new List<string>{ "76278759"});
            //RequestIllustAsync("47974548");
            return "12s3";
        }

        /*
         * Request:Query from remote and return result
         * Fetch:Request and Save to Local
         * Get:Find in Local,if not exists ,Fetch then find.
         */
        //同步所有图片到本地，如有必要则进行下载/删除
        //要求Illust信息都已更新过
        public void DownloadAllIlust()
        {
            int ct = 0;
            var illustList = database.GetAllIllustFull(false);
            foreach (var illust in illustList)
            {
                string dir = GetDownloadDir(illust);
                for (int i = 0; i < illust.pageCount; ++i)
                {
                    string url = String.Format(illust.urlFormat, i);
                    string file_name = GetDownloadFileName(illust, i);
                    bool exist = File.Exists(dir + "/" + file_name);
                    if (exist==false&&GetShouldDownload(illust,i))
                        ct += DownloadIllustForce(illust.id, url,dir,file_name) ? 1 : 0;
                    else if(exist&&GetShouldDelete(illust,i))
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
            Console.WriteLine("Downloaded "+ ct);
        }
        //更新所有已知的作品状态
        public void FetchAllKnownIllust()
        {
            FetchIllustByList(database.GetAllIllustId());
        }

        public async Task<Illust> RequestIllustAsync(string illustId)
        {
            string url = String.Format("{0}ajax/illust/{1}", base_url, illustId);
            string referer = String.Format("{0}member_illust.php?mode=medium&illust_id={1}", base_url, user_id);
            JObject json =await RequestJsonAsync(url, referer).ConfigureAwait(false);
            if (!json.HasValues)
                return new Illust(illustId,false);
            if (json.Value<Boolean>("error"))
                return new Illust(illustId, false);
            return new Illust(json.Value<JObject>("body"));
        }
        //获取并更新该作者的所有作品
        public void FetchAllByUserId(string userId)
        {
            string url = String.Format("{0}ajax/user/{1}/profile/all", base_url, userId);
            string referer = String.Format("{0}member_illust.php?id={1}", base_url, user_id);
            JObject ret = RequestJsonAsync(url, referer).Result;
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get All By User Fail");
            var idList = new List<string>();
            foreach (var illust in ret.GetValue("body").Value<JObject>("illusts"))
                idList.Add(illust.Key.ToString());
            FetchIllustByList(idList);
        }
        //更新指定的作品
        public void FetchIllustByList(List<string> illustIdList)
        {
            //illustIdList = new List<string> { "47974548" };
            var task_list = new List<Task<Illust>>();
            foreach (var illustId in illustIdList)
            {

                task_list.Add(RequestIllustAsync(illustId));
            }

            Task.WaitAll(task_list.ToArray(),1000*60*60*2);
            var illustList = new List<Illust>();
            int ct = 0;
            foreach (var task in task_list)
            {
                illustList.Add(task.Result);
                if (task.Status==TaskStatus.RanToCompletion)
                    ct++;
            }
            Console.WriteLine(ct);
            //database.UpdateIllustAllCol(illustList);
        }
        //获取并更新所有收藏作品
        public void FetchBookMarkIllust(bool pub)
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
                JObject ret = RequestJsonAsync(url, referer).Result;
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
            foreach (var illust_id in database.GetBookmarkIllustId(pub))
                if (!idList.Contains(illust_id))
                    idList.Add(illust_id);
            FetchIllustByList(idList);
            Console.Write(String.Format("Fetch {0}/{1} {2} Bookmarks",pub?"Public":"Private",tmp,total));
        }

        public async Task<User> RequestUserAsync(string userId)
        {
            string url = String.Format("{0}ajax/user/{1}/profile/top", base_url, userId);
            string referer = String.Format("{0}member_illust.php?id={1}", base_url, user_id);
            JObject ret =await RequestJsonAsync(url, referer).ConfigureAwait(false);
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get User Fail");
            var userName = ret.GetValue("body").Value<JObject>("extraData").Value<JObject>("meta").Value<string>("title");
            if (ret.GetValue("body").Value<JObject>("illusts").Count > 0)
                foreach(var illustId in ret.GetValue("body").Value<JObject>("illusts"))
                {
                    var illust = await RequestIllustAsync(illustId.Key);
                    userName = illust.userName;
                    break;
                }
            return new User(userId,userName ,false);
        }
        public int RequestFollowedUserCount()
        {
            string url = String.Format("{0}ajax/user/{1}/following?offset=0&limit=1&rest=show", base_url, user_id);
            string referer = String.Format("{0}bookmark.php?id={1}&rest=show", base_url, user_id);
            JObject ret = RequestJsonAsync(url, referer).Result;
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get Bookmark Fail");
            return ret.GetValue("body").Value<int>("total");
        }
        //获取并更新关注的作者状态
        public void FetchAllFollowedUser()
        {
            int total = RequestFollowedUserCount();
            var userList = new List<User>();
            for (int i = 0; i < total; i += 100)
            {
                string url = String.Format("{0}ajax/user/{1}/following?offset={2}&limit=100&rest=show", base_url, user_id,i);
                string referer = String.Format("{0}bookmark.php?id={1}&rest=show", base_url, user_id);
                JObject ret = RequestJsonAsync(url, referer).Result;
                if (ret.Value<Boolean>("error"))
                    throw new Exception("Get Bookmark Fail");
                foreach (var user in ret.GetValue("body").Value<JArray>("users"))
                    userList.Add(new User(user.ToObject<JObject>()));
            }
            database.UpdateFollowedUser(userList);
            Console.Write("Fetch " + userList.Count.ToString() + " followings");
        }
        //更新所有已知的未关注作者状态
        public void FetchAllKnownUnfollowedUserName()
        {
            var user_list = database.GetUser(false, true);
            var task_list = new List<Task<User>>();
            foreach (var user in user_list)
                task_list.Add(RequestUserAsync(user.userId));
            Task.WaitAll(task_list.ToArray());

            user_list.Clear();
            foreach (var task in task_list)
                user_list.Add(task.Result);
            database.UpdateUserName(user_list);
        }
        public void FetchAllUser()
        {
            FetchAllFollowedUser();
            FetchAllKnownUnfollowedUserName();
        }

        public async Task<List<string>> RequestSearchPage(string word,int page,bool text_mode)
        {
            string url = String.Format("{0}search.php?{1}word={2}&order=popular_d&p={3}",base_url,
                                    text_mode? "s_mode=s_tc&":"", word, page);
            string referer = String.Format("{0}search.php?{1}word={2}&order=popular_d&p={3}", base_url,
                                    text_mode ? "s_mode=s_tc&" : "", word, page>1?page-1:2);
            HtmlDocument doc = await RequestHtmlAsync(url, referer).ConfigureAwait(false);
            var node = doc.DocumentNode.SelectSingleNode("//input[@id='js-mount-point-search-result-list']");            
            var ret= new List<string>();
            JArray list_json=(JArray)JsonConvert.DeserializeObject(
                                                System.Web.HttpUtility.HtmlDecode(
                                                    node.GetAttributeValue("data-items", "[]")));
            foreach(var ill in list_json)
            {
                var id=ill.ToObject<JObject>().Value<string>("illustId");
                var bookmarkCount= ill.ToObject<JObject>().Value<int>("bookmarkCount");
                if (bookmarkCount > bookmarkLimit)
                    ret.Add(id);
            }
            return ret;
        }
        public void RequestSearchResult(string word,bool text_mode)
        {
            var ret = new List<string>();
            int start_page = 0;
            word=System.Web.HttpUtility.UrlEncode(word);
            while (start_page>=0)
            {
                var task_list = new List<Task<List<string>>>();
                for(int i=0;i<10;++i)
                    task_list.Add(RequestSearchPage(word,start_page+i, text_mode));
                Task.WaitAll(task_list.ToArray());
                start_page += 10;
                foreach (var task in task_list)
                {
                    var page = task.Result;
                    if (page.Count == 0)
                        start_page = -1;
                    ret.AddRange(page);
                }
            }
            Console.WriteLine(ret.ToString());
        }

        public void CheckHomePage()
        {
            string url = base_url;
            string referer = String.Format("{0}search.php?word=%E5%85%A8%E8%A3%B8&s_mode=s_tag_full&order=popular_d&p=1",base_url);
            var doc = RequestHtmlAsync(base_url,referer).Result;
            HtmlNode headNode = doc.DocumentNode.SelectSingleNode("//a[@class='user-name js-click-trackable-later']");
            if (headNode != null)
                if (headNode.InnerText == this.user_name)
                    return;
            throw new Exception("Login Not Success");
        }
        public async Task<string> RequestAsync(string url,Uri referer)
        {
            int try_ct = 5;
            while(true)
            {
                try
                {
                    Console.WriteLine("Begin " + try_ct.ToString() + " " + url);
                    if (string.IsNullOrEmpty(url))
                        throw new ArgumentNullException("url");
                    if (!url.StartsWith("https"))
                        throw new ArgumentException("Not SSL");
                    httpClient.DefaultRequestHeaders.Referrer = referer;
                    HttpResponseMessage response = await httpClient.GetAsync(url).ConfigureAwait(false);
                    //作品已删除
                    if (response.StatusCode == HttpStatusCode.NotFound)
                        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    //未知错误
                    CheckStatusCode(response);
                    //正常
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    string msg = e.Message;//e.InnerException.InnerException.Message;
                    Console.WriteLine(msg+"Re Try "+try_ct.ToString()+" On :"+url);
                    if (try_ct==0)
                        throw;
                    try_ct--;
                }
            }       
        }
        public async Task<JObject> RequestJsonAsync(string url,string referer)
        {
            JObject jsonobj = (JObject)JsonConvert.DeserializeObject(await RequestAsync(url, new Uri(referer)).ConfigureAwait(false));
            return jsonobj;
        }
        public async Task<HtmlDocument> RequestHtmlAsync(string url, string referer)
        {
            var doc = new HtmlDocument();
            var ret = await RequestAsync(url, new Uri(referer)).ConfigureAwait(false);
            doc.LoadHtml(ret);
            return doc;
        }
        //将指定图片下载到本地
        //如已存在则先删除
        public bool DownloadIllustForce(string id,string url, string dir,string file_name)
        {
            string path = dir + "/" + file_name;
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
                Console.WriteLine("Begin:" + file_name);
                string referer = String.Format("{0}member_illust.php?mode=medium&illust_id={1}", base_url, id);
                //referer = String.Format("https://www.pixiv.net/artworks/{0}",id);
                //0x01:不确认，0x02:稍后下载
                idm.SendLinkToIDM(url,referer,cookie_server.cookie,"","","", dir, file_name, 0x01);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        //必须在GetShouldDownload返回false的情况下使用
        public bool GetShouldDelete(Illust illust, int page)
        {
            if (illust.bookmarked)//已收藏作品里只有不喜欢和已删除的图不需要下载
                return illust.valid;//如果是不喜欢的则删掉，否则留着
            else if (illust.readed)//已看过且未收藏的作品无论是哪种都可以删
                return true;
            return false;//未读作品留着
        }

        public bool GetShouldDownload(Illust illust, int page)
        {
            if (!illust.valid)
                return false;
            if(illust.bookmarked)
            {
                //illust.bookmarkEach
                return true;
            }
            if (illust.readed)
                return false;
            return true;
        }

        public string GetDownloadFileName(Illust illust, int page)
        {
            string ext = "";
            int pos = illust.urlFormat.LastIndexOf(".");
            if (pos >= 0)
                ext = illust.urlFormat.Substring(pos + 1);
            return String.Format("{0}_p{1}.{2}", illust.id, page, ext);
        }

        public string GetDownloadDir(Illust illust)
        {
            if (illust.bookmarked && illust.bookmarkPrivate)
                return this.formal_private_dir;
            else if (illust.bookmarked && !illust.bookmarkPrivate)
                return this.formal_public_dir;
            return this.tmp_dir;
        }
    }
}
