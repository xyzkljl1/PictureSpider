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
        private string cookie;
        private string user_id;
        private string base_url;
        private string base_host;
        private string user_name;
        private string download_dir;
        private CookieServer cookie_server;
        private PixivAss.Database database;
        private ICIDMLinkTransmitter2 idm;
        //private HttpClient httpClient;
        public Client()
        {
            idm = new CIDMLinkTransmitter();
            download_dir = "E:/p";
            database = new Database("root","pixivAss","pass");
            user_id = "16428599";
            user_name = "xyzkljl1";
            base_url = "https://www.pixiv.net/";
            base_host = "www.pixiv.net";
            cookie = "";
            cookie_server = new CookieServer();
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
            DownloadBookmarkPrivate();
            //RequestSearchResult("染みパン", false);
            //CheckHomePage();
            //FetchBookMarkIllust(true);
            //FetchBookMarkIllust(false);
            //FetchAllUser();
            //FetchIllustByList(new List<string>{ "76278759"});
            return "12s3";
        }

        /*
         * Request:Query from remote and return result
         * Fetch:Request and Save to Local
         * Get:Find in Local,if not exists ,Fetch then find.
         */
        public void DownloadBookmarkPrivate()
        {
            int ct = 0;
            var illustList = database.GetPrivateBookmarkURL();
            foreach (var illust in illustList)
            {
                for (int i = 0; i < illust.pageCount; ++i)
                {
                    string url = String.Format(illust.urlFormat, i);
                    ct += DownloadIllust(illust.id, i, url) ? 1 : 0;
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

        public async Task<Illust> RequestIllustAsync(string illustId)
        {
            string url = String.Format("{0}ajax/illust/{1}", base_url, illustId);
            string referer = String.Format("{0}member_illust.php?mode=medium&illust_id={1}", base_url, user_id);
            JObject json =await RequestJsonAsync(url, referer).ConfigureAwait(false);
            if (json.Value<Boolean>("error"))
                throw new Exception("Get Illust Fail");
            return new Illust(json.Value<JObject>("body"));
        }
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
        public void FetchIllustByList(List<string> illustIdList)
        {
            var task_list = new List<Task<Illust>>();
            foreach (var illustId in illustIdList)
                task_list.Add(RequestIllustAsync(illustId));
            Task.WaitAll(task_list.ToArray());

            var illustList = new List<Illust>();
            foreach (var task in task_list)
                illustList.Add(task.Result);
            database.UpdateIllustLeft(illustList);
        }
        public void FetchBookMarkIllust(bool pub)
        {
            string url = String.Format("{0}ajax/user/{1}/illusts/bookmarks?tag=&offset=0&limit=40000&rest={2}", base_url,user_id,pub?"show":"hide");
            string referer = String.Format("{0}bookmark.php?id={1}&rest={2}", base_url, user_id, pub ? "show" : "hide");
            JObject ret =RequestJsonAsync(url, referer).Result;
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get Bookmark Fail");
            var idList = new List<string>();
            foreach(var illust in ret.GetValue("body").Value<JArray>("works"))
                idList.Add(illust.Value<string>("id"));
            FetchIllustByList(idList);
            Console.Write("Fetch "+ idList.Count.ToString()+" Bookmarks");
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
        public void FetchFollowedUser()
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
        public void FetchUnFollowedUserName()
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
            FetchFollowedUser();
            FetchUnFollowedUserName();
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
            try
            {
                if (string.IsNullOrEmpty(url))
                    throw new ArgumentNullException("url");
                if (!url.StartsWith("https"))
                    throw new ArgumentException("Not SSL");
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var handler = new HttpClientHandler() { UseCookies = false };
                handler.ServerCertificateCustomValidationCallback = delegate { return true; };
                var httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
                httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
                httpClient.DefaultRequestHeaders.Referrer = referer;
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.100 Safari/537.36");
                httpClient.DefaultRequestHeaders.Host = base_host;
                httpClient.DefaultRequestHeaders.Add("Cookie", this.cookie);
                httpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
                httpClient.DefaultRequestHeaders.Add("sec-fetch-site", "none");
                httpClient.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
                HttpResponseMessage response =await httpClient.GetAsync(url).ConfigureAwait(false);
                CheckStatusCode(response);
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                string msg = e.Message;//e.InnerException.InnerException.Message;
                Console.WriteLine(msg);
                throw;
            }
        }
        public async Task RequestToFile(string url, Uri referer,string addr)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                    throw new ArgumentNullException("url");
                if (!url.StartsWith("https"))
                    throw new ArgumentException("Not SSL");
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var handler = new HttpClientHandler() { UseCookies = false };
                handler.ServerCertificateCustomValidationCallback = delegate { return true; };
                var httpClient = new HttpClient(handler);
                httpClient.Timeout=TimeSpan.FromSeconds(30);
                httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
                httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
                httpClient.DefaultRequestHeaders.Referrer = referer;
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/76.0.3809.100 Safari/537.36");
                httpClient.DefaultRequestHeaders.Host = base_host;
                //httpClient.DefaultRequestHeaders.Add("Cookie", this.cookie);
                httpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "navigate");
                httpClient.DefaultRequestHeaders.Add("sec-fetch-site", "none");
                httpClient.DefaultRequestHeaders.Add("sec-fetch-user", "?1");
                httpClient.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
                //httpClient.DefaultRequestHeaders.Add("if-modified-since", "Mon, 29 Jan 2018 18:53:22 GMT");
                httpClient.DefaultRequestHeaders.Add("cache-control", "max-age=0");
                httpClient.DefaultRequestHeaders.Add("upgrade-insecure-requests", "1");
                httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
                Stream inputStream=await httpClient.GetStreamAsync(url).ConfigureAwait(false);
                FileStream file = File.Open(addr, FileMode.Create);
                await inputStream.CopyToAsync(file);
                file.Close();
                Console.WriteLine("Done:" + addr);
                inputStream.Close();
                httpClient.Dispose();
                return;
            }
            catch (Exception e)
            {
                string msg = e.Message;//e.InnerException.InnerException.Message;
                Console.WriteLine(msg);
                throw;
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
        public bool DownloadIllust(string id, int page, string url)
        {
            string file_name = String.Format("{0}_p{1}.png", id, page);
            string path = download_dir + "/" + file_name;
            try
            {
                if (File.Exists(path))
                    return false;
                Console.WriteLine("Begin:" + file_name);
                string referer = String.Format("{0}member_illust.php?mode=medium&illust_id={1}", base_url, id);
                //referer = String.Format("https://www.pixiv.net/artworks/{0}",id);
                //0x01:不确认，0x02:稍后下载
                idm.SendLinkToIDM(url,referer,cookie,"","","", download_dir, file_name, 0x01);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            return true;
        }
    }
}
