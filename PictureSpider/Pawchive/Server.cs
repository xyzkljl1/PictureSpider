using Microsoft.AspNetCore.WebUtilities;
using Microsoft.ClearScript.V8;
using Microsoft.EntityFrameworkCore;
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
using CG.Web.MegaApiClient;
using HtmlAgilityPack;
using Mysqlx.Notice;

namespace PictureSpider.Pawchive
{
    public partial class Server : BaseServerWithBackgroundDB<Database>, IDisposable
    {
        private HttpClient httpClient;
        //api/v1/fanbox/user/7349257/posts-legacy
        public static string baseUrl = "https://pawchive.pw";
        public static string baseHost = "pawchive.pw";
        private string baseAPIUrl = "https://pawchive.pw/api/v1";

        private string download_dir_root = "";
        private string download_dir_tmp = "";
        private string download_dir_fav = "";
        Downloader downloader;
        CookieContainer cookies = new CookieContainer();
        MegaApiClient mega;//从downloader借的mega client，用于访问
        GoogleDriveDownloadQueue googleDriveDownloader;
        HttpZipEntriesReader httpZipEntriesReader;
        private List<string> downloadQueue = new List<string>();//计划下载的work key,线程不安全,只在RunSchedule里使用
        public Server(Config config):base(config.PawchiveConnectStr)
        {
            logPrefix = "Paw";

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = 256,
                UseCookies = true,
                CookieContainer = cookies,
                Proxy = new WebProxy(config.ProxyGo, false),
                AutomaticDecompression = DecompressionMethods.All
            };
            handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            httpClient = new HttpClient(handler);
            httpClient.Timeout = new TimeSpan(0, 0, 35);
            // user/posts request 必须使用text/css
            // 使用"text/css,*/*"可以获得response,但是会间歇性403
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/css");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8,ja;q=0.7");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PictureSpider/1.0 (+https://github.com/xyzkljl1/PictureSpider)");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            httpClient.DefaultRequestHeaders.Referrer=new Uri($"{baseUrl}/fanbox/user/7349257");

            download_dir_root = config.PawchiveDownloadDir;
            download_dir_fav = Path.Combine(download_dir_root, "fav");
            download_dir_tmp = Path.Combine(download_dir_root, "tmp");
            var megaDownloader = new MegaDownloadQueue(config.Proxy, config.Proxy);
            mega = megaDownloader.MegaClient;
            googleDriveDownloader = new GoogleDriveDownloadQueue(config.ProxyGo, config.GoogleDriveApiKey);
            downloader = new Downloader(new Aria2DownloadQueue(Downloader.DownloaderPostfix.Pawchive, config.ProxyGo, baseUrl, 1, 30),megaDownloader,googleDriveDownloader);
            httpZipEntriesReader = new HttpZipEntriesReader(config.Proxy, $"file.{baseHost}");

            Util.TouchDir(download_dir_root, download_dir_tmp, download_dir_fav);
        }
        public void Dispose()
        {
            httpClient.Dispose();
            googleDriveDownloader.Dispose();
            httpZipEntriesReader.Dispose();
        }
        public override Task Init()
        {
#if DEBUG
            _ = Task.Run(() => RunSchedule(false));
#else
            _ = Task.Run(() => RunSchedule(true));
#endif
            return Task.CompletedTask;
        }
        private async Task ParseGroupContent(WorkGroup workGroup)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(workGroup.desc ?? "");
            // 补录另一下载源时保留旧编号，避免同名文件覆盖已有外链文件。
            var index = (database.ExternalWorks.Where(x => (x.workGroup.id == workGroup.id || x.workGroup.parentId == workGroup.id)
                && x.workGroup.user.service == workGroup.service)
                .Max(x => (int?)x.index) ?? 0) + 1;
            var anodes = doc.DocumentNode.SelectNodes("//a");
            if (anodes is null)
                return;
            foreach (var anode in anodes)
                if (anode.Attributes["href"] is not null)
                {
                    try
                    {
                        //包含一个mega文件夹
                        //patreon/user/3659577/post/117461502/revision/9878902
                        //<p><img src=\"/05/68/0568e59bd4bfdea28e3b3183046b81668dc841dfd5bcdc847612248a161138f2.webp\"></p><p>Hi guys!</p><p><a href=\"https://mega.nz/folder/CRR1FKwK#TvDSfT70WLo16AppXzIBtQ\" rel=\"noopener noreferrer\">DOWNLOAD</a>&nbsp;(Watermark-free)</p>
                        if (anode.Attributes["href"].Value.StartsWith("https://mega.nz/folder/"))//Mega folder
                        {
                            var rootUri = new Uri(anode.Attributes["href"].Value);
                            foreach (var meganode in await mega.GetNodesFromLinkAsync(rootUri))//GetNodesFromLinkAsync是递归的
                                if (meganode.Type == NodeType.File)
                                {
                                    var ext = Path.GetExtension(meganode.Name);
                                    if (ext.IsVideo())//暂时只处理video
                                    {
                                        var work = new ExternalWork
                                        {
                                            url = GetMegaLink(meganode, rootUri).AbsoluteUri,
                                            id = meganode.Id,
                                            type = ExternalWork.ExternalWorkType.Mega,
                                            name = meganode.Name,
                                            index = index++
                                        };
                                        if (database.ExternalWorks.Count(x => x.id == work.id && x.type == work.type) > 0)
                                            continue;
                                        work.workGroup = workGroup;
                                        database.ExternalWorks.Add(work);
                                        await database.SaveChangesAsync();
                                    }
                                }
                        }
                        else if (anode.Attributes["href"].Value.StartsWith("https://drive.google.com/"))
                        {
                            var url = HtmlEntity.DeEntitize(anode.Attributes["href"].Value);
                            var file = await googleDriveDownloader.GetFileInfoAsync(url);
                            if (Path.GetExtension(file.name).ToLowerInvariant().IsVideo())
                            {
                                var work = new ExternalWork
                                {
                                    url = url,
                                    id = file.id,
                                    type = ExternalWork.ExternalWorkType.GoogleDrive,
                                    name = file.name,
                                    index = index++
                                };
                                if (database.ExternalWorks.Count(x => x.id == work.id && x.type == work.type) > 0)
                                    continue;
                                work.workGroup = workGroup;
                                database.ExternalWorks.Add(work);
                                await database.SaveChangesAsync();
                            }
                        }
                        //单个mega文件 patreon/user/8693043/post/75248472
                        //<p><br></p><p>Dropbox</p><p><a href=\"https://www.dropbox.com/s/fzgnbgsrrxpohv0/55.Nilou%20%28audio%20update%29%202160p.mp4?dl=0\" rel=\"nofollow noopener\" target=\"_blank\">https://www.dropbox.com/s/fzgnbgsrrxpohv0/55.Nilou%20%28audio%20update%29%202160p.mp4?dl=0</a></p><p>MEGA</p><p><a href=\"https://mega.nz/file/YGI0jSzK#A-ZKPcngj9YkWDeo43JfK5o-rIh1Xniz0OSq08XMhU0\" rel=\"nofollow noopener\" target=\"_blank\">https://mega.nz/file/YGI0jSzK#A-ZKPcngj9YkWDeo43JfK5o-rIh1Xniz0OSq08XMhU0</a> </p>
                        else if (anode.Attributes["href"].Value.StartsWith("https://mega.nz/file/"))
                        {
                            var node = await mega.GetNodeFromLinkAsync(new Uri(anode.Attributes["href"].Value));
                            if (node is not null)
                            {
                                var ext = Path.GetExtension(node.Name);
                                if (ext.IsVideo())//暂时只处理video
                                {
                                    var work = new ExternalWork
                                    {
                                        url = anode.Attributes["href"].Value,
                                        id = node.Id,
                                        type = ExternalWork.ExternalWorkType.Mega,
                                        name = node.Name,
                                        index = index++
                                    };
                                    if (database.ExternalWorks.Count(x => x.id == work.id && x.type == work.type) > 0)
                                        continue;
                                    work.workGroup = workGroup;
                                    database.ExternalWorks.Add(work);
                                    await database.SaveChangesAsync();
                                }
                            }
                        }
                    }
                    catch (GoogleDriveResourceUnavailableException e)
                    {
                        LogError($"Invalid ExternalWork {workGroup.service}/{workGroup.id}: {e.Message}");
                    }
                    catch (CG.Web.MegaApiClient.ApiException e) when (
                        e.ApiResultCode == ApiResultCode.ResourceNotExists ||
                        e.ApiResultCode == ApiResultCode.ResourceExpired ||
                        e.ApiResultCode == ApiResultCode.AccessDenied ||
                        e.ApiResultCode == ApiResultCode.CryptographicError ||
                        e.ApiResultCode == ApiResultCode.ResourceAdministrativelyBlocked ||
                        e.ApiResultCode == ApiResultCode.BadArguments)
                    {
                        LogError($"Invalid ExternalWork {workGroup.service}/{workGroup.id}: {e.Message}");
                    }
                    catch (Exception e)
                    {
                        LogError($"Fail ParseGroupContent {workGroup.id}: {e.Message}");
                        throw;
                    }
                }
        }
        private Uri GetMegaLink(INode node,Uri root)
        {
            if (node.Type == NodeType.File)
                return new Uri($"{root.AbsoluteUri}/file/{node.Id}");
            if (node.Type == NodeType.Directory)
                return new Uri($"{root.AbsoluteUri}/folder/{node.Id}");
            return null;
        }
        //获取作者信息
        private async Task FetchUser(string id,string service)
        {
            var doc =await HttpGetJson($"{baseAPIUrl}/{service}/user/{id}/profile");
            if(doc is null||string.IsNullOrWhiteSpace(doc.Value<string>("name")))//只会fetch已关注的作者，不应出现失败
            {
                LogError($"Can't Fetch User {service}/{id}");
                return;
            }
            var user = database.Users.Where(x => x.id == id && x.service == service).ToList().FirstOrDefault();
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
            if (string.IsNullOrWhiteSpace(user.displayText))
                user.displayText = user.displayId;
            if (string.IsNullOrWhiteSpace(user.displayText))
                user.displayText = user.id;
            user.displayText = user.displayText.ReplaceInvalidCharInFilenameWithReturnValue();//还用做目录名
            await database.SaveChangesAsync();
        }
        //获取该user的作品id并插入数据库
        public async Task FetchWorkGroupListByUser(User user)
        {
            // 新用户先补全名称，避免等到每周更新，并在下载前确定作者目录。
            if (string.IsNullOrWhiteSpace(user.displayId) || user.displayId == user.id)
                await FetchUser(user.id, user.service);
            //默认是按时间倒序
            var existedWorkGroupIds = new HashSet<string>();
            if (user.workGroups is not null)//减少查询次数
                existedWorkGroupIds = user.workGroups.Where(x => !x.IsChild).Select(x => x.id).ToHashSet();
            int offset = 0;
            int totalCount = int.MaxValue;
            string service = user.service;
            DateTime latest = user.fetchedTime;
            // Pawchive profile 没有 post_count，遇到空页时结束分页。
            do
            {
                var posts = await HttpGetJArray($"{baseAPIUrl}/{user.service}/user/{user.id}/posts?o={offset}");
                if (posts is null)
                {
                    LogError($"Can't Fetch posts of {user.service}/{user.id}>>{offset}");
                    return;
                }
                if (posts.Count == 0)
                    break;
                offset += posts.Count;
                if (posts.Count < 50)
                    totalCount = offset;
                foreach(var obj in posts)
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
                        //work可能重复，例 patreon/user/3659577/post/109256192包含了两张一样的图片
                        foreach (var attachment in obj["attachments"])
                        {
                            var work = await TryAddWork(attachment, service);//如果work已在别的group或该group之前的附件中存在，则忽略
                            if (work is null)
                                continue;
                            work.index = index++;
                            work.workGroup = workGroup;
                        }
                        await SplitNonImageWorks(workGroup);
                    }
                    else if (date<user.fetchedTime)
                    {
                        totalCount = -1;
                        break;
                    }
                }
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
            ret.name = token.Value<string>("name");
            await database.SaveChangesAsync();
            return ret;
        }
        //如果存在则更新name，否则什么都不做
        public async Task UpdateWork(JToken token, string service, bool without_save = true)
        {
            string path = token.Value<string>("path");
            Work ret = database.Works.Where(x => x.urlPath == path && x.service == service).ToList().FirstOrDefault();
            if (ret is null)
                return;
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
        public async Task<JArray> HttpGetJArray(string url)
        {
            var r = await HttpGet(url);
            if (r != null)
                return (JArray)JsonConvert.DeserializeObject(r);
            return null;
        }
        public override BaseUser GetUserById(string id)
        {
            var pos = id.IndexOf('/');
            if (pos < 0)
                return null;
            var service = id.Substring(0, pos);
            var userId = id.Substring(pos + 1);
            using var db = NewDbContext(true);
            return db.Users.AsNoTracking().FirstOrDefault(x => x.id == userId && x.service == service);
        }
        protected override async Task<IHasReadFav> FindWorkGroupByDbKey(string key)
        {
            var pos = key.IndexOf('/');
            if (pos < 0)
                return null;
            var service = key.Substring(0, pos);
            var id = key.Substring(pos + 1);
            return await database.WorkGroups.FirstOrDefaultAsync(x => x.id == id && x.user.service == service && !x.isNonImage);
        }
        protected override async Task ApplyPendingUiOperation(PendingUiOperation operation)
        {
            switch (operation.Kind)
            {
                case PendingUiOperationKind.SetPageExcluded:
                    {
                        var parts = operation.TargetKey.Split(new[] { '|' }, 3);
                        if (parts.Length != 3)
                            return;
                        if (parts[0] == "Work")
                        {
                            var work = await database.Works.FirstOrDefaultAsync(x => x.service == parts[1] && x.urlPath == parts[2]);
                            if (work is not null)
                                work.excluded = operation.Value != 0;
                        }
                        else if (parts[0] == "ExternalWork" && int.TryParse(parts[1], out var type))
                        {
                            var externalWorkType = (ExternalWork.ExternalWorkType)type;
                            var work = await database.ExternalWorks.FirstOrDefaultAsync(x => x.type == externalWorkType && x.id == parts[2]);
                            if (work is not null)
                                work.excluded = operation.Value != 0;
                        }
                        return;
                    }
                case PendingUiOperationKind.SetUserFollowOrQueue:
                    {
                        var pos = operation.TargetKey.IndexOf('/');
                        if (pos < 0)
                            return;
                        var service = operation.TargetKey.Substring(0, pos);
                        var id = operation.TargetKey.Substring(pos + 1);
                        var user = await database.Users.FirstOrDefaultAsync(x => x.id == id && x.service == service);
                        if (user is null)
                        {
                            user = new User { id = id, service = service };
                            user.displayText = user.displayId = id;
                            database.Users.Add(user);
                        }
                        user.FollowQueueStatus = (UserFollowQueueStatus)operation.Value;
                        return;
                    }
            }
            await base.ApplyPendingUiOperation(operation);
        }
        public async Task<string> HttpGet(string url)
        {
            for (int try_ct = 2; try_ct >= 0; --try_ct)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
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
        // 只由串行后台调用；不保存，由抓取调用方连同fetched状态一起提交。
        private async Task SplitNonImageWorks(WorkGroup group)
        {
            database.ChangeTracker.DetectChanges();
            var works = group.works.Where(x => x.Dettached).ToList();
            var externalWorks = group.externalWorks.Where(x => x.Dettached).ToList();
            var cover = group.cover;
            var child = group.children.SingleOrDefault(x => x.isNonImage);
            if (works.Count == 0 && externalWorks.Count == 0 && !(cover?.Dettached ?? false))
            {
                if (child is not null)
                    child.fetched = group.fetched;
                return;
            }
            if (child is null)
            {
                child = database.WorkGroups.Add(new WorkGroup
                {
                    id = await database.GetNextChildId(),
                    isNonImage = true,
                    parent = group,
                    user = group.user,
                    title = group.title,
                    desc = group.desc,
                    embedUrl = group.embedUrl
                }).Entity;
            }
            child.fetched = group.fetched;
            foreach (var work in works)
                work.workGroup = child;
            foreach (var work in externalWorks)
                work.workGroup = child;
            if (cover?.Dettached == true)
            {
                group.cover = null;
                child.cover = cover;
            }
        }
        //获取illustGroup的content以获取外链
        private async Task FetchWorkGroup(WorkGroup illustGroup)
        {
            var doc = await HttpGetJson($"{baseAPIUrl}/{illustGroup.service}/user/{illustGroup.user.id}/post/{illustGroup.id}");
            if (doc is null || !doc.ContainsKey("id"))
            {
                Log($"Can't Fetch IllustGroup :{illustGroup.id} {illustGroup.service}");
                return;
            }
            // 只有预览的帖子没有原图，保留 fetched=false，之后定期重新检查。
            if (doc.Value<bool?>("has_full") == false)
                return;
            illustGroup.desc = doc.Value<string>("content");
            illustGroup.embedUrl = doc["embed"]?.Value<string>("url");
            // 预览帖子后来导入时，附件可能发生变化。
            int index = 1;
            var attachments = (doc["attachments"] ?? new JArray()).ToList();
            foreach (var attachment in attachments)
            {
                var work = await TryAddWork(attachment, illustGroup.service);
                if (work is not null)
                {
                    work.index = index;
                    work.workGroup = illustGroup;
                }
                index++;
            }
            try
            {
                if (illustGroup.user.dowloadExternalWorks == User.DownloadExternalWorkType.KeyZipMega)
                    await ParseKeyZipMega(illustGroup, attachments);
                if(illustGroup.user.dowloadExternalWorks == User.DownloadExternalWorkType.DirectExternal)
                    await ParseGroupContent(illustGroup);
            }
            catch (Exception e)
            {
                illustGroup.fetched = false;
                await SplitNonImageWorks(illustGroup);
                await database.SaveChangesAsync();
                LogError($"Fail ParseExternalWork {illustGroup.service}/{illustGroup.id}: {e.Message}");
                return;
            }
            illustGroup.fetched = true;
            await SplitNonImageWorks(illustGroup);
            await database.SaveChangesAsync();
            //Log($"Fetch IllustGroup Done:{illustGroup.id} {illustGroup.title}");
        }
        private async Task ParseKeyZipMega(WorkGroup workGroup, List<JToken> attachments)
        {
            var keyFiles = attachments
                .Where(x => String.Equals(Path.GetFileName(x.Value<string>("name")), "key.zip", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (keyFiles.Count == 0)
                return;
            if (keyFiles.Count != 1 || String.IsNullOrWhiteSpace(keyFiles[0].Value<string>("path")))
            {
                LogError($"Invalid KeyZipMega {workGroup.service}/{workGroup.id}");
                return;
            }

            var keyWork = new Work
            {
                service = workGroup.service,
                urlPath = keyFiles[0].Value<string>("path"),
                name = keyFiles[0].Value<string>("name")
            };
            var (success, entries) = await httpZipEntriesReader.GetEntries(keyWork.DownloadURL);
            if (!success)
                return;
            var files = entries.Where(x => !x.IsDirectory).ToList();
            var entryName = files.Count == 1 ? files[0].FullName : null;
            var token = Path.GetFileNameWithoutExtension(entryName);
            if (String.IsNullOrWhiteSpace(entryName) || entryName.Contains('/') || entryName.Contains('\\') ||
                !Path.GetExtension(entryName).IsImage() || !Regex.IsMatch(token, "^[A-Za-z0-9_-]{8}#[A-Za-z0-9_-]{43}$"))
            {
                LogError($"Invalid KeyZipMega {workGroup.service}/{workGroup.id}");
                return;
            }
            var megaUri = new Uri("https://mega.nz/file/" + token);
            INode node;
            try
            {
                node = await mega.GetNodeFromLinkAsync(megaUri);
            }
            catch (ApiException e) when (
                e.ApiResultCode == ApiResultCode.ResourceNotExists ||
                e.ApiResultCode == ApiResultCode.ResourceExpired ||
                e.ApiResultCode == ApiResultCode.AccessDenied ||
                e.ApiResultCode == ApiResultCode.CryptographicError ||
                e.ApiResultCode == ApiResultCode.ResourceAdministrativelyBlocked ||
                e.ApiResultCode == ApiResultCode.BadArguments)
            {
                LogError($"Invalid KeyZipMega {workGroup.service}/{workGroup.id}");
                return;
            }
            var name = node is null ? null : Path.GetFileName(node.Name);
            if (node is null || node.Type != NodeType.File || node.Size <= 0 || node.Size > ArchiveExtractor.MaxArchiveBytes ||
                !String.Equals(Path.GetExtension(name), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                LogError($"Invalid KeyZipMega {workGroup.service}/{workGroup.id}");
                return;
            }
            if (database.ExternalWorks.Count(x => x.id == node.Id && x.type == ExternalWork.ExternalWorkType.Mega) > 0)
                return;
            var index = (database.ExternalWorks.Where(x => (x.workGroup.id == workGroup.id || x.workGroup.parentId == workGroup.id)
                && x.workGroup.user.service == workGroup.service)
                .Max(x => (int?)x.index) ?? 0) + 1;
            database.ExternalWorks.Add(new ExternalWork
            {
                id = node.Id,
                type = ExternalWork.ExternalWorkType.Mega,
                name = name,
                url = megaUri.AbsoluteUri,
                index = index,
                workGroup = workGroup
            });
            await database.SaveChangesAsync();
            Log($"Parse KeyZipMega: {workGroup.service}/{workGroup.id} => {name} ({node.Size} bytes)");
        }
        private async Task<bool> PostProcessExternalZip(ExternalWork work, string archivePath)
        {
            bool createImageGroup = work.workGroup.user.dowloadExternalWorks == User.DownloadExternalWorkType.KeyZipMega;
            var destinationDirectory = Path.Combine(Path.GetDirectoryName(archivePath),
                Path.GetFileNameWithoutExtension(archivePath) + "_images");
            string archiveId = null;
            if (createImageGroup)
            {
                destinationDirectory = Path.GetDirectoryName(archivePath);
                archiveId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    Encoding.UTF8.GetBytes($"{work.type}/{work.id}")));
            }
            var (success, files) = await ArchiveExtractor.ExtractFiles(archivePath, destinationDirectory,
                Util.imageExtensions, flatFileName: archiveId);
            if (!success)
                return false;
            if (createImageGroup && files.Count > 0)
            {
                try
                {
                    File.Delete(archivePath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    LogError($"Archive cleanup failed {work.service}/{work.id}: {e.Message}");
                    return false;
                }
                var parent = work.workGroup.ParentGroup;
                var imageGroup = new WorkGroup
                {
                    id = await database.GetNextChildId(),
                    parent = parent,
                    parentId = parent.id,
                    user = parent.user,
                    title = $"{parent.title} [{Path.GetFileNameWithoutExtension(work.name)}]",
                    fetched = true
                };
                int index = 1;
                foreach (var file in files)
                {
                    imageGroup.works.Add(new Work
                    {
                        service = work.service,
                        urlPath = $"{work.type}/{work.id}/{index}",
                        name = archiveId + Path.GetExtension(file),
                        index = index++,
                        workGroup = imageGroup
                    });
                }
                database.WorkGroups.Add(imageGroup);
                // 调用方将图片组、图片和压缩包下载完成标记在同一次SaveChanges中提交。
            }
            Log($"Extract ExternalWork: {work.workGroup.service}/{work.workGroup.id} {files.Count} images");
            return true;
        }
        public override bool ListenerUtil_IsValidUrl(string url)
        {
            if (url.StartsWith(baseUrl))
                return true;
            return false;
        }
        static public void CheckStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new Exception("HTTP Not Success");
        }

        private static string GetDownloadQueueKey(PawchiveBaseWork work)
        {
            return work switch
            {
                Work w => $"Work|{w.service}|{w.urlPath}",
                ExternalWork w => $"ExternalWork|{(int)w.type}|{w.id}",
                _ => throw new ArgumentException($"Unsupported Pawchive work type: {work.GetType().FullName}")
            };
        }

        private async Task<PawchiveBaseWork> LoadDownloadQueueWork(string key)
        {
            var parts = key.Split(new[] { '|' }, 3);
            if (parts.Length != 3)
                return null;
            if (parts[0] == "Work")
                return await database.Works
                    .Include(x => x.workGroup)
                    .ThenInclude(x => x.user)
                    .FirstOrDefaultAsync(x => x.service == parts[1] && x.urlPath == parts[2]);
            if (parts[0] == "ExternalWork" && int.TryParse(parts[1], out var type))
            {
                var externalWorkType = (ExternalWork.ExternalWorkType)type;
                return await database.ExternalWorks
                    .Include(x => x.workGroup)
                    .ThenInclude(x => x.user)
                    .FirstOrDefaultAsync(x => x.type == externalWorkType && x.id == parts[2]);
            }
            return null;
        }

        // 下载(加入队列)应当下载的图片，将收藏的作品加入fav文件夹，从fav中删除多余的文件,从tmp中删除已读
        // 只管理图片，对dettach类型(视频等)只负责加到下载队列
        private void SyncLocalFile()
        {
            // 查找tmp目录，将所有应下载的文件加入队列
            {
                var illustGroups = (from illustGroup in database.WorkGroups
                                    where illustGroup.fetched == true
                                           && (illustGroup.fav || !illustGroup.readed)
                                           && (illustGroup.user.followed == true || illustGroup.user.queued == true)
                                    select illustGroup).ToList();
                var tmp = downloadQueue.Count;
                foreach (var workGroup in illustGroups)//对收藏或未读的作品
                {
                    if (workGroup.works.Count == 0 && workGroup.externalWorks.Count == 0 && workGroup.children.Count == 0)
                    {
                        workGroup.readed = true;
                        continue;
                    }
                    var works = workGroup.GetShouldDownloadWorks().ToList();
                    // 子组的下载完成状态独立于父组的图片阅读状态。
                    if (workGroup.isNonImage && works.Count > 0 && works.All(work => work.DettachDownloaded))
                    {
                        workGroup.DettachDownloaded = true;
                        continue;
                    }
                    if (workGroup.IsChild && !workGroup.isNonImage)
                        continue;
                    foreach (var work in works)
                    {
                        var key = GetDownloadQueueKey(work);
                        if (workGroup.fav == false || work.excluded == false)//没有排除
                            if (!downloadQueue.Contains(key)) //不在下载队列
                                if(!(work.Dettached && work.DettachDownloaded)) // 不是之前下载过的dettach类型
                                    if (work.Dettached || !File.Exists($"{download_dir_tmp}/{work.TmpSubPath}"))
                                        downloadQueue.Add(key);
                    } 
                }
                if (downloadQueue.Count > tmp)
                    Log($"Update Download Queue {tmp}=>{downloadQueue.Count}");
            }
            database.SaveChanges();
            //整理Fav文件夹
            {
                //.ToList()以释放数据库连接
                //GetFullPath以统一斜杠格式
                var existedFiles = Directory.GetFiles(Path.GetFullPath(download_dir_fav),"*",new EnumerationOptions {RecurseSubdirectories=true}).ToHashSet<string>();
                var illustGroups = (from illustGroup in database.WorkGroups
                                    where illustGroup.fav && !illustGroup.isNonImage
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                    foreach (var illust in illustGroup.works)
                        if (!illust.Dettached) // 一个group中可能同时存在图片和dettach类型
                        {
                            var tmp_path = Path.GetFullPath($"{download_dir_tmp}/{illust.TmpSubPath}");
                            var fav_path = Path.GetFullPath($"{download_dir_fav}/{illust.FavSubPath}");
                            if (!illust.excluded)
                            {
                                if (existedFiles.Contains(fav_path))
                                    existedFiles.Remove(fav_path);
                                else
                                    CopyFile(tmp_path, fav_path);
                            }
                        }
                foreach (var file in existedFiles)//剩下的都是不需要的文件
                    DeleteFile(file);
                Util.ClearEmptyFolders(download_dir_fav);
            }
            //清理tmp文件夹
            {
                int ct = 0;
                var workGroups = (from illustGroup in database.WorkGroups
                                    where illustGroup.readed && illustGroup.fetched && !illustGroup.fav && !illustGroup.isNonImage
                                    select illustGroup).ToList();
                foreach (var workGroup in workGroups)
                {
                    foreach (var work in workGroup.works)
                        if (!work.Dettached)
                            ct += DeleteFile($"{download_dir_tmp}/{work.TmpSubPath}");
                }
                if (ct > 0)
                    Log($"Delete from tmp:{ct}");
            }
        }
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        public async override Task<List<ExplorerQueue>> GetExplorerQueues()
        {
            var ret = new List<ExplorerQueue>();
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Fav, "0", "Pawchive-Fav"));
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Main, "0", "Pawchive-Main"));
            using var db = NewDbContext(true);
            foreach (var user in db.Users.AsNoTracking().Where(x => x.queued).ToList())
                ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.User, $"{user.service}/{user.id}", user.displayId));
            return ret;
        }
#pragma warning restore CS1998
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
        public async override Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue)
#pragma warning restore CS1998
        {
            var result = new List<ExplorerFileBase>();
            using var db = NewDbContext(true);
            if (queue.type == ExplorerQueue.QueueType.Main)
            {
                var illustGroups = (from illustGroup in db.WorkGroups.AsNoTracking()
                                        .Include(x => x.user)
                                        .Include(x => x.works)
                                        .Include(x => x.externalWorks)
                                    where illustGroup.fetched && illustGroup.readed == false && illustGroup.fav == false
                                       && !illustGroup.isNonImage
                                       && illustGroup.user.followed
                                       && (illustGroup.parentId == null ? illustGroup.user.downloadAttachmentImages
                                            : illustGroup.user.dowloadExternalWorks == User.DownloadExternalWorkType.KeyZipMega)
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    var exploreFile = new ExplorerFile(illustGroup, download_dir_tmp);
                    if(exploreFile.validPageCount()>0)
                        result.Add(exploreFile);
                }
            }
            else if (queue.type == ExplorerQueue.QueueType.Fav)
            {
                var illustGroups = (from illustGroup in db.WorkGroups.AsNoTracking()
                                        .Include(x => x.user)
                                        .Include(x => x.works)
                                        .Include(x => x.externalWorks)
                                    where illustGroup.fetched && illustGroup.fav && !illustGroup.isNonImage
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    var exploreFile = new ExplorerFile(illustGroup, download_dir_tmp);
                    if (exploreFile.validPageCount() > 0)
                        result.Add(exploreFile);
                }
            }
            else
            {
                string id_text = queue.id;
                string service = id_text.Substring(0, id_text.IndexOf('/'));
                string id = id_text.Substring(id_text.IndexOf('/') + 1);
                var illustGroups = (from illustGroup in db.WorkGroups.AsNoTracking()
                                        .Include(x => x.user)
                                        .Include(x => x.works)
                                        .Include(x => x.externalWorks)
                                    where illustGroup.fetched && illustGroup.readed == false
                                       && !illustGroup.isNonImage
                                       && illustGroup.user.id == id && illustGroup.user.service == service
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    if ((illustGroup.parentId == null ? illustGroup.user.downloadAttachmentImages
                        : illustGroup.user.dowloadExternalWorks == User.DownloadExternalWorkType.KeyZipMega) && illustGroup.works.Count > 0)
                    {
                        var exploreFile = new ExplorerFile(illustGroup, download_dir_tmp);
                        if (exploreFile.validPageCount() > 0)
                            result.Add(exploreFile);
                    }
                    //目前从外链下载的都是视频，不在此浏览
                    //else if (illustGroup.user.dowloadExternalWorks&& illustGroup.externalWorks is not null&& illustGroup.externalWorks.Count>0)
                    //    result.Add(new ExplorerExternalFile(illustGroup, download_dir_tmp));
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
                                         where illustGroup.fetched == false && illustGroup.parentId == null
                                            && (illustGroup.user.followed == true || illustGroup.user.queued == true)
                                         select illustGroup).ToList())
                await FetchWorkGroup(illustGroup);
            Log("Fetch Groups Done");
        }
        public async Task AddQueuedUser(string id, string service)
        {
            using (var db = NewDbContext(true))
            {
                var user = db.Users.AsNoTracking().Where(x => x.id == id && x.service == service).FirstOrDefault();
                if (user is not null && (user.followed || user.queued))
                    return;
            }
            await QueuePendingUiOperation(new PendingUiOperation
            {
                Kind = PendingUiOperationKind.SetUserFollowOrQueue,
                TargetKey = $"{service}/{id}",
                Value = (int)UserFollowQueueStatus.Queued
            });
        }
        private async Task RunSchedule(bool enableScheduleTasks)
        {
            //和pixiv不同，请求次数很少，除了下载图片不需要使用队列
            //由于hitomi不提供浏览收藏等数据，通过tag或搜索获得的作品良莠不齐，因此只做关注作者相关功能，不做随机浏览队列
            int last_daily_task = DateTime.Now.Day;
            await ApplyPendingUiOperations();
            if (enableScheduleTasks)
                SyncLocalFile();
            await RunPendingAndScheduleLoop(
                ApplyPendingUiOperations,
                async () =>
                {
                    //await ReloadDb();
                    if (DateTime.Now.Day != last_daily_task)//每日一次
                    {
                        last_daily_task = DateTime.Now.Day;
                        await FetchUserAndIllustGroups();
                        await ApplyPendingUiOperations();
                        if (DateTime.Now.DayOfWeek == DayOfWeek.Monday) //每周一次
                        {
                            foreach (var user in database.Users.ToList())//更新作者
                                await FetchUser(user.id, user.service);
                        }
                        SyncLocalFile();
                    }
                    //同时下载太多503
                    await ProcessIllustDownloadQueue(downloadQueue, 40);
                },
                new TimeSpan(0, 30, 0),
                enableScheduleTasks);
        }
        public override async Task<bool> ListenerUtil_FollowUser(string url)
        {
            var regex = new Regex(baseUrl + "/([a-z]+)/user/([^/]+)(/.*)?$");
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

        private async Task ProcessIllustDownloadQueue(List<string> workList, int limit = -1)
        {
            try
            {
                //移除临时文件
                foreach (var file in Directory.GetFiles(download_dir_tmp, "*.aria2"))//下载临时文件
                    File.Delete(file);
                var download_illusts = new List<(string key, PawchiveBaseWork work)>();
                var ignore_illusts = new List<string>();
                int download_ct = 0;
                foreach (var key in workList.ToList())
                {
                    var work = await LoadDownloadQueueWork(key);
                    if (work is null)
                    {
                        ignore_illusts.Add(key);
                        continue;
                    }
                    var path = Path.Combine(download_dir_tmp, work.TmpSubPath);
                    var dir = Path.GetDirectoryName(path).Replace('\\','/');
                    var filename = Path.GetFileName(path);
                    var ext = work.Ext;
                    if ((ext.IsImage() || ext.IsVideo()) && work is Work)
                    {
                        // 站点禁止下载工具伪装浏览器，使用随模块附带的 aria2 原生标识。
                        await Task.Delay(TimeSpan.FromSeconds(2));
                        Util.TouchDir(dir);
                        await downloader.GetDownloader(Downloader.DownloaderType.Aria2DownloadQueue)
                            .Add(work.DownloadURL, dir, filename, new DownloadRequestOptions
                            {
                                UserAgent = "aria2/1.33.0",
                                Referer = baseUrl + "/",
                                Split = 1,
                                MaxConnectionPerServer = 1
                            });
                    }
                    else if ((ext.IsVideo() || ext.IsZip()) && work is ExternalWork)
                    {
                        if (!File.Exists(path))
                            await downloader.Add(work, download_dir_tmp);
                    }
                    else if (ext.IsZip())
                    {
                        ignore_illusts.Add(key);//暂定：直接忽略压缩包
                        continue;
                    }
                    else
                    {
                        ignore_illusts.Add(key);
                        continue;
                    }
                    download_ct++;
                    download_illusts.Add((key, work));
                    if (limit >= 0 && download_ct >= limit)
                        break;
                }
                foreach (var key in ignore_illusts)
                    workList.Remove(key);
                ignore_illusts.Clear();

                //等待完成并查询状态
                await downloader.WaitForAll();
                //检查结果，以本地文件为准，无视aria2和函数的返回
                {
                    int success_ct = 0;
                    //var fail_illustGroup=new HashSet<WorkGroup>();
                    foreach (var (key, illust) in download_illusts)
                    {
                        var path = $"{download_dir_tmp}/{illust.TmpSubPath}";
                        var localComplete = File.Exists(path) && !File.Exists(path + ".aria2");
                        if (localComplete && illust.Ext.IsZip() && illust is ExternalWork externalWork)
                            localComplete = await PostProcessExternalZip(externalWork, path);
                        if (File.Exists(path + ".aria2") || !localComplete)//存在.aria2说明下载未完成
                        {
//                            Log($"Download Fail: {illust.url}");
                            workList.Remove(key);//移到队末并重置url
                            workList.Add(key);
                            //fail_illustGroup.Add(illust.workGroup);
                            //throw new Exception("debug");
                        }
                        else
                        {
                            success_ct++;
                            workList.Remove(key);
                            if (illust.Dettached) // dettach类型只下载一次，
                                illust.DettachDownloaded = true;
                            //转换格式
                        }
                    }
                    await database.SaveChangesAsync();
                    Log($"Process Download Queue: {success_ct}/{download_illusts.Count} Success, {downloadQueue.Count} Left.");
                }
            }
            catch (Exception e)
            {
                // 丢弃本批未保存的状态，避免后续任务误提交；下次启动重新扫描未完成任务。
                database.ChangeTracker.Clear();
                LogError($"Download batch interrupted: {e}");
            }
        }

    }
}
