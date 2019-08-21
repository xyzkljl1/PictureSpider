using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PixivAss.Data;
namespace PixivAss
{
    class Client
    {
        private string cookie;
        private string user_id;
        private string base_url;
        private string base_host;
        private string user_name;
        private PixivAss.Database database;
        //private HttpClient httpClient;
        public Client()
        {
            database = new Database("root","pixivAss","pass");
            user_id = "16428599";
            user_name = "xyzkljl1";
            base_url = "https://www.pixiv.net/";
            base_host = "www.pixiv.net";
            cookie = "first_visit_datetime_pc=2018-09-17+23%3A53%3A12; p_ab_id=3; p_ab_id_2=9; p_ab_d_id=541914940; yuid_b=h3AkQA; _ga=GA1.2.47913577.1537195995; a_type=0; b_type=1; login_ever=yes; ki_r=; __gads=ID=a2e6e4f87299c9b1:T=1537196638:S=ALNI_MYVPbTgfU44SPmopoQVmCpcrH0uHg; module_orders_mypage=%5B%7B%22name%22%3A%22sketch_live%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22tag_follow%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22recommended_illusts%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22everyone_new_illusts%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22following_new_illusts%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22mypixiv_new_illusts%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22fanbox%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22featured_tags%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22contests%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22user_events%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22sensei_courses%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22spotlight%22%2C%22visible%22%3Atrue%7D%2C%7B%22name%22%3A%22booth_follow_items%22%2C%22visible%22%3Atrue%7D%5D; d_type=1; limited_ads=%7B%22responsive%22%3A%22%22%7D; auto_view_enabled=1; __utmv=235335808.|2=login%20ever=yes=1^3=plan=premium=1^5=gender=male=1^6=user_id=16428599=1^9=p_ab_id=3=1^10=p_ab_id_2=9=1^11=lang=zh=1; stacc_mode=unify; __utmz=235335808.1561655715.96.2.utmcsr=order.pico2.jp|utmccn=(referral)|utmcmd=referral|utmcct=/shop1/toricotrick; ki_s=190722%3A0.0.0.0.0%3B195203%3A0.0.0.0.0%3B198890%3A0.0.0.0.0; howto_recent_view_history=56824293%2C73074191; bookToggle=cloud; c_type=28; is_sensei_service_user=1; _gid=GA1.2.1505179502.1566061798; __utmc=235335808; privacy_policy_agreement=1; login_bc=1; PHPSESSID=26c35fea895197b4dad589642983f0bc; ki_t=1537196613719%3B1566061800212%3B1566066292765%3B15%3B26; tags_sended=1; categorized_tags=0KixsJBDVn~AjBDLpRc95~BU9SQkS-zU~BeQwquYOKY~CADCYLsad0~EGefOqA6KB~EZQqoW9r8g~IRbM9pb4Zw~IVwLyT8B6k~OEXgaiEbRa~Qa8ggRsDmW~RcahSSzeRf~RsIQe1tAR0~THI8rtfzKo~UCltOqdXEy~WcTW9TCOx9~aC55Umcfh1~b8b4-hqot7~bXMh6mBhl8~iFcW6hPGPU~y8GNntYHsi; __utma=235335808.47913577.1537195995.1566073976.1566077571.145; OX_plg=pm; tag_view_ranking=0xsDLqCEW6~RTJMXD26Ak~KN7uxuR89w~MM6RXH_rlN~Lt-oEicbBr~Ie2c51_4Sp~3gc3uGrU1V~faHcYIP1U0~zyKU3Q5L4C~bXMh6mBhl8~BU9SQkS-zU~TOd0tpUry5~aKhT3n4RHZ~u8McsBs7WV~y8GNntYHsi~HY55MqmzzQ~iFcW6hPGPU~xha5FQn_XC~EUwzYuPRbU~pzzjRSV6ZO~9wN-K8_crj~AjBDLpRc95~TWrozby2UO~ETjPkL0e6r~x_jB0UM4fe~ouiK2OKQ-A~RcahSSzeRf~CrFcrMFJzz~jH0uD88V6F~l5WYRzHH5-~Hjx7wJwsUT~5oPIfUbtd6~TcgCqYbydo~jkcivN39wT~xZ6jtQjaj9~tgP8r-gOe_~_pwIgrV8TB~Bd2L9ZBE8q~THI8rtfzKo~jk9IzfjZ6n~uC2yUZfXDc~afQT0PrnAv~qWFESUmfEs~LVSDGaCAdn~2EpPrOnc5S~azESOjmQSV~QaiOjmwQnI~mzJgaDwBF5~YHRjLHL-7q~cbmDKjZf9z~Nbvc4l7O_x~_vCZ2RLsY2~85s1qqXlWy~NsbQEogeyL~RokSaRBUGr~tAZXG3M0z-~Bx3XxRyJlI~FaWxj7OPgp~JXmGXDx4tL~75zhzbk0bS~q303ip6Ui5~GvAi_xVl0u~Qa8ggRsDmW~Ti1gvrVQFO~K9_9y8aD2T~wbvCWCYbkM~xjfPXTyrpQ~Wzi7sMlG7S~VBEfuG5k7L~-StjcwdYwv~aCv01L8wRh~ujS7cIBGO-~QiS8jHBQYX~4rDNkkAuj_~DADQycFGB0~wj5pMdZnzG~X_1kwTzaXt~hrDZxHZLs1~MUQoS0sfqG~K8esoIs2eW~FH69TLSzdM~iVTmZJMGJj~Cac_6jhDcg~XgZwHIIL4V~DcMFRkXx6k~rQTnmliYQM~WcTW9TCOx9~ML8s4PH95U~28gdfFXlY7~B_OtVkMSZT~YRDwjaiLZn~LSG3qSZIDS~5f1R8PG9ra~L58xyNakWW~aC55Umcfh1~YoRlreqmAQ~ter5q-6mpB~v3nOtgG77A~wmxKAirQ_H~eVxus64GZU; __utmt=1; __utmb=235335808.102.9.1566080248107";
        }
        public void CheckStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new Exception("HTTP Not Success");
        }
        public string Test()
        {
            //CheckHomePage();
            //FetchBookMark(true);
            //FetchIllustByList(new List<string>{ "76278759"});
            FetchAllByUserId("3104565");
            return "12s3";
        }
        public void FetchAllByUserId(string userId)
        {
            string url = String.Format("{0}ajax/user/{1}/profile/all", base_url, userId);
            string referer = String.Format("{0}member_illust.php?id={1}", base_url, user_id);
            JObject ret = GetJsonAsync(url, referer).Result;
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get All By User Fail");
            var idList = new List<string>();
            foreach (var illust in ret.GetValue("body").Value<JObject>("illusts"))
                idList.Add(illust.Key.ToString());
            FetchIllustByList(idList);
        }
        public async Task<Illust> FetchIllustAsync(string illustId)
        {
            string url = String.Format("{0}ajax/illust/{1}", base_url, illustId);
            string referer = String.Format("{0}member_illust.php?mode=medium&illust_id={1}", base_url, user_id);
            JObject json =await GetJsonAsync(url, referer).ConfigureAwait(false);
            if (json.Value<Boolean>("error"))
                throw new Exception("Get Illust Fail");
            return new Illust(json.Value<JObject>("body"));
        }
        public void FetchIllustByList(List<string> illustIdList)
        {
            var illustList = new List<Illust>();
            var startTime1 = DateTime.Now;
            var task_list = new List<Task<Illust>>();
            foreach (var illustId in illustIdList)
                task_list.Add(FetchIllustAsync(illustId));
            Task.WaitAll(task_list.ToArray());
            foreach (var task in task_list)
                illustList.Add(task.Result);
            Console.WriteLine("Final:"+(DateTime.Now - startTime1).ToString());
            startTime1 = DateTime.Now;
            database.UpdateIllustLeft(illustList);
            Console.WriteLine("Final:" + (DateTime.Now - startTime1).ToString());
        }
        public void FetchBookMark(bool pub)
        {
            string url = String.Format("{0}ajax/user/{1}/illusts/bookmarks?tag=&offset=0&limit=40000&rest={2}", base_url,user_id,pub?"show":"hide");
            string referer = String.Format("{0}bookmark.php?id={1}&rest={2}", base_url, user_id, pub ? "show" : "hide");
            JObject ret =GetJsonAsync(url, referer).Result;
            if (ret.Value<Boolean>("error"))
                throw new Exception("Get Bookmark Fail");
            var idList = new List<string>();
            foreach(var illust in ret.GetValue("body").Value<JArray>("works"))
                idList.Add(illust.Value<string>("id"));
            FetchIllustByList(idList);
            Console.Write("Fetch "+ idList.Count.ToString()+" Bookmarks");
        }
        public void CheckHomePage()
        {
            string url = base_url;
            string referer = String.Format("{0}search.php?word=%E5%85%A8%E8%A3%B8&s_mode=s_tag_full&order=popular_d&p=1",base_url);
            var doc = GetHtmlAsync(base_url,referer).Result;
            HtmlNode headNode = doc.DocumentNode.SelectSingleNode("//a[@class='user-name js-click-trackable-later']");
            if (headNode != null)
                if (headNode.InnerText == this.user_name)
                    return;
            throw new Exception("Login Not Success");
        }
        public async Task<string> GetResponseAsync(string url,Uri referer)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                    throw new ArgumentNullException("url");
                if (!url.StartsWith("https"))
                    throw new ArgumentException("Not SSL");
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var handler = new HttpClientHandler() { UseCookies = false };
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
        public async Task<JObject> GetJsonAsync(string url,string referer)
        {
            JObject jsonobj = (JObject)JsonConvert.DeserializeObject(await GetResponseAsync(url, new Uri(referer)).ConfigureAwait(false));
            return jsonobj;
        }
        public async Task<HtmlDocument> GetHtmlAsync(string url, string referer)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(await GetResponseAsync(url, new Uri(referer)).ConfigureAwait(false));
            return doc;
        }
    }
}
