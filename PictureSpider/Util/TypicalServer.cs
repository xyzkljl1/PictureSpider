using Fizzler;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace PictureSpider
{
    // 一个简单的通用爬虫模板，使用预设有Work/WorkGroup/User的EF数据库，Aria2下载，无浏览队列，借用LSF浏览
    public abstract class TypicalServer<DatabaseType,WorkType,WorkGroupType,UserType>: BaseServerWithDB<DatabaseType> 
        where DatabaseType: TypicalDatabase<WorkType, WorkGroupType, UserType>, new()
        where WorkType : TypicalWork<WorkGroupType>
        where WorkGroupType : TypicalWorkGroup<WorkType, UserType>
        where UserType : TypicalUser<WorkGroupType>,new()
    {
        protected HttpClient httpClient;
        protected string download_dir_root = "";
        protected string download_dir_tmp = "";
        // fav目前没用到
        protected string download_dir_fav = "";
        //downloader 目前需要派生类自己初始化
        public Aria2DownloadQueue downloader;
        protected List<WorkType> downloadQueue = new List<WorkType>();
        protected TypicalServer(String proxy,Config config): base()
        {
            {
                var name = this.GetType().Namespace;
                name = name.Split(".").Last();
                base.ResetDb(config.TypicalConnectStr + name);
                {
                    var prefix = name.ToUpper();
                    for (int i = 1; i < prefix.Length; ++i)
                        if (!usedLogPrefix.Contains(prefix.Substring(0, i)))
                        {
                            prefix = prefix.Substring(0, i);
                            break;
                        }
                    usedLogPrefix.Add(prefix);
                    logPrefix = prefix;
                }

                download_dir_root = Path.Combine(config.TypicalDownloadDir, name);
                download_dir_fav = Path.Combine(download_dir_root, "fav");
                download_dir_tmp = Path.Combine(download_dir_root, "tmp");
                foreach (var dir in new List<string> { download_dir_root, download_dir_tmp, download_dir_fav })
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = 256,
                UseCookies = true,
                Proxy = new WebProxy(proxy, false)
            };
            handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            httpClient = new HttpClient(handler);
            httpClient.Timeout = new TimeSpan(0, 0, 35);
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8,ja;q=0.7");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        }
        public void Dispose()
        {
            httpClient.Dispose();
        }
        protected virtual async Task FetchworkGroupListByUser(UserType user)
        {
            return;
        }
        protected virtual async Task FetchWorkGroup(WorkGroupType workGroup)
        {
            return;
        }
        //获取作品列表
        protected virtual async Task FetchUserAndWorkGroups()
        {
            //注意linq语句产生的Iqueryable不是立即返回，而是一直占用连接,此期间无法进行其它查询
            //加上ToList令查询完成后再执行循环
            //获取follow/queue作者的作品
            foreach (var user in (from user in database.Users
                                  where user.followed == true || user.queued == true
                                  select user).ToList())
                await FetchworkGroupListByUser(user);
            Log("Fetch User Done");
            // fetch workgroup
            foreach (var workGroup in (from workGroup in database.WorkGroups
                                       where workGroup.fetched == false && (workGroup.user.followed == true || workGroup.user.queued == true)
                                       select workGroup).ToList())
                await FetchWorkGroup(workGroup);
        }
        protected virtual async Task UpdateDownloadQueue()
        {
            var workGroups = (from workGroup in database.WorkGroups
                              where workGroup.fetched == true
                                      && (workGroup.fav || !workGroup.readed)
                                      && (workGroup.user.followed == true || workGroup.user.queued == true)
                              select workGroup).ToList();
            var tmp = downloadQueue.Count;
            foreach (var workGroup in workGroups)//如果收藏或未读的作品
                foreach (var work in workGroup.works)
                    if (!work.downloaded)
                        if (!work.excluded) //没有排除
                            if (!downloadQueue.Contains(work)) //不在下载队列
                                if (!File.Exists($"{download_dir_tmp}/{work.fileName}{work.ext}")) //不在本地
                                    downloadQueue.Add(work);
            if (downloadQueue.Count > tmp)
                Log($"Update Download Queue {tmp}=>{downloadQueue.Count}");
        }
        protected virtual async Task RunSchedule()
        {           
            int last_daily_task = DateTime.Now.Day;
            var day_of_week = DateTime.Now.DayOfWeek;
            await UpdateDownloadQueue();
            do
            {
                if (DateTime.Now.Day != last_daily_task)//每日一次
                {
                    last_daily_task = DateTime.Now.Day;
                    await FetchUserAndWorkGroups();
                    await UpdateDownloadQueue();
                }
                await ProcessIllustDownloadQueue(downloadQueue, 40);
                await Task.Delay(new TimeSpan(0, 30, 0));
            }
            while (true);
        }
        protected virtual async Task ProcessIllustDownloadQueue(List<WorkType> illustList, int limit = -1)
        {
            try
            {
                //移除临时文件
                downloader.ClearTmpFiles(download_dir_tmp);
                var download_illusts = new List<WorkType>();
                int download_ct = 0;
                foreach (var work in illustList)
                {
                    await downloader.Add(work.url, download_dir_tmp, work.TmpSubPath);
                    download_ct++;
                    download_illusts.Add(work);
                    if (limit >= 0 && download_ct >= limit)
                        break;
                }
                //等待完成并查询状态
                await downloader.WaitForAll();
                //检查结果，以本地文件为准，无视aria2和函数的返回
                {
                    int success_ct = 0;
                    foreach (var work in download_illusts)
                    {
                        var path = $"{download_dir_tmp}/{work.TmpSubPath}";
                        if (File.Exists(path + ".aria2") || !File.Exists(path))//存在.aria2说明下载未完成
                        {
                            Log($"Download Fail: {work.url}");
                            illustList.Remove(work);//移到队末并重置url
                            illustList.Add(work);
                        }
                        else
                        {
                            success_ct++;
                            illustList.Remove(work);
                            work.downloaded = true;
                        }
                    }
                    await database.SaveChangesAsync();
                    Log($"Process Download Queue: {success_ct}/{download_illusts.Count} Success, {downloadQueue.Count} Left.");
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                throw;
            }
        }
        static public void CheckStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new Exception("HTTP Not Success");
        }
        public async Task<HtmlDocument> HttpGetHTML(string url, string referer="")
        {
            var doc = new HtmlDocument();
            var result = await HttpGet(url, referer);
            if (result is null)
                return null;
            doc.LoadHtml(result);
            return doc;
        }

        public async Task<string> HttpGet(string url, string referer)
        {
            for (int try_ct = 8; try_ct >= 0; --try_ct)
            {
                try
                {
                    if (string.IsNullOrEmpty(url))
                        throw new ArgumentNullException("url");
                    if (!url.StartsWith("https"))
                        throw new ArgumentException("Not SSL");
                    // TODO: refer写到请求里，而不是defaultHeader
                    if(!String.IsNullOrEmpty(referer))
                        httpClient.DefaultRequestHeaders.Referrer = new Uri(referer);
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
        /*
        public override BaseUser GetUserById(string id)
        {
            return database.Users.Where(x => x.name == id).FirstOrDefault();
        }
        public override void SetUserFollowOrQueue(BaseUser user)
        {
            // 对user的修改在ui中完成，此处只需要save
            database.SaveChanges();
        }
        public override void SetBookmarkEach(ExplorerFileBase file)
        {
            throw new NotImplementedException();
        }
        public override void SetReaded(ExplorerFileBase file)
        {
            (file as TypicalExplorerFile<WorkType, WorkGroupType, UserType>).workGroup.readed = file.readed;
            database.SaveChanges();
        }
        public override void SetBookmarked(ExplorerFileBase file)
        {
            (file as TypicalExplorerFile<WorkType, WorkGroupType, UserType>).workGroup.fav = file.bookmarked;
            database.SaveChanges();
        }*/

        public async virtual Task<bool> AddQueuedUser(string name)
        {
            UserType user = null;
            if (database.Users.Count(x => x.name == name) > 0)
                user = database.Users.Where(x => x.name == name).First();
            else
            {
                user = new UserType();
                user.displayText = user.displayId = user.name = name;
                database.Users.Add(user);
            }
            if (user.followed || user.queued)
                return true;
            user.queued = true;
            await database.SaveChangesAsync();
            return true;
        }
    }
}
