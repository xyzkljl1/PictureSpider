using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PictureSpider.Twitter
{
    /*
     * Official X API is no longer a good free entry point for this project.
     * This module uses browser cookies with X Web GraphQL and throttled timeline paging.
     */
    public partial class Server : BaseServerWithDB<Database>, IBindHandleProvider, IDisposable
    {
        // X Web 前端使用的公共 bearer，不是用户私有 API token。
        private const string PublicBearerToken = "AAAAAAAAAAAAAAAAAAAAANRILgAAAAAAnNwIzUejRCOuH5E6I8xnZz4puTs%3D1Zv7ttfk8LF81IUq16cHjhLTvJu4FA33AGWWjCpTnA";
        private const int MaxPagesPerUserRun = 200;
        // 下载任务可能长期堆积，按小时小批量消化，避免一次性打满请求。
        private const int DownloadLimitPerRun = 80;

        public BindHandleProvider provider { get; set; } = new BindHandleProvider();

        private readonly Config config;
        private readonly HttpClient httpClient;
        private readonly Aria2DownloadQueue downloader;
        private readonly Random random = new Random();
        private readonly string request_proxy;
        private readonly string download_dir_root;
        private readonly string download_dir_tmp;
        private readonly string download_dir_private;
        private string authCookie = "";
        private string authUserAgent = "";
        private string csrfToken = "";
        private DateTime nextXRequestAt = DateTime.MinValue;
        private DateTime nextMediaRequestAt = DateTime.MinValue;
        private string userByScreenNameQueryId = "IGgvgiOx4QZndDHuD3x9TQ";
        private string userMediaQueryId = "9EovraBTXJYGSEQXZqlLmQ";

        public Server(Config config) : base(config.TwitterConnectStr)
        {
            this.config = config;
            logPrefix = "T";

            request_proxy = config.ProxyGo;
            download_dir_root = config.TwitterDownloadDir;
            download_dir_tmp = Path.Combine(download_dir_root, "tmp");
            download_dir_private = Path.Combine(download_dir_root, "private");
            Util.TouchDir(download_dir_root, download_dir_tmp, download_dir_private);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler
            {
                MaxConnectionsPerServer = 8,
                // Cookie 需要随插件同步的 authCookie 精确写入请求头，禁用 CookieContainer 的隐式状态。
                UseCookies = false,
                Proxy = string.IsNullOrWhiteSpace(request_proxy) ? null : new WebProxy(request_proxy, false),
                AutomaticDecompression = DecompressionMethods.All
            };
            handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            httpClient = new HttpClient(handler);
            httpClient.Timeout = new TimeSpan(0, 0, 60);
            downloader = new Aria2DownloadQueue(Downloader.DownloaderPostfix.Twitter, request_proxy, "https://x.com/", 1, 10);
        }

        public override async Task Init()
        {
            await database.Database.EnsureCreatedAsync();
            await database.EnsureTwitterSchemaAsync();
#if !DEBUG
            await LoadAuthAsync();
            if (string.IsNullOrWhiteSpace(authCookie))
                LogError("Twitter auth is empty. Start PixivHelper in Chrome to send X/Twitter cookies first.");
#pragma warning disable CS4014
            Task.Run(RunSchedule);
#pragma warning restore CS4014
#endif
        }

        public override void SetReaded(ExplorerFileBase file)
        {
            var media = (file as ExplorerFile)?.media;
            if (media is null)
                return;
            var dbMedia = database.Medias.FirstOrDefault(x => x.id == media.id);
            if (dbMedia is null)
                return;
            dbMedia.readed = file.readed;
            database.SaveChanges();
        }

        public override async Task<List<ExplorerQueue>> GetExplorerQueues()
        {
            var ret = new List<ExplorerQueue>
            {
                new ExplorerQueue(ExplorerQueue.QueueType.FavR, "0", "FavR"),
                new ExplorerQueue(ExplorerQueue.QueueType.MainR, "0", "MainR")
            };
            foreach (var user in await database.GetUsers(false, true))
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
            return medias.Where(media => media.media_type == MediaType.Image)
                         .Select(media => (ExplorerFileBase)new ExplorerFile(media, download_dir_tmp))
                         .ToList();
        }

        public override void SetBookmarked(ExplorerFileBase file)
        {
            var media = (file as ExplorerFile)?.media;
            if (media is null)
                return;
            var dbMedia = database.Medias.FirstOrDefault(x => x.id == media.id);
            if (dbMedia is null)
                return;
            dbMedia.bookmarked = file.bookmarked;
            database.SaveChanges();
        }

        public override BaseUser GetUserById(string id)
        {
            var user = database.Users.FirstOrDefault(x => x.id == id);
            user?.InitDisplayText();
            return user;
        }

        public override void SetUserFollowOrQueue(BaseUser user)
        {
            if (user is not User twitterUser)
                return;
            var dbUser = database.Users.FirstOrDefault(x => x.id == twitterUser.id);
            if (dbUser is null)
                return;
            dbUser.followed = twitterUser.followed;
            dbUser.queued = twitterUser.queued;
            database.SaveChanges();
        }

        private async Task RunSchedule()
        {
            int interval_ct = 0;
            // 抓取和下载分离：抓取每天一次，下载每小时一小批。
            var interval = new TimeSpan(1, 0, 0);
            do
            {
                try
                {
                    await RunScheduleOnce(interval_ct);
                }
                catch (Exception e)
                {
                    LogError(FormatException(e));
                }
                await Task.Delay(interval);
                interval_ct++;
            }
            while (true);
        }

        private async Task RunScheduleOnce(int interval_ct)
        {
            Log($"Twitter schedule tick {interval_ct}");
            await LoadAuthAsync();
            if (string.IsNullOrWhiteSpace(authCookie))
            {
                // 允许程序先启动；等待 Chrome 插件把 X/Twitter cookie 写入数据库。
                Log("Twitter auth empty, schedule waits for listener cookie");
                return;
            }
            if (interval_ct % 24 == 0)
            {
                var users = await database.GetUsers(true, true);
                Log($"Twitter fetch users count={users.Count}");
                foreach (var user in users)
                {
                    await FetchTweetsByUserWeb(user.name, user.api_latest_tweet_id);
                    await Task.Delay(TimeSpan.FromSeconds(random.Next(20, 45)));
                }
                Log("Twitter sync bookmark directory");
                await SyncBookmarkDirectory();
            }
            Log("Twitter download waiting media");
            await DownloadWaitingMedia(DownloadLimitPerRun);
        }

        private async Task FetchTweetsByUserWeb(string user_name, string since_tweet_id)
        {
            Log($"Fetch User(Web) @{NormalizeScreenName(user_name)} since={since_tweet_id}");
            var user = await FetchUserByName(user_name);
            if (user is null)
                return;

            // 复用历史 api_latest_tweet_id 字段作为 Web GraphQL 的增量边界。
            long.TryParse(since_tweet_id, out var sinceTweetId);
            var maxTweetId = sinceTweetId;
            var tweets = new Dictionary<string, Tweet>();
            var medias = new Dictionary<string, Media>();
            var cursor = "";
            var complete = false;
            var page = 0;

            while (!complete && page < MaxPagesPerUserRun)
            {
                page++;
                var json = await FetchUserMediaPage(user.id, cursor);
                var pageTweets = ExtractTweetsAndMedia(json, user.id, user.name).ToList();
                foreach (var item in pageTweets)
                {
                    if (long.TryParse(item.Tweet.id, out var tweetId))
                    {
                        if (tweetId <= sinceTweetId && sinceTweetId > 0)
                        {
                            complete = true;
                            continue;
                        }
                        if (tweetId > maxTweetId)
                            maxTweetId = tweetId;
                    }
                    tweets[item.Tweet.id] = item.Tweet;
                    foreach (var media in item.Medias)
                        medias[media.id] = media;
                }

                // 每页落库一次，长账号中途失败时也能保留已解析出的结果。
                await UpsertTweetsAndMedia(tweets.Values.ToList(), medias.Values.ToList());
                tweets.Clear();
                medias.Clear();

                cursor = FindBottomCursor(json);
                if (string.IsNullOrWhiteSpace(cursor))
                    complete = true;
            }

            if (maxTweetId > sinceTweetId)
            {
                var dbUser = await database.Users.FirstOrDefaultAsync(x => x.id == user.id);
                if (dbUser is not null)
                {
                    dbUser.api_latest_tweet_id = maxTweetId.ToString();
                    await database.SaveChangesAsync();
                }
            }
            Log($"Fetch User(Web) @{user.name} {(complete ? "Complete" : "Paused")} pages={page} latest={maxTweetId}");
        }

        private async Task<User> FetchUserByName(string user_name)
        {
            user_name = NormalizeScreenName(user_name);
            Log($"Fetch user info @{user_name}");
            var json = await GraphQlGet(userByScreenNameQueryId, "UserByScreenName",
                new Dictionary<string, object>
                {
                    ["screen_name"] = user_name
                },
                UserFeatures(),
                new Dictionary<string, object>
                {
                    ["withAuxiliaryUserLabels"] = true
                });

            var result = json.SelectToken("data.user.result") as JObject;
            var id = result?["rest_id"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(id))
            {
                LogError($"Can't fetch Twitter user @{user_name}");
                return null;
            }

            var legacy = result["legacy"] as JObject;
            var user = new User
            {
                id = id,
                name = legacy?["screen_name"]?.ToString() ?? user_name,
                nick_name = legacy?["name"]?.ToString() ?? user_name
            };
            user.displayId = user.id;
            user.displayText = user.name;
            return await UpsertUser(user);
        }

        private async Task<User> UpsertUser(User remoteUser)
        {
            var existing = await database.Users.FirstOrDefaultAsync(x => x.id == remoteUser.id);
            var existingByName = await database.Users.FirstOrDefaultAsync(x => x.name == remoteUser.name);
            if (existing is null && existingByName is not null && existingByName.id.StartsWith("name:", StringComparison.Ordinal))
            {
                remoteUser.followed = existingByName.followed;
                remoteUser.queued = existingByName.queued;
                remoteUser.api_latest_tweet_id = existingByName.api_latest_tweet_id;
                remoteUser.search_latest_tweet_id = existingByName.search_latest_tweet_id;
                database.Users.Remove(existingByName);
                await database.SaveChangesAsync();
            }
            else if (existing is not null)
            {
                existing.name = remoteUser.name;
                existing.nick_name = remoteUser.nick_name;
                existing.displayId = remoteUser.id;
                existing.displayText = remoteUser.name;
                await database.SaveChangesAsync();
                return existing;
            }

            remoteUser.displayId = remoteUser.id;
            remoteUser.displayText = remoteUser.name;
            database.Users.Add(remoteUser);
            await database.SaveChangesAsync();
            return remoteUser;
        }

        private async Task<JObject> FetchUserMediaPage(string userId, string cursor)
        {
            var variables = new Dictionary<string, object>
            {
                ["userId"] = userId,
                ["count"] = 40,
                ["includePromotedContent"] = false,
                ["withClientEventToken"] = false,
                ["withBirdwatchNotes"] = false,
                ["withVoice"] = true
            };
            if (!string.IsNullOrWhiteSpace(cursor))
                variables["cursor"] = cursor;

            return await GraphQlGet(userMediaQueryId, "UserMedia", variables, TimelineFeatures(),
                new Dictionary<string, object>
                {
                    ["withArticlePlainText"] = false
                });
        }

        private async Task<JObject> GraphQlGet(string queryId, string operationName, Dictionary<string, object> variables,
            Dictionary<string, object> features, Dictionary<string, object> fieldToggles)
        {
            var url = $"https://x.com/i/api/graphql/{queryId}/{operationName}";
            var query = new Dictionary<string, string>
            {
                ["variables"] = JsonConvert.SerializeObject(variables),
                ["features"] = JsonConvert.SerializeObject(features),
                ["fieldToggles"] = JsonConvert.SerializeObject(fieldToggles)
            };
            var requestUrl = QueryHelpers.AddQueryString(url, query);
            try
            {
                var text = await SendXRequest(requestUrl, true, $"https://x.com/");
                return JObject.Parse(text);
            }
            catch (Exception)
            {
                // X 前端会不定期更换 queryId；失败后从当前 JS bundle 重新发现再重试。
                await DiscoverGraphQlOperationIds();
                var retryUrl = QueryHelpers.AddQueryString($"https://x.com/i/api/graphql/{queryIdFor(operationName)}/{operationName}", query);
                var text = await SendXRequest(retryUrl, true, $"https://x.com/");
                return JObject.Parse(text);
            }
        }

        private string queryIdFor(string operationName)
        {
            if (operationName == "UserByScreenName")
                return userByScreenNameQueryId;
            if (operationName == "UserMedia")
                return userMediaQueryId;
            throw new ArgumentException(operationName);
        }

        private async Task DiscoverGraphQlOperationIds()
        {
            Log("Discover Twitter GraphQL operation ids");
            var html = await SendXRequest("https://x.com/home", false, "https://x.com/");
            // operationName/queryId 写在前端 bundle 中，限制扫描数量避免一次加载过多脚本。
            var scripts = Regex.Matches(html, @"https://abs\.twimg\.com/responsive-web/client-web/[^""']+?\.js")
                .Select(m => m.Value)
                .Distinct()
                .Take(20)
                .ToList();
            foreach (var script in scripts)
            {
                var js = await SendXRequest(script, false, "https://x.com/");
                foreach (Match match in Regex.Matches(js, @"queryId:""([^""]+)"",operationName:""([^""]+)"""))
                {
                    var operationName = match.Groups[2].Value;
                    if (operationName == "UserByScreenName")
                        userByScreenNameQueryId = match.Groups[1].Value;
                    else if (operationName == "UserMedia")
                        userMediaQueryId = match.Groups[1].Value;
                }
            }
        }

        private async Task<string> SendXRequest(string url, bool authorize, string referer)
        {
            for (int try_ct = 0; try_ct < 3; try_ct++)
            {
                await ThrottleXRequest();
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyCommonHeaders(request, authorize, referer);
                using var response = await httpClient.SendAsync(request);
                var text = await response.Content.ReadAsStringAsync();
                if ((int)response.StatusCode == 429)
                {
                    // 遵守 Retry-After，避免在限流窗口内持续重试。
                    var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(15);
                    Log($"Twitter rate limited, wait {delay.TotalSeconds:0}s");
                    await Task.Delay(delay);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Twitter HTTP {(int)response.StatusCode}: {TrimForLog(text)}");
                return text;
            }
            throw new Exception("Twitter request retry exhausted.");
        }

        private void ApplyCommonHeaders(HttpRequestMessage request, bool authorize, string referer)
        {
            request.Headers.UserAgent.ParseAdd(string.IsNullOrWhiteSpace(authUserAgent) ? config.TwitterUserAgent : authUserAgent);
            request.Headers.Accept.ParseAdd("*/*");
            request.Headers.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8,ja;q=0.7");
            request.Headers.TryAddWithoutValidation("cookie", authCookie);
            request.Headers.TryAddWithoutValidation("x-csrf-token", csrfToken);
            request.Headers.TryAddWithoutValidation("x-twitter-active-user", "yes");
            request.Headers.TryAddWithoutValidation("x-twitter-auth-type", "OAuth2Session");
            request.Headers.TryAddWithoutValidation("x-twitter-client-language", "en");
            if (authorize)
                request.Headers.TryAddWithoutValidation("authorization", $"Bearer {PublicBearerToken}");
            if (!string.IsNullOrWhiteSpace(referer))
                request.Headers.Referrer = new Uri(referer);
        }

        private async Task ThrottleXRequest()
        {
            var now = DateTime.UtcNow;
            if (nextXRequestAt > now)
                await Task.Delay(nextXRequestAt - now);
            // Timeline/GraphQL 比静态媒体更容易触发限制，间隔保守一些。
            nextXRequestAt = DateTime.UtcNow.AddSeconds(random.Next(7, 14));
        }

        private async Task ThrottleMediaRequest()
        {
            var now = DateTime.UtcNow;
            if (nextMediaRequestAt > now)
                await Task.Delay(nextMediaRequestAt - now);
            nextMediaRequestAt = DateTime.UtcNow.AddSeconds(random.Next(2, 5));
        }

        private IEnumerable<(Tweet Tweet, List<Media> Medias)> ExtractTweetsAndMedia(JToken json, string userId, string userName)
        {
            foreach (var tweetResult in FindTweetResults(json))
            {
                var legacy = tweetResult["legacy"] as JObject;
                if (legacy is null)
                    continue;
                if (legacy["user_id_str"] is not null && legacy["user_id_str"].ToString() != userId)
                    continue;

                var tweetId = tweetResult["rest_id"]?.ToString() ?? legacy["id_str"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(tweetId))
                    continue;

                var tweet = new Tweet
                {
                    id = tweetId,
                    created_at = ParseTwitterDate(legacy["created_at"]?.ToString()),
                    full_text = legacy["full_text"]?.ToString() ?? legacy["text"]?.ToString() ?? "",
                    user_id = userId,
                    url = $"https://x.com/{userName}/status/{tweetId}"
                };

                var mediaList = new List<Media>();
                var mediaArray = legacy.SelectToken("extended_entities.media") as JArray;
                if (mediaArray is null)
                    continue;
                var mediaIndex = 0;
                foreach (var mediaObject in mediaArray.OfType<JObject>())
                {
                    mediaIndex++;
                    var media = ExtractMedia(mediaObject, tweet, userName, mediaIndex);
                    if (media is not null)
                        mediaList.Add(media);
                }
                if (mediaList.Count > 0)
                    yield return (tweet, mediaList);
            }
        }

        private Media ExtractMedia(JObject mediaObject, Tweet tweet, string userName, int mediaIndex)
        {
            var type = mediaObject["type"]?.ToString() ?? "";
            var mediaId = mediaObject["id_str"]?.ToString() ?? mediaObject["media_key"]?.ToString() ?? $"{tweet.id}_{mediaIndex}";
            if (type == "photo")
            {
                var mediaUrl = mediaObject["media_url_https"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(mediaUrl))
                    return null;
                var ext = Path.GetExtension(new Uri(mediaUrl).AbsolutePath).TrimStart('.').ToLowerInvariant();
                var idx = mediaUrl.LastIndexOf(".", StringComparison.Ordinal);
                if (idx >= 0)
                    mediaUrl = mediaUrl.Substring(0, idx);
                var fileExt = string.IsNullOrWhiteSpace(ext) ? "jpg" : ext;
                // 原图需要去掉路径扩展名后显式指定 format 和 name=orig。
                return new Media
                {
                    id = mediaId,
                    key = mediaObject["media_key"]?.ToString() ?? mediaId,
                    user_id = tweet.user_id,
                    tweet_id = tweet.id,
                    url = $"{mediaUrl}?format={fileExt}&name=orig",
                    expand_url = $"https://x.com/{userName}/status/{tweet.id}/photo/{mediaIndex}",
                    media_type = MediaType.Image,
                    file_name = $"{tweet.id}_{mediaId}.{fileExt}".ReplaceInvalidCharInFilenameWithReturnValue()
                };
            }
            if (type == "video" || type == "animated_gif")
            {
                var variants = mediaObject.SelectToken("video_info.variants") as JArray;
                var bestUrl = "";
                var bestBitrate = -1;
                foreach (var variant in variants?.OfType<JObject>() ?? Enumerable.Empty<JObject>())
                {
                    var variantUrl = variant["url"]?.ToString() ?? "";
                    // 跳过流式 m3u8，优先保存最高码率的直链 mp4，便于本地浏览。
                    if (variantUrl.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var bitrate = variant["bitrate"]?.Value<int>() ?? 0;
                    if (bitrate > bestBitrate)
                    {
                        bestBitrate = bitrate;
                        bestUrl = variantUrl;
                    }
                }
                if (string.IsNullOrWhiteSpace(bestUrl))
                    return null;
                var ext = Path.GetExtension(new Uri(bestUrl).AbsolutePath).TrimStart('.').ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(ext))
                    ext = "mp4";
                return new Media
                {
                    id = mediaId,
                    key = mediaObject["media_key"]?.ToString() ?? mediaId,
                    user_id = tweet.user_id,
                    tweet_id = tweet.id,
                    url = bestUrl,
                    expand_url = $"https://x.com/{userName}/status/{tweet.id}/video/{mediaIndex}",
                    media_type = MediaType.Video,
                    file_name = $"{tweet.id}_{mediaId}.{ext}".ReplaceInvalidCharInFilenameWithReturnValue()
                };
            }
            return null;
        }

        private IEnumerable<JObject> FindTweetResults(JToken token)
        {
            if (token is JObject obj)
            {
                if (obj["legacy"] is JObject legacy && legacy.SelectToken("extended_entities.media") is JArray)
                    yield return obj;
                foreach (var property in obj.Properties())
                    foreach (var result in FindTweetResults(property.Value))
                        yield return result;
            }
            else if (token is JArray array)
            {
                foreach (var child in array)
                    foreach (var result in FindTweetResults(child))
                        yield return result;
            }
        }

        private string FindBottomCursor(JToken token)
        {
            foreach (var obj in FindObjects(token))
            {
                if (obj["cursorType"]?.ToString() == "Bottom")
                {
                    var value = obj["value"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
            return "";
        }

        private IEnumerable<JObject> FindObjects(JToken token)
        {
            if (token is JObject obj)
            {
                yield return obj;
                foreach (var property in obj.Properties())
                    foreach (var result in FindObjects(property.Value))
                        yield return result;
            }
            else if (token is JArray array)
            {
                foreach (var child in array)
                    foreach (var result in FindObjects(child))
                        yield return result;
            }
        }

        private async Task UpsertTweetsAndMedia(List<Tweet> tweets, List<Media> medias)
        {
            foreach (var tweet in tweets)
            {
                var old = await database.Tweets.FirstOrDefaultAsync(x => x.id == tweet.id);
                if (old is null)
                    database.Tweets.Add(tweet);
                else
                {
                    old.created_at = tweet.created_at;
                    old.full_text = tweet.full_text;
                    old.user_id = tweet.user_id;
                    old.url = tweet.url;
                }
            }
            // 旧数据库上 media.tweet_id 可能有外键约束；先落 tweet，避免 EF 在同一批次中先插 media。
            await database.SaveChangesAsync();
            foreach (var media in medias)
            {
                var old = await database.Medias.FirstOrDefaultAsync(x => x.id == media.id);
                if (old is null)
                    database.Medias.Add(media);
                else
                {
                    var urlChanged = old.url != media.url;
                    old.key = media.key;
                    old.user_id = media.user_id;
                    old.tweet_id = media.tweet_id;
                    old.url = media.url;
                    old.expand_url = media.expand_url;
                    old.media_type = media.media_type;
                    old.file_name = media.file_name;
                    // URL 变化时旧文件可能对应过期资源，重置后让下载队列重新获取。
                    if (urlChanged)
                        old.downloaded = false;
                }
            }
            await database.SaveChangesAsync();
        }

        private async Task DownloadWaitingMedia(int limit)
        {
            // 串行下载，配合 DownloadLimitPerRun 和 ThrottleMediaRequest 控制堆积任务的释放速度。
            var medias = await database.GetWaitingDownloadMedia(limit);
            Log($"Download Waiting Media count={medias.Count} limit={limit}");
            var success = 0;
            foreach (var media in medias)
            {
                var path = Path.Combine(download_dir_tmp, media.file_name);
                if (File.Exists(path))
                {
                    // 兼容旧文件迁移：文件已存在时直接恢复数据库下载状态。
                    Log($"Download skip existing {media.file_name}");
                    media.downloaded = true;
                    success++;
                    continue;
                }
                try
                {
                    Log($"Download media {media.id} => {media.file_name}");
                    await DownloadMediaFile(media.url, path, media.expand_url);
                    media.downloaded = File.Exists(path);
                    if (media.downloaded)
                        success++;
                }
                catch (Exception e)
                {
                    LogError($"Download fail {media.url}: {FormatException(e)}");
                }
            }
            if (medias.Count > 0)
            {
                await database.SaveChangesAsync();
                Log($"Download Done:{success}/{medias.Count}");
            }
        }

        private async Task DownloadMediaFile(string url, string path, string referer)
        {
            await ThrottleMediaRequest();
            var dir = Path.GetDirectoryName(path);
            Util.TouchDir(dir);
            var fileName = Path.GetFileName(path);
            var options = new DownloadRequestOptions
            {
                Referer = string.IsNullOrWhiteSpace(referer) ? "https://x.com/" : referer,
                UserAgent = string.IsNullOrWhiteSpace(authUserAgent) ? config.TwitterUserAgent : authUserAgent,
                Split = 1,
                MaxConnectionPerServer = 1
            };
            Log($"Aria2 download start {fileName}");
            var added = await downloader.Add(url, dir, fileName, options);
            if (!added)
                throw new Exception("Aria2 add download task failed.");
            await downloader.WaitForAll();
            if (!File.Exists(path))
                throw new FileNotFoundException("Aria2 finished but target file was not found.", path);
            Log($"Aria2 download done {fileName}");
        }

        private async Task SyncBookmarkDirectory()
        {
            var private_files = new HashSet<string>();
            foreach (var media in await database.GetBookmarkedMedia())
            {
                var dest = Path.Combine(download_dir_private, media.file_name);
                var tmp = Path.Combine(download_dir_private, "_tmp");
                var src = Path.Combine(download_dir_tmp, media.file_name);
                if (!File.Exists(dest) && File.Exists(src))
                    try
                    {
                        File.Copy(src, tmp, true);
                        File.Move(tmp, dest, true);
                    }
                    catch (IOException) { }
                private_files.Add(media.file_name);
            }
            foreach (var file in Directory.GetFiles(download_dir_private, "*.*"))
                if (!private_files.Contains(Path.GetFileName(file)))
                    File.Delete(file);
        }

        private async Task LoadAuthAsync()
        {
            // Cookie 由 Chrome 插件写入 auth_state；不再从本地 twitter_auth.json 读取。
            var state = await database.AuthStates.FirstOrDefaultAsync(x => x.Id == "default");
            if (state is not null && !string.IsNullOrWhiteSpace(state.Cookie))
            {
                authCookie = state.Cookie;
                authUserAgent = string.IsNullOrWhiteSpace(state.UserAgent) ? config.TwitterUserAgent : state.UserAgent;
                csrfToken = ExtractCt0(authCookie);
            }
        }

        private async Task SaveAuthState()
        {
            var state = await database.AuthStates.FirstOrDefaultAsync(x => x.Id == "default");
            if (state is null)
            {
                state = new AuthState { Id = "default" };
                database.AuthStates.Add(state);
            }
            state.Cookie = authCookie;
            state.UserAgent = authUserAgent;
            state.UpdatedAt = DateTime.UtcNow;
            await database.SaveChangesAsync();
        }

        private string ExtractCt0(string cookie)
        {
            foreach (var part in cookie.Split(';'))
            {
                var idx = part.IndexOf('=');
                if (idx <= 0)
                    continue;
                if (part.Substring(0, idx).Trim() == "ct0")
                    return part.Substring(idx + 1).Trim();
            }
            return "";
        }

        public override async Task ListenerUtil_SetCookie(string cookie, string userAgent)
        {
            if (string.IsNullOrWhiteSpace(cookie))
                return;
            // ListenerServer 根据 site=twitter/x 把插件同步的浏览器凭据路由到这里。
            authCookie = cookie;
            csrfToken = ExtractCt0(cookie);
            if (!string.IsNullOrWhiteSpace(userAgent))
                authUserAgent = userAgent;
            else if (string.IsNullOrWhiteSpace(authUserAgent))
                authUserAgent = config.TwitterUserAgent;
            await SaveAuthState();
            Log("Twitter auth updated from listener");
        }

        public override bool ListenerUtil_IsValidUrl(string url)
        {
            return url.StartsWith("https://x.com/") || url.StartsWith("https://twitter.com/");
        }

        public override async Task<bool> ListenerUtil_FollowUser(string url)
        {
            var screenName = ExtractScreenNameFromUrl(url);
            if (string.IsNullOrWhiteSpace(screenName))
                return false;
            return await AddQueuedUser(screenName);
        }

        private async Task<bool> AddQueuedUser(string screenName)
        {
            screenName = NormalizeScreenName(screenName);
            var user = await database.Users.FirstOrDefaultAsync(x => x.name == screenName);
            if (user is null)
            {
                try
                {
                    user = await FetchUserByName(screenName);
                }
                catch
                {
                    // 凭据暂时不可用时仍保留用户名占位，后续调度拿到 cookie 后会补齐远端 id。
                    user = new User($"name:{screenName.ToLowerInvariant()}", screenName, screenName);
                    database.Users.Add(user);
                    await database.SaveChangesAsync();
                }
            }
            if (user.followed || user.queued)
            {
                Log($"Twitter user already followed or queued @{user.name}");
                return true;
            }
            user.queued = true;
            await database.SaveChangesAsync();
            Log($"Twitter user queued @{user.name}");
            return true;
        }

        private string ExtractScreenNameFromUrl(string url)
        {
            var regex = new Regex(@"https://(?:x|twitter)\.com/([^/?#]+)", RegexOptions.IgnoreCase);
            var match = regex.Match(url);
            if (!match.Success)
                return "";
            var screenName = match.Groups[1].Value;
            if (screenName is "i" or "home" or "search" or "notifications" or "messages")
                return "";
            return NormalizeScreenName(screenName);
        }

        private string NormalizeScreenName(string screenName)
        {
            screenName = (screenName ?? "").Trim();
            if (screenName.StartsWith("@"))
                screenName = screenName.Substring(1);
            return screenName.Trim('/');
        }

        private DateTime ParseTwitterDate(string text)
        {
            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
                return dto.UtcDateTime;
            return DateTime.UtcNow;
        }

        private string TrimForLog(string text)
        {
            text = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            return text.Length > 500 ? text.Substring(0, 500) : text;
        }

        private string FormatException(Exception exception)
        {
            var messages = new List<string>();
            for (var current = exception; current is not null; current = current.InnerException)
                messages.Add(current.Message);
            return string.Join(" | Inner: ", messages);
        }

        private Dictionary<string, object> UserFeatures()
        {
            return new Dictionary<string, object>
            {
                ["hidden_profile_subscriptions_enabled"] = true,
                ["payments_enabled"] = false,
                ["profile_label_improvements_pcf_label_in_post_enabled"] = true,
                ["rweb_tipjar_consumption_enabled"] = true,
                ["verified_phone_label_enabled"] = false,
                ["subscriptions_verification_info_is_identity_verified_enabled"] = true,
                ["subscriptions_verification_info_verified_since_enabled"] = true,
                ["highlights_tweets_tab_ui_enabled"] = true,
                ["responsive_web_twitter_article_notes_tab_enabled"] = true,
                ["subscriptions_feature_can_gift_premium"] = true,
                ["creator_subscriptions_tweet_preview_api_enabled"] = true,
                ["responsive_web_graphql_skip_user_profile_image_extensions_enabled"] = false,
                ["responsive_web_graphql_timeline_navigation_enabled"] = true
            };
        }

        private Dictionary<string, object> TimelineFeatures()
        {
            return new Dictionary<string, object>
            {
                ["rweb_video_screen_enabled"] = false,
                ["payments_enabled"] = false,
                ["profile_label_improvements_pcf_label_in_post_enabled"] = true,
                ["rweb_tipjar_consumption_enabled"] = true,
                ["verified_phone_label_enabled"] = false,
                ["creator_subscriptions_tweet_preview_api_enabled"] = true,
                ["responsive_web_graphql_timeline_navigation_enabled"] = true,
                ["responsive_web_graphql_skip_user_profile_image_extensions_enabled"] = false,
                ["premium_content_api_read_enabled"] = false,
                ["communities_web_enable_tweet_community_results_fetch"] = true,
                ["c9s_tweet_anatomy_moderator_badge_enabled"] = true,
                ["responsive_web_grok_analyze_button_fetch_trends_enabled"] = false,
                ["responsive_web_grok_analyze_post_followups_enabled"] = true,
                ["responsive_web_jetfuel_frame"] = false,
                ["responsive_web_grok_share_attachment_enabled"] = true,
                ["articles_preview_enabled"] = true,
                ["responsive_web_edit_tweet_api_enabled"] = true,
                ["graphql_is_translatable_rweb_tweet_is_translatable_enabled"] = true,
                ["view_counts_everywhere_api_enabled"] = true,
                ["longform_notetweets_consumption_enabled"] = true,
                ["responsive_web_twitter_article_tweet_consumption_enabled"] = true,
                ["tweet_awards_web_tipping_enabled"] = false,
                ["responsive_web_grok_show_grok_translated_post"] = false,
                ["responsive_web_grok_analysis_button_from_backend"] = false,
                ["creator_subscriptions_quote_tweet_preview_enabled"] = false,
                ["freedom_of_speech_not_reach_fetch_enabled"] = true,
                ["standardized_nudges_misinfo"] = true,
                ["tweet_with_visibility_results_prefer_gql_limited_actions_policy_enabled"] = true,
                ["longform_notetweets_rich_text_read_enabled"] = true,
                ["longform_notetweets_inline_media_enabled"] = true,
                ["responsive_web_grok_image_annotation_enabled"] = true,
                ["responsive_web_enhance_cards_enabled"] = false
            };
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
