﻿using Microsoft.AspNetCore.WebUtilities;
using Microsoft.ClearScript.V8;
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

namespace PictureSpider.Hitomi
{
    public partial class Server : BaseServer, IDisposable
    {
        private Database database;
        private HttpClient httpClient;
        //private string base_host = "hitomi.la";
        //private string base_host_ltn = "ltn.hitomi.la";
        private string baseUrl = "https://hitomi.la";
        private string baseUrlLtn = "https://ltn.hitomi.la";
        private string commonjs = "";
        private string myjs = "";

        private string download_dir_root = "";
        private string download_dir_tmp = "";
        private string download_dir_fav = "";
        Aria2DownloadQueue downloader;
        private List<Illust> downloadQueue = new List<Illust>();//计划下载的illustid,线程不安全,只在RunSchedule里使用
        public Server(Config config)
        {
            logPrefix = "H";
            database = new Database(config.HitomiConnectStr);

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var handler = new HttpClientHandler()
            {
                MaxConnectionsPerServer = 256,
                UseCookies = true,
                Proxy = new WebProxy(config.Proxy, false)
            };
            handler.ServerCertificateCustomValidationCallback = delegate { return true; };
            httpClient = new HttpClient(handler);
            httpClient.Timeout = new TimeSpan(0, 0, 35);
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
            httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8,ja;q=0.7");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            //httpClient.DefaultRequestHeaders.Referrer=new Uri("https://hitomi.la/artist/muk-all.html?page=3");            

            download_dir_root = config.HitomiDownloadDir;
            download_dir_fav = Path.Combine(download_dir_root, "fav");
            download_dir_tmp = Path.Combine(download_dir_root, "tmp");
            downloader = new Aria2DownloadQueue(Aria2DownloadQueue.Downloader.Hitomi, config.Proxy, "https://hitomi.la");
            foreach (var dir in new List<string> { download_dir_root, download_dir_tmp, download_dir_fav })
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

        }
        public void Dispose()
        {
            httpClient.Dispose();
        }
        public override async Task Init()
        {
            PrepareJS();
            //await downloader.Add("https://ba.hitomi.la/webp/1693684802/1571/417b229d440f9eccb321e44a7a54fe66098e022daa3972eb6af5b24543cc5236.webp","E:\\","123");
            //await downloader.WaitForAll();
            //var t=database.IllustGroups.FirstOrDefault();
            //await FetchIllustGroupByIdx(t);
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
            RunSchedule();
#pragma warning restore CS4014
        }
        private async Task RunSchedule()
        {
            //和pixiv不同，请求次数很少，除了下载图片不需要使用队列
            //由于hitomi不提供浏览收藏等数据，通过tag或搜索获得的作品良莠不齐，因此只做关注作者相关功能，不做随机浏览队列
            int last_daily_task = DateTime.Now.Day;
            var day_of_week = DateTime.Now.DayOfWeek;
            SyncLocalFile();
            do
            {
                if (DateTime.Now.Day != last_daily_task)//每日一次
                {
                    last_daily_task = DateTime.Now.Day;
                    await FetchUserAndIllustGroups();
                    SyncLocalFile();
                }
                //同时下载太多503
                await ProcessIllustDownloadQueue(downloadQueue, 25);
                await Task.Delay(new TimeSpan(0, 5, 0));
            }
            while (true);
        }
        //下载(加入队列)应当下载的图片，将收藏的作品加入fav文件夹，从fav中删除多余的文件
        private void SyncLocalFile()
        {
            //下载
            {
                var illustGroups = (from illustGroup in database.IllustGroups
                                    where illustGroup.fetched == true
                                           && (illustGroup.fav || !illustGroup.readed)
                                           && (illustGroup.user.followed == true || illustGroup.user.queued == true)
                                    select illustGroup).ToList();
                var tmp = downloadQueue.Count;
                foreach (var illustGroup in illustGroups)//如果收藏或未读的作品
                {
                    database.LoadFK(illustGroup);
                    foreach (var illust in illustGroup.illusts)
                    {
                        database.LoadFK(illust);
                        if (!(illustGroup.fav && !illust.excluded))//没有排除
                            if (!downloadQueue.Contains(illust)) //不在下载队列
                                if (!File.Exists($"{download_dir_tmp}/{illust.fileName}{illust.ext}")) //不在本地
                                    downloadQueue.Add(illust);
                    }
                }
                if (downloadQueue.Count > tmp)
                    Log($"Update Download Queue {tmp}=>{downloadQueue.Count}");
            }
            //整理Fav文件夹
            {
                var existedFiles=Directory.GetFiles(download_dir_fav).Select(x=> Path.GetFileName(x)).ToHashSet<string>();
                var illustGroups = (from illustGroup in database.IllustGroups
                                    where illustGroup.fav
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    database.LoadFK(illustGroup);
                    foreach (var illust in illustGroup.illusts)
                    {
                        var tmp_path = $"{download_dir_tmp}/{illust.fileName}{illust.ext}";
                        var fav_path = $"{download_dir_fav}/{illust.fileName}{illust.ext}";
                        if (!illust.excluded)
                        {
                            if (existedFiles.Contains(fav_path))//从existedFiles中移除
                                existedFiles.Remove(fav_path);
                            else
                                if (File.Exists(tmp_path))      //如果不存在就从tmp目录拷过来
                                File.Copy(tmp_path, fav_path);
                        }
                    }
                }
                foreach (var file in existedFiles)//剩下的都是不需要的文件
                    File.Delete($"{download_dir_fav}/{file}");
            }
        }
        private async Task ProcessIllustDownloadQueue(List<Illust> illustList, int limit = -1)
        {
            try
            {
                //移除临时文件
                foreach (var file in Directory.GetFiles(download_dir_tmp, "*.aria2"))//下载临时文件
                    File.Delete(file);
                var download_illusts = new List<Illust>();
                int download_ct = 0;
                foreach (var illust in illustList)
                {
                    //是否应当下载在外部判断
                    if (illust.url == "")//重新计算url
                        await CalcIllustURL(illust.illustGroup);
                    await downloader.Add(illust.url, download_dir_tmp, $"{illust.fileName}{illust.ext}");
                    download_ct++;
                    download_illusts.Add(illust);
                    if (limit >= 0 && download_ct >= limit)
                        break;
                }
                //等待完成并查询状态
                await downloader.WaitForAll();
                //检查结果，以本地文件为准，无视aria2和函数的返回
                {
                    int success_ct = 0;
                    var fail_illusts = new HashSet<int>();
                    foreach (var illust in download_illusts)
                    {
                        var path = $"{download_dir_tmp}/{illust.fileName}{illust.ext}";
                        if (File.Exists(path + ".aria2") || !File.Exists(path))//存在.aria2说明下载未完成
                        {
                            Log($"Download Fail: {illust.url}");
                            illustList.Remove(illust);
                            illustList.Add(illust);
                            illust.url = "";//移到队末并重置url
                            //throw new Exception("debug");
                        }
                        else
                        {
                            success_ct++;
                            illustList.Remove(illust);
                        }
                    }
                    Log($"Process Download Queue: {success_ct}/{download_illusts.Count} Success, {downloadQueue.Count} Left.");
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                throw;
            }
        }
        //获取图片真实地址时使用的js不会经常变化，因此只在启动程序时获取一次
        private void PrepareJS()
        {
            commonjs = "";
            {
                //common.js有一些自动执行的代码没有卵用，不提供对应环境又会报错，需要去掉
                //自动处理js太麻烦了，此处选择手动处理后在本地存一份(作为嵌入资源)
                //需要在本地common.js的属性->生成操作里选择嵌入资源
                //更新common.js时需要去除顶层代码和document相关
                //var commonJS = await HttpGet($"{base_url_ltn}/common.js");
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("PictureSpider.Resources.common.js"))
                    using(var reader = new StreamReader(stream))
                        commonjs=reader.ReadToEnd();
            }
            //gg.js随时间变动
            //var ggJS = await HttpGet($"{baseUrlLtn}/gg.js");
            //common在前，因为common.js中定义了gg对象，gg.js中只有赋值
             myjs = @"
                        var gid=galleryinfo.id;
                        var myurl=[];
                        var myhash=[];
                        var mytitle=galleryinfo.japanese_title || galleryinfo.title;
                        for(let file of galleryinfo.files){
                            var src=url_from_url_from_hash(galleryinfo.id,file,'webp',undefined,'a');
                            myurl.push(src);
                            myhash.push(file.hash);
                        }";
        }
        public async Task CalcIllustURL(IllustGroup illustGroup)
        {
            //从https://ltn.hitomi.la/galleries/2360191.js获取galleryInfo,在reader.js中解析，用到了common.js和gg.js
            //图片路径形如https://[子域名].hitomi.la/webp/[常数]/[根据hash计算]/[hash].[扩展名]
            //借用common.js/gg.js，加上一段自己的js计算出图片路径
            //其中gg.js内容会随时间变化，导致图片地址变化
            database.LoadFK(illustGroup);
            var galleryInfoJS = await HttpGet($"{baseUrlLtn}/galleries/{illustGroup.Id}.js");
            var ggJS = await HttpGet($"{baseUrlLtn}/gg.js");
            using (var engine = new V8ScriptEngine())
            {
                //注意顺序。要用\n隔开
                var script = galleryInfoJS + "\n" + commonjs + "\n" + ggJS + "\n" + myjs;
                engine.Execute(script);
                var urls = engine.Script.myurl;
                var hashs = engine.Script.myhash;
                var illusts = illustGroup.illusts.ToList();
                //同一个illustGroup里也可能有相同hash的图片,此处不能用hash查找要用index
                illusts.Sort((l, r) =>l.index.CompareTo(r.index));

                for (var i = 0; i < urls.length&&i<illusts.Count; ++i)
                    {
                        var illust = illusts[i];
                        illust.url = urls[i] as string;
                        illust.ext = "";
                        var pos = illust.url.LastIndexOf('.');
                        if (pos > 0)
                            illust.ext = illust.url.Substring(pos);
                    }
            }
            database.SaveChanges();
        }
        public async override Task<List<ExplorerQueue>> GetExplorerQueues() {
            var ret=new List<ExplorerQueue>();
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Fav, "0", "Hitomi-Fav"));
            ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.Main, "0", "Hitomi-Main"));
            foreach (var user in database.Users.Where(x => x.queued).ToList())
                ret.Add(new ExplorerQueue(ExplorerQueue.QueueType.User, user.name, user.name));
            return ret;
        }
        public async override Task<List<ExplorerFileBase>> GetExplorerQueueItems(ExplorerQueue queue)
        {
            var result =new List<ExplorerFileBase>();
            if(queue.type == ExplorerQueue.QueueType.Main)
            {
                var illustGroups=(from illustGroup in database.IllustGroups
                                 where illustGroup.fetched && illustGroup.readed==false  && illustGroup.fav==false
                                    && illustGroup.user.followed
                                 select illustGroup).ToList();
                foreach(var illustGroup in illustGroups)
                {
                    database.LoadFK(illustGroup);
                    var exploreFile = new ExplorerFile(illustGroup, download_dir_tmp);
                    result.Add(exploreFile);
                }
            }
            else if(queue.type==ExplorerQueue.QueueType.Fav)
            {
                var illustGroups = (from illustGroup in database.IllustGroups
                                    where illustGroup.fetched && illustGroup.fav
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    database.LoadFK(illustGroup);
                    var exploreFile = new ExplorerFile(illustGroup, download_dir_tmp);
                    result.Add(exploreFile);
                }
            }
            else
            {
                var illustGroups = (from illustGroup in database.IllustGroups
                                    where illustGroup.fetched && illustGroup.readed == false
                                       && illustGroup.user.name==queue.id
                                    select illustGroup).ToList();
                foreach (var illustGroup in illustGroups)
                {
                    database.LoadFK(illustGroup);
                    var exploreFile = new ExplorerFile(illustGroup, download_dir_tmp);
                    result.Add(exploreFile);
                }
            }
            result.Sort((l, r) =>(l as ExplorerFile).illustGroup.title.CompareTo((r as ExplorerFile).illustGroup.title));
            return result;
        }
        private HashSet<string> tt=new HashSet<string>();
        //获取illustGroup详细信息
        private async Task FetchIllustGroupById(IllustGroup illustGroup)
        {            
            database.LoadFK(illustGroup);
            var galleryInfoJS = await HttpGet($"{baseUrlLtn}/galleries/{illustGroup.Id}.js");
            var ggJS = await HttpGet($"{baseUrlLtn}/gg.js");
            using (var engine=new V8ScriptEngine())
            {
                //注意顺序。要用\n隔开
                var script = galleryInfoJS+"\n"+commonjs+"\n"+ggJS+"\n"+myjs;
                engine.Execute(script);
                //illustGroup有tag，但是既然不做随机浏览队列，tag并没有用处
                var hashs = engine.Script.myhash;
                illustGroup.title = engine.Script.mytitle;                
                foreach(var illust in illustGroup.illusts)
                    database.Illusts.Remove(illust);
                illustGroup.illusts.Clear();//注意Clear并不会删除illust行
                for (var i = 0; i < hashs.length; ++i)
                {
                    var illust = new Illust();
                    //url随时间变化，下载时再计算
                    illust.hash = hashs[i] as string;
                    illust.index=i;//有序
                    illust.fileName = $"{illustGroup.Id}_{i:000}";
                    illustGroup.illusts.Add(illust);
                }
            }
            illustGroup.fetched = true;
            database.SaveChanges();
            Log($"Fetch IllustGroup Done:{illustGroup.Id} {illustGroup.title}");
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
                await FetchIllustGroupListByUser(user);
            Log("Fetch User Done");
            foreach (var illustGroup in (from illustGroup in database.IllustGroups
                                  where illustGroup.fetched==false&&(illustGroup.user.followed==true|| illustGroup.user.queued==true)
                                  select illustGroup).ToList())
                await FetchIllustGroupById(illustGroup);
        }
        //获取该user的作品id并插入数据库
        public async Task FetchIllustGroupListByUser(User user)
        {
            /*
             * https://ltn.hitomi.la/artist/muk-all.nozomi 返回二进制作品ID，在 https://ltn.hitomi.la/galleryblock.js 中解析（以nozomiextension(值=.nozomi)为关键字搜索找到对应代码）：
            var xhr = new XMLHttpRequest();
            xhr.open('GET', nozomi_address);
            xhr.responseType = 'arraybuffer';
            xhr.setRequestHeader('Range', 'bytes=' + start_byte.toString() + '-' + end_byte.toString());
            xhr.onreadystatechange = function (oEvent) {
                if (xhr.readyState === 4) {
                    if (xhr.status === 200 || xhr.status === 206) {
                        var arrayBuffer = xhr.response; // Note: not oReq.responseText
                        if (arrayBuffer) {
                            var view = new DataView(arrayBuffer);
                            var total = view.byteLength / 4;
                            for (var i = 0; i < total; i++) {
                                nozomi.push(view.getInt32(i * 4, false ));
            }
            total_items = parseInt(xhr.getResponseHeader("Content-Range").replace(/^[Bb] ytes \d+-\d+\//, '')) / 4;
            put_results_on_page();
                }
            }
            }
            一次性返回所有作品的id，每4个字节是一个整型ID(注意大小头,需要reverse)，没有多余数据
            Content-Range:bytes 200-299/840表示一共有840字节(即210个作品)，当前页显示第200-299字节代表的作品，但是全部840字节都在Response中
             */
            database.LoadFK(user);
            var illustGroupIds =new HashSet<int>();
            if(user.illustGroups is not null)
                illustGroupIds=user.illustGroups.Select(x => x.Id).ToHashSet();
            var list_binary = await HttpGetBinary($"{baseUrlLtn}/artist/{user.name}-all.nozomi");
            list_binary = list_binary.Reverse().ToArray();
            for (int i = 0; i < list_binary.Length; i += 4)
            {
                var id = System.BitConverter.ToInt32(list_binary, i);
                if (!illustGroupIds.Contains(id))
                {
                    var illustGroup = database.IllustGroups.Where(x=>x.Id == id).FirstOrDefault();
                    if (illustGroup is null)
                    {
                        illustGroup = new IllustGroup();
                        illustGroup.Id = id;
                        database.IllustGroups.Add(illustGroup);
                    }
                    illustGroup.user=user;
                }
            }
            database.SaveChanges();
        }
        public override BaseUser GetUserById(string id)
        {
            return database.Users.Where(x=>x.name==id).FirstOrDefault();
        }
        public override void SetUserFollowOrQueue(BaseUser user) {
            database.SaveChanges();
        }
        public override void SetBookmarkEach(ExplorerFileBase file) {
            database.SaveChanges();
        }
        public override void SetReaded(ExplorerFileBase file) { 
            (file as ExplorerFile).illustGroup.readed = file.readed;
            database.SaveChanges();
        }
        public override void SetBookmarked(ExplorerFileBase file) {
            (file as ExplorerFile).illustGroup.fav = file.bookmarked;
            database.SaveChanges();
        }
        public async Task<string> HttpGet(string url)
        {
            for (int try_ct = 8; try_ct >= 0; --try_ct)
            {
                try
                {
                    //Console.WriteLine("Begin " + try_ct.ToString() + " " + (url.Length>150?url.Substring(0, 150) :url));
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
        public async Task<byte[]> HttpGetBinary(string url)
        {
            for (int try_ct = 8; try_ct >= 0; --try_ct)
            {
                try
                {
                    //Console.WriteLine("Begin " + try_ct.ToString() + " " + (url.Length>150?url.Substring(0, 150) :url));
                    if (string.IsNullOrEmpty(url))
                        throw new ArgumentNullException("url");
                    if (!url.StartsWith("https"))
                        throw new ArgumentException("Not SSL");
                    using (HttpResponseMessage response = await httpClient.GetAsync(url))
                    {
                        //未知错误
                        CheckStatusCode(response);
                        //正常
                        return await response.Content.ReadAsByteArrayAsync();
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
        static public void CheckStatusCode(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new Exception("HTTP Not Success");
        }
    }
}
