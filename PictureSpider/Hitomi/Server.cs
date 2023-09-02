using HtmlAgilityPack;
using Microsoft.AspNetCore.WebUtilities;
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
        private string base_url = "https://hitomi.la";
        private string base_url_ltn = "https://ltn.hitomi.la";
        private string illustReaderJS = "";

        public Server(Config config)
        {
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
            //httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("identity");
            //httpClient.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
            //httpClient.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
            //httpClient.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
            //httpClient.DefaultRequestHeaders.Add("Origin", "https://hitomi.la");
            //httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Chromium\";v=\"116\", \"Not)A;Brand\";v=\"24\", \"Google Chrome\";v=\"116\"");
            //httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
            //httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            //httpClient.DefaultRequestHeaders.Add("Range", "bytes=200-299");
            //httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
        }
        public void Dispose()
        {
            httpClient.Dispose();
        }
        public override async Task Init()
        {
            //await FetchIllustGroupList();
            await PrepareJS();
            var tmp = database.IllustGroups.Where(x => x.id == 2360191).FirstOrDefault();
            //await FetchIllustGroup(tmp);

        }
        private async Task RunSchedule()
        {
            int last_daily_task = DateTime.Now.Day;
            var day_of_week = DateTime.Now.DayOfWeek;
            do
            {
                if (DateTime.Now.Day != last_daily_task)
                {
                    last_daily_task = DateTime.Now.Day;
                    await FetchIllustGroupList();
                }
                await Task.Delay(new TimeSpan(1, 0, 0));//每隔一个小时执行一次
            }
            while (true);
        }
        //获取图片真实地址时使用的js不会经常变化，因此只在启动程序时获取一次
        private async Task PrepareJS()
        {
            var commonJS = "";
            {
                //common.js有一些自动执行的代码没有卵用，不提供对应环境又会报错，需要去掉
                //自动处理js太麻烦了，此处选择手动处理后在本地存一份(作为嵌入资源)
                //需要在本地common.js的属性->生成操作里选择嵌入资源
                //更新common.js时需要去除顶层代码和document相关
                //var commonJS = await HttpGet($"{base_url_ltn}/common.js");
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("PictureSpider.Resources.common.js"))
                    using(var reader = new StreamReader(stream))
                        commonJS=reader.ReadToEnd();
            }
            //gg.js和每个illustGroup对应的js不需要额外处理
            var ggJS = await HttpGet($"{base_url_ltn}/gg.js");
            //common在前，因为common.js中定义了gg对象，gg.js中只有赋值
            //要用\n隔开
            illustReaderJS = commonJS + "\n" + ggJS + "\n";
        }
        //获取illustGroup详细信息
        private async Task FetchIllustGroup(IllustGroup illustGroup)
        {
            //从https://ltn.hitomi.la/galleries/2360191.js获取galleryInfo,在reader.js中解析，用到了common.js和gg.js
            //图片路径形如https://[子域名].hitomi.la/webp/[常数]/[根据hash计算]/[hash].[扩展名]
            //借用common.js/gg.js，加上一段自己的js计算出图片路径
            database.LoadFK(illustGroup);
            var galleryInfoJS = await HttpGet($"{base_url_ltn}/galleries/{illustGroup.id}.js");
            using(var engine=new V8ScriptEngine())
            {
                //需要galleryInfoJS在前，因为计算代码在illustReaderJS结尾，其中使用了galleryInfo
                //要用\n隔开
                var script = galleryInfoJS+"\n"+illustReaderJS + "\n"
                    + @"var gid=galleryinfo.id;
                        var myurl=[];
                        var myhash=[];
                        for(let file of galleryinfo.files){
                            var src=url_from_url_from_hash(galleryinfo.id,file,'webp',undefined,'a');
                            myurl.push(src);
                            myhash.push(file.hash);
                        }";
                engine.Execute(script);
                var urls = engine.Script.myurl;
                var hashs = engine.Script.myhash;
                illustGroup.illusts.Clear();
                for (var i = 0; i < urls.length; ++i)
                {
                    var illust = new Illust();
                    illust.url = urls[i] as string;
                    illust.hash=hashs[i] as string;
                    illustGroup.illusts.Add(illust);
                }
            }
            database.SaveChanges();
        }
        //获取作品列表
        private async Task FetchIllustGroupList()
        {
            //注意linq语句产生的Iqueryable不是立即返回，而是一直占用连接,此期间无法进行其它查询
            //加上ToList令查询完成后再执行循环
            //获取follow/queue作者的作品
            foreach (var user in (from user in database.Users
                                  where user.followed == true || user.queued==true
                                  select user).ToList())
                await FetchIllustGroupListByUser(user);
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
                illustGroupIds=user.illustGroups.Select(x => x.id).ToHashSet();
            var list_binary = await HttpGetBinary($"{base_url_ltn}/artist/{user.id}-all.nozomi");
            list_binary = list_binary.Reverse().ToArray();
            for (int i = 0; i < list_binary.Length; i += 4)
            {
                var id = System.BitConverter.ToInt32(list_binary, i);
                if (!illustGroupIds.Contains(id))
                {
                    var illustGroup = database.IllustGroups.Where(x=>x.id == id).FirstOrDefault();
                    if (illustGroup is null)
                    {
                        illustGroup = new IllustGroup();
                        illustGroup.id = id;
                        database.IllustGroups.Add(illustGroup);
                    }
                    illustGroup.user=user;
                }
            }
            database.SaveChanges();
        }
        public override BaseUser GetUserById(string id)
        {
            return database.Users.Where(x=>x.id==id).FirstOrDefault();
        }
        public async Task<HtmlDocument> HttpGetHtml(string url)
        {
            var doc = new HtmlDocument();
            var result = await HttpGet(url);
            if (result is null)
                return null;
            doc.LoadHtml(result);
            return doc;
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
                        Console.WriteLine(msg + "Re Try " + try_ct.ToString() + " On :" + url);
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
                        Console.WriteLine(msg + "Re Try " + try_ct.ToString() + " On :" + url);
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
