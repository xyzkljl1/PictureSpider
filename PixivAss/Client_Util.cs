using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PixivAss.Data;
using IDManLib;

namespace PixivAss
{
    partial class Client
    {
        //public int c = 0;
        /*
         * 下载
         */
        //该函数必须在GetShouldDownload返回false的情况下使用
        static public bool GetShouldDelete(Illust illust, int page)
        {
            if (illust.bookmarked)//已收藏作品里只有不喜欢和已删除的图不需要下载
                return illust.valid;//如果是不喜欢的则删掉，否则留着
            else if (illust.readed)//已看过且未收藏的作品无论是哪种都可以删
                return true;
            return false;//未读作品留着
        }

        static public bool GetShouldDownload(Illust illust, int page)
        {
            if (!illust.valid)
                return false;
            if (illust.bookmarked)
            {
                //illust.bookmarkEach
                return true;
            }
            if (illust.readed)
                return false;
            return true;
        }

        static public string GetDownloadFileName(Illust illust, int page)
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
        //将指定图片下载到本地
        //如已存在则先删除
        public bool DownloadIllustForce(string id, string url, string dir, string file_name)
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
                idm.SendLinkToIDM(url, referer, cookie_server.cookie, "", "", "", dir, file_name, 0x01 | 0x02);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
            return true;
        }
        /*
         * 网络请求
         */
        static public void CheckStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new Exception("HTTP Not Success");
        }
        public async Task<string> RequestAsync(string url, Uri referer)
        {
            int try_ct = 5;
            while (true)
            {
                try
                {
                    //Console.WriteLine("Begin " + try_ct.ToString() + " " + (url.Length>150?url.Substring(0, 150) :url));
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
                    /*
                        HttpWebRequest http = WebRequest.CreateHttp(url);
                        http.Method = "GET";
                        http.KeepAlive = true;
                        http.Proxy = new WebProxy(string.Format("{0}:{1}", "127.0.0.1", 8000), false);
                        http.Referer = referer.ToString();
                        http.Headers["Cookie"] = this.cookie_server.cookie;
                        var response = await http.GetResponseAsync().ConfigureAwait(false);
                        //await Task.Delay(10 * 1000);
                        //return "{\"error\":true}";
                        StreamReader reader = new StreamReader(response.GetResponseStream());
                        return await reader.ReadToEndAsync().ConfigureAwait(false);
                    */
                }
                //用HttpWebRequest时404会抛出异常
                //catch(System.Net.WebException )
                //{
                //     return "{\"error\":true}";
                // }
                catch (Exception e)
                {
                    string msg = e.Message;//e.InnerException.InnerException.Message;
                    Console.WriteLine(msg + "Re Try " + try_ct.ToString() + " On :" + url);
                    if (try_ct == 0)
                        throw;
                    try_ct--;
                }
            }
        }
        public async Task<JObject> RequestJsonAsync(string url, string referer)
        {
            return (JObject)JsonConvert.DeserializeObject(await RequestAsync(url, new Uri(referer)).ConfigureAwait(false));
        }
        public async Task<HtmlDocument> RequestHtmlAsync(string url, string referer)
        {
            var doc = new HtmlDocument();
            var ret = await RequestAsync(url, new Uri(referer)).ConfigureAwait(false);
            doc.LoadHtml(ret);
            return doc;
        }
        public async Task<User> RequestUserAsync(string userId)
        {
            string url = String.Format("{0}ajax/user/{1}/profile/top", base_url, userId);
            string referer = String.Format("{0}member_illust.php?id={1}", base_url, user_id);
            JObject ret = await RequestJsonAsync(url, referer).ConfigureAwait(false);
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get User Fail");
            var userName = ret.GetValue("body").Value<JObject>("extraData").Value<JObject>("meta").Value<string>("title");
            if (ret.GetValue("body").Value<JObject>("illusts").Count > 0)
                foreach (var illustId in ret.GetValue("body").Value<JObject>("illusts"))
                {
                    var illust = await RequestIllustAsync(illustId.Key).ConfigureAwait(false);
                    userName = illust.userName;
                    break;
                }
            return new User(userId, userName, false);
        }
        public async Task<int> RequestFollowedUserCount()
        {
            string url = String.Format("{0}ajax/user/{1}/following?offset=0&limit=1&rest=show", base_url, user_id);
            string referer = String.Format("{0}bookmark.php?id={1}&rest=show", base_url, user_id);
            JObject ret =await RequestJsonAsync(url, referer).ConfigureAwait(false);
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get Bookmark Fail");
            return ret.GetValue("body").Value<int>("total");
        }
       
        public async Task<List<string>> RequestSearchPage(string word, int page, bool text_mode)
        {
            //s_mode:s_tc 在描述和标题里搜索 s_tag 在tag里搜索(部分一致) s_tag_full tag搜索(完全一致)
            //blt:最低收藏数 blg:最大收藏数
            //order:popular_male_d 最受男性欢迎 popular_d 最受欢迎 date_d 最新日期
            //scd:发布日期，格式:2020-08-02，但是因为bookmark的累积需要时间，同时根据收藏数量和时间筛选会漏，所以没有意义
            string url = String.Format("{0}ajax/search/artworks/{1}?word={1}&order=popular_male_d&mode=all&p={2}&s_mode={3}&type=all&blt=1000",
                                    base_url,word, page+1,text_mode ? "s_tc" : "s_tag");
            string referer = String.Format("{0}tags/{1}/artworks?s_mode=s_tag_full", base_url,word);
            JObject json =await RequestJsonAsync(url, referer).ConfigureAwait(false);
            List<string> ret = new List<string>();
            //排除包含非法关键字的图片
            foreach (var ill in json.GetValue("body").Value<JObject>().Value<JObject>("illustManga").Value<JArray>("data")) //这里返回的illust信息不全
            {
                bool valid = true;
                var tags = ill.ToObject<JObject>().Value<JArray>("tags");
                if (tags != null)
                    foreach (var tag in tags)
                        if (this.banned_keyword.Contains(tag.ToString()))
                            valid = false;
                if(valid)
                    ret.Add(ill.ToObject<JObject>().Value<string>("illustId"));
            }
            return ret;
        }
        public async Task<Illust> RequestIllustAsync(string illustId)
        {
            string url = String.Format("{0}ajax/illust/{1}", base_url, illustId);
            string referer = String.Format("{0}member_illust.php?mode=medium&illust_id={1}", base_url, user_id);            
            JObject json = await RequestJsonAsync(url, referer).ConfigureAwait(false);
            if (!json.HasValues)
                return new Illust(illustId, false);
            if (json.Value<Boolean>("error"))
                return new Illust(illustId, false);
            return new Illust(json.Value<JObject>("body"));
        }

    }
}
