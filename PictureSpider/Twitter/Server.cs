using Microsoft.AspNetCore.WebUtilities;
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
using System.Text;
using System.Threading.Tasks;

namespace PictureSpider.Twitter
{
    /*
     * TODO:
     * 显示视频
     * 处理删除的media(由于是一次性fetch到media且不会重复获取，并且总量较少从获取到下载的间隔短，失效的情况比较少见，暂不处理)
     */
    public partial class Server : BaseServer, IBindHandleProvider, IDisposable
    {
        public BindHandleProvider provider { get; set; } = new BindHandleProvider();
        private HttpClient httpClient_pub;
        private HttpClient httpClient_myapi;
        private string base_url_website = "https://twitter.com/";
        private string base_url_v2 = "https://api.twitter.com/2/";
        private string base_url_v11 = "https://api.twitter.com/1.1/";
        //private string base_host = "www.pixiv.net";
        private string my_user_name;
        private string my_password;
        private string download_dir_root;
        private string download_dir_tmp;
        private string download_dir_private;
        private string request_proxy;
        private Database database;
        Aria2DownloadQueue downloader;
        private CookieContainer cookie_container=new CookieContainer();
        public Server(Config config)
        {
            base.logPrefix = "T";

            my_user_name = config.TwitterUserName;
            my_password = config.TwitterPassword;
            download_dir_root = config.TwitterDownloadDir;
            download_dir_tmp = Path.Combine(download_dir_root, "tmp");
            download_dir_private = Path.Combine(download_dir_root, "private");
            foreach (var dir in new List<string> { download_dir_root, download_dir_tmp, download_dir_private })
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

            request_proxy = config.Proxy;
            database = new Database(config.TwitterConnectStr);
            downloader = new Aria2DownloadQueue(Aria2DownloadQueue.Downloader.Twitter, config.Proxy);
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var handler = new HttpClientHandler()
                {
                    MaxConnectionsPerServer = 256,
                    UseCookies = true,//task.json的flow需要cookies
                    CookieContainer = cookie_container,
                    Proxy = new WebProxy(request_proxy, false)
                };
                handler.ServerCertificateCustomValidationCallback = delegate { return true; };
                httpClient_pub = new HttpClient(handler);
                httpClient_pub.Timeout = new TimeSpan(0, 0, 35);
                httpClient_pub.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
                httpClient_pub.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
                httpClient_pub.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");
                //httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {config.TwitterBearerToken}");//My api，develop portal中申请的token
                httpClient_pub.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA");//Public api(浏览器访问网页时使用的固定token)
                                                                                                                                                                                                                     //如果不设置sec-fetch-*,则ct0/x-csrf-token每次请求后都会改变
                httpClient_pub.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
                httpClient_pub.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
                httpClient_pub.DefaultRequestHeaders.Add("sec-fetch-site", "same-site");

                //不同的请求需要使用不同的host，此处url都使用原本域名，不需要设置host
                //httpClient.DefaultRequestHeaders.Host = "twitter.com";
                //httpClient.DefaultRequestHeaders.Host = "api.twitter.com";
                //httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
                //httpClient.DefaultRequestHeaders.Add("x-csrf-token", "7617e0974c96ef6a593bf03ddfffe9ae");
                //httpClient.DefaultRequestHeaders.Add("x-guest-token", "1606610372651536384");
                //httpClient.DefaultRequestHeaders.Add("x-twitter-client-language", "zh-cn");
                //httpClient.DefaultRequestHeaders.Add("Cookie", @"auth_token=169e974b1946b3c2eee680f5c47facae907e8634; ct0=4cfcd20045d8114e981a6057672152177a136d9a68df0e30e5710b1571d6fe9f60dc7bcfb8120f34f71f613a4558c0f3184ffbf73f8810d1fbbb7eb55baa0ab59500939908a3633428dcd149f025969e;");
                //httpClient.DefaultRequestHeaders.Add("Cookie", @"ct0=7617e0974c96ef6a593bf03ddfffe9ae");
            }
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var handler = new HttpClientHandler()
                {
                    MaxConnectionsPerServer = 256,
                    UseCookies = true,//task.json的flow需要cookies
                    CookieContainer = cookie_container,
                    Proxy = new WebProxy(request_proxy, false)
                };
                handler.ServerCertificateCustomValidationCallback = delegate { return true; };
                httpClient_myapi = new HttpClient(handler);
                httpClient_myapi.Timeout = new TimeSpan(0, 0, 35);
                httpClient_myapi.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3");
                httpClient_myapi.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,ja;q=0.8");
                httpClient_myapi.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36");
                httpClient_myapi.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse($"Bearer {config.TwitterBearerToken}");//My api，develop portal中申请的token
            }
        }
        public override async Task Init() {
            //return;
            //await CheckGetUserNameApi();
            if (!await Login())
                throw new TopLevelException("TwitterServer Login Failed");
            else
                Log($"Twitter Login Success.");
            //await FetchTweetsByUserAPI("AndyBunyan13", "0");
            RunSchedule();
            //Task.Run(RunSchedule);
        }
        public override void SetReaded(ExplorerFileBase file)
        {
            var media = (file as ExplorerFile).media;
            media.readed = file.readed;
            database.UpdateMediaProperty(new List<Media> {media},"readed").Wait();
        }
        public override async Task<List<ExplorerQueue>> GetExplorerQueues()
        {
            var ret = new List<ExplorerQueue>();
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.FavR, "0", "FavR"));
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.MainR, "0", "MainR"));
            foreach (var user in await database.GetUsers(false,true))
                ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.User, user.id, $"@{user.name}"));
            return ret;
        }
        public override async Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue)
        {
            var medias = new List<Media>();
            if (queue.type == ExplorerQueue.QueueType.MainR)
                medias = await database.GetFollowedUnreadMedia();
            else if (queue.type == ExplorerQueue.QueueType.FavR)
                medias = await database.GetBookmarkedMedia();
            else
                medias = await database.GetMediaByUserId(queue.id);
            var result = new List<ExplorerFileBase>();
            foreach (var media in medias)
                if(media.media_type==MediaType.Image)
                    result.Add(new ExplorerFile(media, download_dir_tmp));
            return result;
        }
        public override void SetBookmarked(ExplorerFileBase file)
        {
            var media = (file as ExplorerFile).media;
            media.bookmarked=file.bookmarked;
            database.UpdateMediaProperty(new List<Media> { media},"bookmarked").Wait();
        }
        public override BaseUser GetUserById(string id) { return database.GetUserById(id).Result; }
        public override void SetUserFollowOrQueue(BaseUser user) { database.UpdateUserFollowOrQueue(user as User).Wait(); }
        private async Task RunSchedule()
        {
            int interval_ct = 0;
            var interval = new TimeSpan(1, 0, 0);
            do
            {
                if(interval_ct%24==0)//每24次间隔
                {
                    //重新获取最新tweet
                    var users = await database.GetUsers(true, true);
                    foreach (var user in users)
                    {
                        await FetchTweetsByUserSearch(user.name, user.search_latest_tweet_id);
                        await FetchTweetsByUserAPI(user.name, user.api_latest_tweet_id);
                    }
                    //同步收藏文件夹
                    await SyncBookmarkDirectory();
                }
                //每次间隔下载media
                {
                    var medias = await database.GetWaitingDownloadMedia(120);
                    Log($"Start Download:{medias.Count()}");
                    foreach (var media in medias)
                    {
                        var dir = download_dir_tmp;
                        var path = Path.Combine(dir, media.file_name);
                        if (File.Exists(path))
                            File.Delete(path);
                        await downloader.Add(media.url, dir, media.file_name);
                    }
                    await downloader.WaitForAll();
                    int ct = 0;
                    foreach (var media in medias)
                    {
                        var dir = download_dir_tmp;
                        var path = Path.Combine(dir, media.file_name);
                        if (File.Exists(path))
                        {
                            media.downloaded = true;
                            ct++;
                        }
                    }
                    await database.UpdateMediaProperty(medias,"downloaded");
                    Log($"{DateTime.Now} Download Done:{ct}/{medias.Count()}");
                }
                await Task.Delay(interval);
                interval_ct++;
            }
            while (true);
        }
        private async Task<User> FetchUserByName(string user_name)
        {
            // {"data":{"id":"294025417","name":"「艦これ」開発/運営","username":"KanColle_STAFF"}}
            var ret = await GetJsonAsync($"{base_url_v2}users/by/username/{user_name}",false);
            if (ret["data"] != null
                && ret["data"]["id"] != null)
                {
                    var user = new User();
                    user.id = ret["data"]["id"].ToString();
                    user.name = user_name;
                    user.nick_name=ret["data"]["name"].ToString();
                    await database.AddUserBase(new List<User> { user });
                    return user;
                }
            LogError($"Can't Fetch User {user_name}");
            return null;
            //throw new TopLevelException("Fail");
        }
        /*
         * 有的用户比如@yakumomomoko,不登陆时只能搜到少量tweet，因此需要登录
         * 实质是获取x-csrf-token以及cookie中的auth_token和ct0，其中ct0和x-csrf-token的值相等
         */
        private async Task<bool> Login()
        {
            //登录，参考https://github.com/mikf/gallery-dl/blob/master/gallery_dl/extractor/twitter.py
            //(twitter.com/sessions已经失效)
            /*
             * 获得x-guest-token
             */
            await UpdateXGuestToken();

            /*
             * 开始一个flow,依次执行若干Subtask
             * 首先获得flow_token
             * {
              "flow_token": "g;167186288841152109:-1671862888419:GkcaBFibLBTMuwD5tX7s1Tfo:0",
              "status": "success",
              "subtasks": [
                {
                  "subtask_id": "LoginJsInstrumentationSubtask",
                  "js_instrumentation": {
                    "url": "https://twitter.com/i/js_inst?c_name=ui_metrics",
                    "timeout_ms": 2000,
                    "next_link": {
                      "link_type": "task",
                      "link_id": "next_link"
                    }
                  }
                }
              ]
            }
             */
            string flow_token = "";
            {
                //httpClient.DefaultRequestHeaders.Referrer = new Uri("https://twitter.com/i/flow/login");
                var data = @"{""input_flow_data"":{""flow_context"":{""debug_overrides"":{},""start_location"":{""location"":""unknown""}}},
                              ""subtask_versions"":{""action_list"":2,""alert_dialog"":1,""app_download_cta"":1,""check_logged_in_account"":1,""choice_selection"":3,""contacts_live_sync_permission_prompt"":0,""cta"":7,""email_verification"":2,""end_flow"":1,""enter_date"":1,""enter_email"":2,""enter_password"":5,""enter_phone"":2,""enter_recaptcha"":1,""enter_text"":5,""enter_username"":2,""generic_urt"":3,""in_app_notification"":1,""interest_picker"":3,""js_instrumentation"":1,""menu_dialog"":1,""notifications_permission_prompt"":2,""open_account"":2,""open_home_timeline"":1,""open_link"":1,""phone_verification"":4,""privacy_options"":1,""security_key"":3,""select_avatar"":4,""select_banner"":2,""settings_list"":7,""show_code"":1,""sign_up"":2,""sign_up_review"":4,""tweet_selection_urt"":1,""update_users"":1,""upload_media"":1,""user_recommendations_list"":4,""user_recommendations_urt"":1,""wait_spinner"":3,""web_modal"":1}}";
                var responseObject = ParseJson(await PostAsync($"{base_url_website}i/api/1.1/onboarding/task.json?flow_name=login", data));
                if(responseObject["flow_token"]!=null)
                    flow_token= responseObject["flow_token"].ToString();
                else
                    return false;
            }
            // LoginJsInstrumentationSubtask
            {
                var data = $@"{{""flow_token"":""{flow_token}"",""subtask_inputs"":[{{""subtask_id"":""LoginJsInstrumentationSubtask"",""js_instrumentation"":{{""response"":""{{}}"",""link"":""next_link""}}}}]}}";
                var responseObject = ParseJson(await PostAsync($"{base_url_website}i/api/1.1/onboarding/task.json?flow_token={flow_token}", data));
                //每次都要更新flow_token
                if (responseObject["flow_token"] != null)
                    flow_token = responseObject["flow_token"].ToString();
                else
                    return false;
            }
            // LoginEnterUserIdentifierSSO 输入用户名
            {
                var data = $@"{{""flow_token"":""{flow_token}"",
                                ""subtask_inputs"":[
                                        {{""subtask_id"":""LoginEnterUserIdentifierSSO"",
                                          ""settings_list"":
                                                {{""setting_responses"":[
                                                          {{ ""key"": ""user_identifier"",
                                                             ""response_data"": {{ ""text_data"": {{""result"": ""{my_user_name}"" }} }}
                                                          }}],
                                                  ""link"":""next_link""}}
                                        }}]}}";
                var responseObject = ParseJson(await PostAsync($"{base_url_website}i/api/1.1/onboarding/task.json?flow_token={flow_token}", data));
                if (responseObject["flow_token"] != null)
                    flow_token = responseObject["flow_token"].ToString();
                else
                    return false;
            }
            //LoginEnterPassword 输入密码
            {
                var data = $@"{{""flow_token"":""{flow_token}"",
                                ""subtask_inputs"":[
                                        {{""subtask_id"":""LoginEnterPassword"",
                                          ""enter_password"":
                                                {{""password"":""{my_password}"",
                                                  ""link"":""next_link""}}
                                        }}]}}";
                var responseObject = ParseJson(await PostAsync($"{base_url_website}i/api/1.1/onboarding/task.json?flow_token={flow_token}", data));
                if (responseObject["flow_token"] != null)
                    flow_token = responseObject["flow_token"].ToString();
                else
                    return false;
            }
            //AccountDuplicationCheck 完成登录，获得auth_token
            //还会顺带获得ct0，获得ct0后必须设置csrf-token，两者不匹配会403
            {
                var data = $@"{{""flow_token"":""{flow_token}"",
                                ""subtask_inputs"":[
                                        {{""subtask_id"":""AccountDuplicationCheck"",
                                          ""check_logged_in_account"":
                                                {{""link"":""AccountDuplicationCheck_false""}}
                                        }}]}}";
                var responseObject = ParseJson(await PostAsync($"{base_url_website}i/api/1.1/onboarding/task.json?flow_token={flow_token}", data));
                if (responseObject["flow_token"] != null)
                    flow_token = responseObject["flow_token"].ToString();
                else
                    return false;
            }
            return true;
        }
        private void UpdateCSRFToken()
        {
            //部分请求会导致ct0更新，需要更新csrf-token使二者匹配,否则会403，因此每次请求后都要检查
            //例如登录完成后ct0更新，开始搜索后又会更新
            //只有需要更新
            var cookie = cookie_container.GetCookies(new Uri(base_url_website))["ct0"];
            string origin_token = "";
            if (httpClient_pub.DefaultRequestHeaders.Contains("x-csrf-token"))
                if (httpClient_pub.DefaultRequestHeaders.GetValues("x-csrf-token").Count() > 0)
                    origin_token = httpClient_pub.DefaultRequestHeaders.GetValues("x-csrf-token").First();
            if (cookie != null)
                if(!string.IsNullOrEmpty(cookie.Value))
                    if(cookie.Value != origin_token)
                    {
                        httpClient_pub.DefaultRequestHeaders.Remove("x-csrf-token");//Add不会清空旧的值
                        httpClient_pub.DefaultRequestHeaders.Add("x-csrf-token", cookie.Value);
                        Log($"Update CSRF Token{cookie.Value}");
                    }
        }
        /*
         * x-guest-token,会过期因此每次搜索前重新获取
         * 登录和搜索要使用不同的x-guest-token?
         */
        private async Task<Boolean> UpdateXGuestToken()
        {
            try
            {
                var response = await httpClient_pub.PostAsync($"{base_url_v11}guest/activate.json", null);
                var responseContent = await response.Content.ReadAsStringAsync();
                var json = JsonConvert.DeserializeObject<JObject>(responseContent);
                if (json["guest_token"] != null)
                {
                    httpClient_pub.DefaultRequestHeaders.Remove("x-guest-token");
                    httpClient_pub.DefaultRequestHeaders.Add("x-guest-token", json["guest_token"].ToString());
                    return true;
                }
            }
            catch (Exception e)
            {
                LogError($"Update XGuestToken Fail{e.Message}");
            }
            return false;
        }
        private async Task FetchTweetsByUserSearch(string user_name, string _since_tweet_id)
        {
            /*
             * 因为垃圾的twitter api只能获得最近的tweet，更高权限又申请不到，通过模拟搜索页获得特定用户的历史tweet
             * 注意搜索搜到的推特不全！
             * 不登陆时搜到的很少，登录不同账号搜到的数量不同，并且都不能搜到特定用户的全部推，例如AndyBunyan13；有的tweet再怎么细化条件也搜不到
             */
            try
            {
                await UpdateXGuestToken();
                //搜索
                var users = new Dictionary<string, User>();
                var tweets = new Dictionary<string, Tweet>();
                var medias = new Dictionary<string, Media>();
                Int64 since_tweet_id = Int64.Parse(_since_tweet_id);

                var paras = new Dictionary<string, string>{
                    {"q" ,$"(from:@{user_name}) -filter:replies" },//query
                    {"include_profile_interstitial_type", "1"},
                    {"include_blocking", "1"},
                    {"include_blocked_by", "1"},
                    {"include_followed_by", "1"},
                    {"include_want_retweets", "1"},
                    {"include_mute_edge", "1"},
                    {"include_can_dm", "1"},
                    {"include_can_media_tag", "1"},
                    {"skip_status", "1"},
                    {"cards_platform", "Web-12"},
                    {"include_cards", "1"},
                    {"include_composer_source", "true"},
                    {"include_ext_alt_text", "true"},
                    {"include_reply_count", "1"},
                    {"tweet_mode", "extended"},
                    {"include_entities", "true"},
                    {"include_user_entities", "true"},
                    {"include_ext_media_color", "true"},
                    {"include_ext_media_availability", "true"},
                    {"send_error_codes", "true"},
                    {"simple_quoted_tweets", "true"},
                    {"tweet_search_mode", "live"},
                    {"count", "20" },//每页数量，似乎最大20？
                    {"query_source", "typed_query"},
                    {"pc", "1"},
                    {"spelling_corrections", "1"},
                    {"ext", "mediaStats,highlightedLabel,hasNftAvatar,voiceInfo,birdwatchPivot,enrichments,superFollowMetadata,unmentionInfo,editControl,collab_control,vibe"},
                    //{"ext", "mediaStats,highlightedLabel,cameraMoment"},

                };
                var search_uri = QueryHelpers.AddQueryString($"{base_url_v2}search/adaptive.json", paras);
                //search_uri = "https://api.twitter.com" + "/2/search/adaptive.json?include_profile_interstitial_type=1&include_blocking=1&include_blocked_by=1&include_followed_by=1&include_want_retweets=1&include_mute_edge=1&include_can_dm=1&include_can_media_tag=1&include_ext_has_nft_avatar=1&include_ext_is_blue_verified=1&include_ext_verified_type=1&skip_status=1&cards_platform=Web-12&include_cards=1&include_ext_alt_text=true&include_ext_limited_action_results=false&include_quote_count=true&include_reply_count=1&tweet_mode=extended&include_ext_collab_control=true&include_entities=true&include_user_entities=true&include_ext_media_color=true&include_ext_media_availability=true&include_ext_sensitive_media_warning=true&include_ext_trusted_friends_metadata=true&send_error_codes=true&simple_quoted_tweet=true&q=(from%3Ayakumomomoko)%20-filter%3Areplies&count=20&query_source=typed_query&cursor=DAACCwABAAABGnRoR0FWVVYwVkZWQllCRm9DTWh1ak8zTnl1TEJJWXRBRVNZOExyQUFBQjlELUFZazNTOGFuOEFBQUFGQlloYTM1YmxVQUFGaS1WMml5YUVBSVdGMG95NDlxQUFSWVE0U1ROV21BQUZqeVAyNUNhQUFBV1BkUm45QnB3QUJZU0k3dUhHcEFBRmk1bHJGUlZRQUFXR1pCajhKcHdBQllUVXFiaFdqQUJGZ3oxVjZNYVlBQVdPZVJpdjFwUUFSWVZrTmUzbFVBQUZnLVpqY0ZhZ0FBV0o4ZHZTMVVnQVJZTGVvcjhHakFCRmhpZm9lMmFrQUVXTXk1Qm1WcFFBQllkcU1FbTFWQUFGallJZHgwYVVBTEZLQlVBRlFBQQoAAhZLC-IsgCcRCgADFksL4ix__z8IAAQAAAACCwAFAAAA8EVtUEM2d0FBQWZRL2dHSk4wdkdwL0FBQUFCUVdJV3QrVzVWQUFCWXZsZG9zbWhBQ0ZoZEtNdVBhZ0FFV0VPRWt6VnBnQUJZOGo5dVFtZ0FBRmozVVovUWFjQUFXRWlPN2h4cVFBQll1WmF4VVZVQUFGaG1RWS9DYWNBQVdFMUttNFZvd0FSWU05VmVqR21BQUZqbmtZcjlhVUFFV0ZaRFh0NVZBQUJZUG1ZM0JXb0FBRmlmSGIwdFZJQUVXQzNxSy9Cb3dBUllZbjZIdG1wQUJGak11UVpsYVVBQVdIYWpCSnRWUUFCWTJDSGNkR2xBQwgABgAAAAAIAAcAAAAAAAA&pc=1&spelling_corrections=1&include_ext_edit_control=true&ext=mediaStats%2ChighlightedLabel%2ChasNftAvatar%2CvoiceInfo%2CbirdwatchPivot%2Cenrichments%2CsuperFollowMetadata%2CunmentionInfo%2CeditControl%2Ccollab_control%2Cvibe";
                bool complete = false;
                while (true)
                {
                    await Task.Delay(new TimeSpan(0, 0, 1));//防止请求过于频繁,似乎不需要？
                    var responseContent = await GetAsync(search_uri);
                    var dateTimeConverter = new IsoDateTimeConverter { DateTimeFormat = "ddd MMM dd HH:mm:ss zzzz yyyy", Culture = System.Globalization.CultureInfo.InvariantCulture };
                    var deserializeSettings = new JsonSerializerSettings();
                    deserializeSettings.Converters.Add(dateTimeConverter);
                    var jsonDoc = JsonConvert.DeserializeObject<dynamic>(responseContent);
                    if (jsonDoc["globalObjects"] is null || jsonDoc["globalObjects"]["tweets"] is null)//搜索失败
                    {
                        Log("Search Fail");
                        break;
                    }
                    //User
                    foreach (var userPair in (jsonDoc.globalObjects.users as JObject).Properties())
                    {
                        var id = userPair.Name;//user id(数字)
                                               //注意此处screen_name是user name，而name是昵称
                        if (!users.ContainsKey(id))
                            users.Add(id, new User(id, userPair.Value["screen_name"].ToString(), userPair.Value["name"].ToString()));
                    }
                    //Tweet&Media
                    {
                        var tweetsObject = jsonDoc.globalObjects.tweets as JObject;
                        if (tweetsObject.Properties().Count() == 0)//搜索结束
                        {
                            complete = true;
                            break;
                        }
                        foreach (var tweetPair in tweetsObject.Properties())
                        {
                            var id = tweetPair.Name;
                            if (Int64.Parse(id) <= since_tweet_id)
                            {
                                complete = true;//返回的结果是按时间倒序，但是此处的遍历并不是按顺序，所以此处不break
                                continue;
                            }
                            var tweetObject = tweetPair.Value;
                            var tweet = new Tweet();
                            tweet.id = id;
                            tweet.created_at = DateTime.ParseExact(tweetObject.Value<string>("created_at"), "ddd MMM dd HH:mm:ss zzzz yyyy", System.Globalization.CultureInfo.InvariantCulture);//"Thu Dec 22 07:46:32 +0000 2022"
                            tweet.full_text = tweetObject.Value<string>("full_text");
                            tweet.user_id = tweetObject.Value<string>("user_id_str");
                            tweet.url = $"{base_url_website}{users[tweet.user_id].name}/status/{tweet.id}";//此时users中应该一定有该user
                            if (!tweets.ContainsKey(tweet.id))
                                tweets.Add(tweet.id, tweet);
                            //media
                            if (tweetObject["extended_entities"] != null
                                && tweetObject["extended_entities"]["media"] != null)
                                foreach (var mediaObject in tweetObject["extended_entities"]["media"])
                                {
                                    var media = new Media();
                                    //注意此处使用key作为id
                                    media.id = media.key = mediaObject.Value<string>("media_key");
                                    media.expand_url = mediaObject.Value<string>("expanded_url");
                                    media.user_id = tweet.user_id;
                                    media.tweet_id = tweet.id;
                                    var type = mediaObject.Value<string>("type");
                                    //参考https://github.com/mikf/gallery-dl/blob/master/gallery_dl/extractor/twitter.py extract_media
                                    if (type == "photo" || type == "animated_gif")//图片
                                    {
                                        media.media_type = MediaType.Image;
                                        media.url = mediaObject.Value<string>("media_url_https");
                                        //直接访问media_url不能得到原图，图片有不同规格:orig 4096x4096 large medium small
                                        var ext = Util.GetExtFromURL(media.url);
                                        var idx = media.url.LastIndexOf(".");
                                        if (idx != -1)
                                            media.url = media.url.Substring(0, idx);
                                        //指定jpg格式，orig规格
                                        media.url += $"?format={ext}&name=orig";
                                        media.file_name = $"{media.id}.{ext}";
                                    }
                                    else if (type == "video")//视频
                                    {
                                        media.media_type = MediaType.Video;
                                        //下载码率最大的视频
                                        int max_bitrate = -1;
                                        foreach (var videoObject in mediaObject["video_info"]["variants"].ToArray())
                                        {
                                            int bitrate = videoObject.Value<int>("bitrate");
                                            if (bitrate > max_bitrate)
                                            {
                                                max_bitrate = bitrate;
                                                media.url = videoObject.Value<string>("url");
                                            }
                                        }
                                        if (string.IsNullOrEmpty(media.url))
                                            throw new TopLevelException("Can't Find Twitter Video URL");
                                        var ext = Util.GetExtFromURL(media.url);
                                        media.file_name = $"{media.id}.{ext}";
                                    }
                                    else
                                        throw new TopLevelException($"Unknown Media Type:{type}");
                                    if (!medias.ContainsKey(media.id))
                                        medias.Add(media.id, media);
                                }
                        }
                        if (complete)//如果已经搜到上次的位置则退出
                            break;
                    }
                    //找到下一页的cursor
                    string cursor = "";
                    foreach (var instruction in jsonDoc.timeline.instructions)
                        if (instruction.addEntries != null)
                        {
                            foreach (var entry in instruction.addEntries.entries)
                                if (((string)entry.entryId).StartsWith("sq-cursor-bottom"))
                                    cursor = (string)entry.content.operation.cursor.value;
                        }
                        else if (instruction.replaceEntry != null)
                        {
                            if (((string)instruction.replaceEntry.entryIdToReplace).StartsWith("sq-cursor-bottom"))
                                cursor = (string)instruction.replaceEntry.entry.content.operation.cursor.value;
                        }
                    if (string.IsNullOrEmpty(cursor))//不明原因未获得cursor
                        break;
                    //移动cursor
                    paras["cursor"] = cursor;
                    search_uri = QueryHelpers.AddQueryString($"{base_url_v2}search/adaptive.json", paras);
                }
                //更新数据库
                Log($"Fetch User(Search) @{user_name} " + (complete ? "Complete." : "Break!!") + $":{tweets.Count} Tweets/{medias.Count} Medias");
                await database.AddUserBase(users.Values.ToList());
                await database.AddTweetFull(tweets.Values.ToList());
                await database.AddMediaBase(medias.Values.ToList());
                if (complete)//只有搜到底，才更新latest_tweet_id
                    foreach (var user in users.Values)
                        if (user.name == user_name)
                        {
                            //转成int64再比较,越晚的tweet的id越大
                            user.search_latest_tweet_id = tweets.Keys.ToList()
                                                        .Select<string, Int64>(x => Int64.Parse(x))
                                                        .Max().ToString();
                            await database.UpdateUserSearchLatestTweet(user);
                        }
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }
        private async Task FetchTweetsByUserAPI(string user_name, string _since_tweet_id)
        {
            try
            {
                var user = await FetchUserByName(user_name);//此时不一定已经知道id，所以重新Fetch
                if (user is null)
                    return;
                var user_id=user.id;
                //搜索
                var tweets = new Dictionary<string, Tweet>();
                var medias = new Dictionary<string, Media>();//注意此处以media_key为key，而不是media_id
                Int64 since_tweet_id = Int64.Parse(_since_tweet_id);
                bool complete = false;

                var paras = new Dictionary<string, string>{
                    { "max_results","100"},
                    { "exclude","replies,retweets"},//不包括回复和转推，这会使最大查询数量从3200缩减到800
                    { "tweet.fields","attachments,created_at"},
                    { "media.fields","media_key,url,variants,type"},
                    //{ "pagination_token",""},//page token,不能为空，为空时需要去掉
                    { "expansions","attachments.media_keys"},//返回media的详细信息
                };
                string next_token = "";
                do
                {
                    if (string.IsNullOrEmpty(next_token))
                        paras.Remove("pagination_token");
                    else
                        paras["pagination_token"] = next_token;
                    var search_uri = QueryHelpers.AddQueryString($"{base_url_v2}users/{user_id}/tweets", paras);
                    var responseContent = await GetAsync(search_uri,false);
                    var jsonDoc = JsonConvert.DeserializeObject<JObject>(responseContent);
                    foreach(JObject tweetObject in jsonDoc["data"].ToArray())
                    {
                        
                        var id = tweetObject["id"].ToString();
                        if (Int64.Parse(id) <= since_tweet_id)
                        {
                            complete = true;
                            continue;
                        }
                        var tweet = new Tweet();
                        tweet.id = id;
                        tweet.created_at = DateTime.Parse(tweetObject.Value<string>("created_at"));//"2022-12-25T08:36:55.000Z"
                        tweet.full_text = tweetObject.Value<string>("text");
                        tweet.user_id = user_id;
                        tweet.url = $"{base_url_website}{user_id}/status/{tweet.id}";
                        if (!tweets.ContainsKey(tweet.id))
                            tweets.Add(tweet.id, tweet);
                        if (tweetObject["attachments"] != null
                            && tweetObject["attachments"]["media_keys"]!=null)
                            foreach(var mediaKeyObject in tweetObject["attachments"]["media_keys"].ToArray())
                            {
                                var media= new Media();
                                media.id=media.key=mediaKeyObject.ToString();
                                media.user_id=user_id;
                                media.tweet_id=tweet.id;
                                media.expand_url = tweet.url;//懒得生成expand_url,用tweet的url代替
                                if(!medias.ContainsKey(media.key))
                                    medias.Add(media.key, media);
                            }

                    }
                    if (jsonDoc["includes"] != null && jsonDoc["includes"]["media"] != null)
                        foreach (var mediaObject in jsonDoc["includes"]["media"].ToArray())
                        {
                            var key=mediaObject["media_key"].ToString();
                            if (!medias.ContainsKey(key))
                                continue;
                            var media = medias[key];
                            var type = mediaObject.Value<string>("type");
                            //参考https://github.com/mikf/gallery-dl/blob/master/gallery_dl/extractor/twitter.py extract_media
                            if (type == "photo" || type == "animated_gif")//图片
                            {
                                media.media_type = MediaType.Image;
                                media.url = mediaObject.Value<string>("url");
                                //直接访问media_url不能得到原图，图片有不同规格:orig 4096x4096 large medium small
                                var ext = Util.GetExtFromURL(media.url);
                                var idx = media.url.LastIndexOf(".");
                                if (idx != -1)
                                    media.url = media.url.Substring(0, idx);
                                //指定jpg格式，orig规格
                                media.url += $"?format={ext}&name=orig";
                                media.file_name = $"{media.id}.{ext}";
                            }
                            else if (type == "video")//视频
                            {
                                media.media_type = MediaType.Video;
                                //下载码率最大的视频
                                int max_bitrate = -1;
                                foreach (var videoObject in mediaObject["variants"].ToArray())
                                {
                                    int bitrate = videoObject.Value<int>("bitrate");
                                    if (bitrate > max_bitrate)
                                    {
                                        max_bitrate = bitrate;
                                        media.url = videoObject.Value<string>("url");
                                    }
                                }
                                if (string.IsNullOrEmpty(media.url))
                                    throw new TopLevelException("Can't Find Twitter Video URL");
                                var ext = Util.GetExtFromURL(media.url);
                                media.file_name = $"{media.id}.{ext}";
                            }
                            else
                                throw new TopLevelException($"Unknown Media Type:{type}");
                        }

                    next_token =jsonDoc["meta"].Value<string>("next_token");
                    if (complete)
                        break;
                } while (!string.IsNullOrEmpty(next_token));               

                //更新数据库
                Log($"Fetch User(API) @{user_name} " + (complete ? "Complete." : "Break!!") + $":{tweets.Count} Tweets/{medias.Count} Medias");
                await database.AddTweetFull(tweets.Values.ToList());
                await database.AddMediaBase(medias.Values.ToList());
                if (complete)//只有搜到底，才更新latest_tweet_id
                {
                    //转成int64再比较,越晚的tweet的id越大
                    user.search_latest_tweet_id = tweets.Keys.ToList()
                                                .Select<string, Int64>(x => Int64.Parse(x))
                                                .Max().ToString();
                    await database.UpdateUserAPILatestTweet(user);
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
            }
        }
        private async Task SyncBookmarkDirectory()
        {
            var private_files = new HashSet<string>();
            foreach (var media in await database.GetBookmarkedMedia())
            {
                string dir = download_dir_private;
                string file_name = media.file_name;
                string dest = Path.Combine(dir, file_name);
                string tmp = Path.Combine(dir, "_tmp");
                string src = Path.Combine(download_dir_tmp, file_name);
                if (!File.Exists(dest) && File.Exists(src))
                    try
                    {
                        File.Copy(src, tmp, true);
                        File.Move(tmp,dest);
                    }
                    catch (System.IO.IOException) { }//此时文件可能被Explorer的缓存占用,复制不需要立刻完成，因此忽略该异常
                private_files.Add(file_name);
            }
            foreach (var file in Directory.GetFiles(download_dir_private, "*.*"))//下载临时文件
                if (!private_files.Contains(Path.GetFileName(file)))
                    File.Delete(file);
        }
        public async Task<JObject> GetJsonAsync(string text, bool is_pub = true)
        {
            return ParseJson(await GetAsync(text, is_pub));
        }
        public JObject ParseJson(string text)
        {
            if (text != null && text != "")
                return (JObject)JsonConvert.DeserializeObject(text);
            return (JObject)JsonConvert.DeserializeObject("{\"NetError\":true,\"error\":true}");
        }
        public async Task<string> GetAsync(string url, bool is_pub = true)
        {
            var httpClient = is_pub ? httpClient_pub : httpClient_myapi;
            for (int try_ct = 5; try_ct >= 0; --try_ct)
            {
                try
                {
                    //Log("Begin " + try_ct.ToString() + " " + (url.Length>150?url.Substring(0, 150) :url));
                    if (string.IsNullOrEmpty(url))
                        throw new ArgumentNullException("url");
                    //if (!url.StartsWith("https"))
                    //    throw new ArgumentException("Not SSL");
                    using (HttpResponseMessage response = await httpClient.GetAsync(url))
                    {
                        //Too Many Requests
                        //返回头中包含x-rate-limit-*规定了调用频率，频率重置周期是15分钟，超过频率会返回429，此时等待15分钟后重试
                        if ((int)response.StatusCode == 429 
                            && response.Headers.Contains("x-rate-limit-remaining"))
                        {
                            await Task.Delay(new TimeSpan(0, 15, 0));
                            continue;
                        }
                        //if (response.StatusCode == HttpStatusCode.NotFound)
                        //    return await response.Content.ReadAsStringAsync();
                        var result = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode)
                            throw new Exception($"HTTP Not Success{result}");
                        if(is_pub)
                            UpdateCSRFToken();
                        return result;
                    }
                }
                catch (Exception e)
                {
                    string msg = e.Message;//e.InnerException.InnerException.Message;
                    if (try_ct < 1)
                        LogError(msg + "Re Try " + try_ct.ToString() + " On :" + url);
                    if (try_ct == 0)
                        throw;
                }
            }
            return null;
        }
        public async Task<string> PostAsync(string url, string data, bool is_pub=true)
        {
            var httpClient = is_pub ? httpClient_pub : httpClient_myapi;
            for (int try_ct = 5; try_ct >= 0; --try_ct)
            {
                try
                {
                    //Log("Begin " + try_ct.ToString() + " " + (url.Length>150?url.Substring(0, 150) :url));
                    if (string.IsNullOrEmpty(url))
                        throw new ArgumentNullException("url");
                    //if (!url.StartsWith("https"))
                    //    throw new ArgumentException("Not SSL");
                    using(var content=new StringContent(data,Encoding.UTF8,"application/json"))
                        using (HttpResponseMessage response = await httpClient.PostAsync(url,content))
                        {
                            //Too Many Requests
                            //返回头中包含x-rate-limit-*规定了调用频率，频率重置周期是15分钟，超过频率会返回429，此时等待15分钟后重试
                            if ((int)response.StatusCode == 429
                                && response.Headers.Contains("x-rate-limit-remaining"))
                            {
                                await Task.Delay(new TimeSpan(0, 15, 0));
                                continue;
                            }
                            //if (response.StatusCode == HttpStatusCode.NotFound)
                            //    return await response.Content.ReadAsStringAsync();
                            var result = await response.Content.ReadAsStringAsync();
                            if (!response.IsSuccessStatusCode)
                                throw new Exception($"HTTP Not Success{result}");
                        //正常
                        if(is_pub)
                            UpdateCSRFToken();
                        return result;
                        }
                }
                catch (Exception e)
                {
                    string msg = e.Message;//e.InnerException.InnerException.Message;
                    if (try_ct < 1)
                        LogError(msg + "Re Try " + try_ct.ToString() + " On :" + url);
                    if (try_ct == 0)
                        throw;
                }
            }
            return null;
        }
        public void Dispose()
        {
            httpClient_pub.Dispose();
        }
    }
}
