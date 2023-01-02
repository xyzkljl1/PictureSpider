using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Text;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PictureSpider;

namespace PictureSpider.Pixiv
{
    public partial class Server
    {
        /*
         * 下载
         */
        /*
         * 网络请求
         */
        static public void CheckStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new Exception("HTTP Not Success");
        }
        public async Task<string> RequestPixivAsyncGet(string url, Uri referer, bool anonymous = false)
        {
            HttpClient client=anonymous?httpClient_anonymous:httpClient;
            for (int try_ct = 8; try_ct >= 0; --try_ct)
            {
                try
                {
                    //Console.WriteLine("Begin " + try_ct.ToString() + " " + (url.Length>150?url.Substring(0, 150) :url));
                    if (string.IsNullOrEmpty(url))
                        throw new ArgumentNullException("url");
                    if (!url.StartsWith("https"))
                        throw new ArgumentException("Not SSL");
                    if(referer!=null)
                        client.DefaultRequestHeaders.Referrer = referer;
                    using (HttpResponseMessage response = await client.GetAsync(url))
                    {
                        //可能是作品已删除，此时仍然返回结果
                        if (response.StatusCode == HttpStatusCode.NotFound)
                            return await response.Content.ReadAsStringAsync();
                        //未知错误
                        CheckStatusCode(response);
                        //正常
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                catch (Exception e)
                {
                    string msg = e.Message;//e.InnerException.InnerException.Message;
                    if(try_ct<1)
                        Console.WriteLine(msg + "Re Try " + try_ct.ToString() + " On :" + url);
                    //if (try_ct == 0)
                        //throw;
                }
            }
            return null;
        }
        public async Task<string> RequestPixivAsyncPost(string url,Uri referer,string data)
        {
            for (int try_ct = 8; try_ct >= 0; --try_ct)
                try
                {
                    if (string.IsNullOrEmpty(url))
                        throw new ArgumentNullException("url");
                    if (!url.StartsWith("https"))
                        throw new ArgumentException("Not SSL");
                    httpClient.DefaultRequestHeaders.Referrer = referer;
                    using (var content= new StringContent(data, Encoding.UTF8, "application/json"))
                    using (HttpResponseMessage response = await httpClient.PostAsync(url, content))
                    {
                        var ret = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(response.StatusCode.ToString() + ":" + ret);
                        CheckStatusCode(response);
                        return ret;
                    }
                }
                catch (Exception e)
                {
                    if (try_ct < 1)
                        Console.WriteLine(e.Message + "Re Try " + try_ct.ToString() + " On :" + url);
                    //if (try_ct == 0)
                        //throw;
                }
            return null;
        }
        public async Task<JObject> RequestJsonAsync(string url, string referer,bool anonymous=false)
        {
            var r = await RequestPixivAsyncGet(url,referer.Length>0?new Uri(referer):null,anonymous);
            if(r!=null)
                return (JObject)JsonConvert.DeserializeObject(r);
            return (JObject)JsonConvert.DeserializeObject("{\"NetError\":true,\"error\":true}");
        }
        public async Task<HtmlDocument> RequestHtmlAsync(string url, string referer)
        {
            var doc = new HtmlDocument();
            var result = await RequestPixivAsyncGet(url, new Uri(referer));
            if(result is null)
                return null;
            doc.LoadHtml(result);
            return doc;
        }
        public async Task<string> RequestUserName(int userId)
        {
            string url = String.Format("{0}ajax/user/{1}?full=0", base_url, userId);//即使full=1也不包含illusts
            string referer = String.Format("{0}member_illust.php?id={1}", base_url, user_id);
            JObject ret = await RequestJsonAsync(url, referer, true);
            //对于停用的账号(和跑路不同)如2336,ajax/user/{1}/profile/top正常返回无error，而ajax/user/{1}请求失败()
            //将所有网络错误视作账号停用并忽略
            if (ret.Value<Boolean>("NetError"))
            {
                Console.WriteLine(String.Format("Can't Get UserName {0},Ignore", userId));
                return null;
            }
            if (ret.Value<Boolean>("error"))
            {
                Console.Error.WriteLine("Get UserName {0} Fail:{1}", userId, ret.Value<string>("message"));
                throw new TopLevelException(ret.Value<string>("message"));
            }
            var body = ret.Value<JObject>("body");
            if(body!=null)
                return body.Value<string>("name");
            return null;
        }
        public async Task<User> RequestUserAsync(int userId)
        {
            if (userId == 0)
                return null;
            string url = String.Format("{0}ajax/user/{1}/profile/top", base_url, userId);
            string referer = String.Format("{0}member_illust.php?id={1}", base_url, user_id);
            JObject ret = await RequestJsonAsync(url, referer,true);
            if (ret.Value<Boolean>("NetError"))
            {
                Console.Error.WriteLine(String.Format("Get User {0} Net Error",userId));
                return null;
            }
            if (ret.Value<Boolean>("error"))
            {
                if(ret.Value<string>("message")=="抱歉，您当前所寻找的个用户已经离开了pixiv, 或者这ID不存在。")//作者跑路了,正常情况
                    return null;
                Console.Error.WriteLine("Get User {0} Fail:{1}",userId,ret.Value<string>("message"));
                throw new TopLevelException(ret.Value<string>("message"));
            }
            var body = ret.Value<JObject>("body");
            var userName = body.Value<JObject>("extraData").Value<JObject>("meta").Value<string>("title");//这个是带-pixiv的，不是真实用户名
            //尝试通过另一个api获得用户名，获取不到就拿上一个将就一下
            //不能通过获取任意illust的方式获取用户名，因为调用频率限制很严格
            var tmp=await RequestUserName(userId);
            if (tmp != null)
                userName = tmp;
            return new User(userId, userName, false,false);
        }
        public async Task<int> RequestFollowedUserCount()
        {
            string url = String.Format("{0}ajax/user/{1}/following?offset=0&limit=1&rest=show", base_url, user_id);
            string referer = String.Format("{0}bookmark.php?id={1}&rest=show", base_url, user_id);
            JObject ret =await RequestJsonAsync(url, referer,false);
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get Bookmark Fail");
            return ret.GetValue("body").Value<int>("total");
        }

        public async Task<bool> PushBookmark(bool bookmarked,int illust_id,bool pub, Int64 bookmark_id=-1)
        {
            if(bookmarked)
            {
                var ret = await RequestPixivAsyncPost(String.Format("{0}ajax/illusts/bookmarks/add", base_url),
                                            new Uri(String.Format("{0}artworks/{1}", base_url, illust_id)),
                                            String.Format("{{\"illust_id\": \"{0}\", \"restrict\": {1}, \"comment\": \"\", \"tags\": []}}", illust_id, pub ? 0 : 1));
                var json = (JObject)JsonConvert.DeserializeObject(ret);
                if (json != null)
                    if (!json.Value<Boolean>("error"))
                        return true;
                return false;
            }
            else
            {
                if (bookmark_id < 0)
                    throw new ArgumentNullException("There must be a Bookmark ID when delete a bookmark");
                var ret=await RequestPixivAsyncPost(String.Format("{0}bookmark_setting.php", base_url),
                    new Uri(String.Format("{0}bookmark_add.php?type=illust&illust_id={1}",base_url,illust_id)),
                    String.Format("tt={0}&p=1&untagged=0&rest=show&book_id%5B%5D={1}&del=1",cookie_server.csrf_token,bookmark_id));
                Console.WriteLine(ret);
                return false;
            }
        }
        //获取搜索结果
        //!:key_word需要以URL编码
        private async Task<List<int>> RequestSearchResult(string key_word, bool text_mode, int start_page, int end_page,int start_like_count,int end_like_count)
        {
            var ret = new List<int>();
            try
            {
                var queue = new TaskQueue<List<int>>(25);
                for (int i = start_page; i < end_page; ++i)//页数从1开始，在RequestSearchPage里面加1了
                    await queue.Add(RequestSearchPage(key_word, i, text_mode, start_like_count,end_like_count));
                await queue.Done();
                foreach (var task in queue.done_task_list)
                    ret.AddRange(task.Result);
                Console.WriteLine("Search {0}:{1}_{2}", key_word.Substring(0, 20), ret.Count.ToString(), ret.Count > 0 ? ret[0].ToString() : "None");
                return ret;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
            return ret;
        }
        public async Task<List<int>> RequestSearchPage(string word, int page, bool text_mode, int start_like_count, int end_like_count)
        {
            //s_mode:s_tc 在描述和标题里搜索 s_tag 在tag里搜索(部分一致) s_tag_full tag搜索(完全一致)
            //blt:最低收藏数 bgt:最大收藏数
            //order:popular_male_d 最受男性欢迎 popular_d 最受欢迎 date_d 最新日期
            //scd:发布日期，格式:2020-08-02，但是因为bookmark的累积需要时间，同时根据收藏数量和时间筛选会漏，所以没有意义
            string url = String.Format("{0}ajax/search/artworks/{1}?word={1}&order=popular_male_d&mode=all&p={2}&s_mode={3}&type=all&blt={4}{5}",
                                    base_url,word, page+1,text_mode ? "s_tc" : "s_tag",
                                    start_like_count,end_like_count>0?"&bgt="+end_like_count:"");
            string referer = String.Format("{0}tags/{1}/artworks?s_mode=s_tag_full", base_url,word);
            JObject json =await RequestJsonAsync(url, referer);
            var ret = new List<int>();
            if (json.Value<Boolean>("error"))
                return ret;
            //检查是否超出最后一页，超出时会返回最后一页的内容
            var total=json.Value<JObject>("body").Value<JObject>("illustManga").Value<int>("total");//illust和manga都在一起
            if (page >= Math.Ceiling(total / SEARCH_PAGE_SIZE))
                return ret;
            //排除包含非法关键字的图片
            foreach (var ill in json.Value<JObject>("body").Value<JObject>("illustManga").Value<JArray>("data")) //这里返回的illust信息不全
            {
                bool valid = true;
                var tags = ill.ToObject<JObject>().Value<JArray>("tags");
                if (tags != null)
                    foreach (var tag in tags)
                        if (this.banned_keyword.Contains(tag.ToString()))
                            valid = false;
                if(valid)
                    ret.Add(ill.ToObject<JObject>().Value<int>("id"));
            }
            return ret;
        }
        //返回<结果,总数>
        public async Task<List<int>> RequestRankPage(string mode, int page)
        {
            //mode:daily weekly monthly male(受男性欢迎) female original(原创) rookie(新人)
            //仅有daily/weekly/male/female可以带_r18后缀
            //date:指定日期，形如20200921，没有获知过往排行的必要所以不使用
            string url = String.Format("{0}ranking.php?mode={1}&p={2}&format=json",
                                    base_url, mode, page + 1);
            JObject json = await RequestJsonAsync(url, base_url,false);//部分排行榜需要登录
            var ret = new List<int>();
            if (json.Value<Boolean>("error"))
                return ret;
            //排除包含非法关键字的图片
            if(json.GetValue("contents")!=null)
                foreach (var illust_object in json.Value<JArray>("contents"))
                {
                    var illust_id = illust_object.Value<int>("illust_id");
                    var valid = true;
                    var tags = illust_object.Value<JArray>("tags");
                    if (tags != null)
                        foreach (var tag in tags)
                            if (this.banned_keyword.Contains(tag.ToString()))
                                valid = false;
                    //额外有一个
                    var types = illust_object.Value<JObject>("illust_content_type");
                    if (types.Value<bool>("homosexual") || types.Value<bool>("bl"))
                        valid = false;
                    if (valid)
                        ret.Add(illust_id);
                }
            /*目前没有必要获取总数
            int total = 0;
            if (json.GetValue("rank_total")!=null)
                total = json.Value<Int32>("rank_total");
            */
            return ret;
        }
        public async Task<Illust> RequestIllustAsync(int illustId)
        {
            string url = String.Format("{0}ajax/illust/{1}", base_url, illustId);
            string referer = String.Format("{0}member_illust.php?mode=medium&illust_id={1}", base_url, user_id);
            //现在R18如95047397需要登录才能查看，用匿名client可以成功返回但是不会获得url
            JObject json = await RequestJsonAsync(url, referer,false);
            if (json.Value<Boolean>("NetError"))//因网络原因获取不到时，不认为是无效的
                return null;
            if (json.Value<Boolean>("error"))//否则标记未无效
                return new Illust(illustId, false);
            if (json.Value<JObject>("body").Value<Int32>("illustType")==2)//动图需要额外获取动图信息
            {
                string ugoira_url = String.Format("{0}/ugoira_meta?lang=zh", url);
                JObject ugoira_json = await RequestJsonAsync(ugoira_url, "", false);//需要非匿名
                if (json.Value<Boolean>("NetError"))//因网络原因获取不到时，不认为是无效的
                    return null;
                if (json.Value<Boolean>("error"))//能获取到图片信息但获取不到动图信息时报错
                    throw new TopLevelException("Get Ugoira Error");
                return new Illust(json.Value<JObject>("body"), ugoira_json.Value<JObject>("body"));
            }
            return new Illust(json.Value<JObject>("body"));
        }
    }
}
