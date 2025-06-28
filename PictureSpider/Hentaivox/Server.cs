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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.RegularExpressions;
using System.Threading;
using PictureSpider.Kemono;
using System.Security.Policy;
using Microsoft.AspNetCore.SignalR;

namespace PictureSpider.Hentaivox
{
    public partial class Server : TypicalServer<Database,Work, WorkGroup, User>, IDisposable
    {
        private string base_host = "hentaivox.com";
        private string baseUrl = "https://hentaivox.com";
       
        public Server(Config config): base(config.Proxy,config)
        {

            downloader = new Aria2DownloadQueue(Downloader.DownloaderPostfix.Hentaivox, config.Proxy, "https://hentaivox.com",1);
        }
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
#pragma warning disable CS1998 // 此异步方法缺少 "await" 运算符，将以同步方式运行
#pragma warning disable CS0162 // 检测到无法访问的代码
        public override async Task Init()
        {
#if DEBUG
            await FetchWorkGroup(database.WorkGroups.Where(x=>x.Id== 456356).First());
            return;
#endif
            Task.Run(RunSchedule);
        }
#pragma warning restore CS0162
#pragma warning restore CS4014
#pragma warning restore CS1998
        //获取该user的作品id并插入数据库
        protected override async Task FetchworkGroupListByUser(User user)
        {
            //没找到api，只能解析html了
            /*
             * <div class="gallery-wrapper">
                <div class="gallery ">
                    <h3 class="gallery-type">
                        <a href="https://hentaivox.com/category/comic" rel="nofollow">
                            Comic
                        </a><span class="flag flag-jpn"></span>
                    </h3><a class="gallery-thumb" href="https://hentaivox.com/view/456356" style="padding:0 0 142% 0"><img class="lazy small-bg-load" data-src="https://a2.hentaivox.com/i/images/986203-thumb.jpg" /></a>
                    <div class="gallery-name">
                        <h2>
                            <a href="https://hentaivox.com/view/456356">
                                ARTIST diathorn
                            </a>
                        </h2>
                    </div>
                </div>
            </div>
             */
            var workGroupIds = new HashSet<int>();
            if (user.workGroups is not null)
                workGroupIds = user.workGroups.Select(x => x.Id).ToHashSet();
            int ct = 0;
            for (int page = 1; ; page++) 
            {
                var url = page==1?$"https://hentaivox.com/artist/{user.name}": $"https://hentaivox.com/artist/{user.name}/{page}";
                var doc = await HttpGetHTML(url);
                if (doc is null)
                {
                    LogError($"fail to get {user.name}({user.Id}) page {page}");
                }
                foreach (var gallery_node in doc.DocumentNode.SelectNodes("//div[@class='gallery ']"))//有个空格
                    try
                    {
                        var name_node = gallery_node.SelectSingleNode(".//div[@class='gallery-name']");
                        var workGroupUrl = name_node.SelectSingleNode(".//a").Attributes["href"].Value;
                        var id = Int32.Parse(workGroupUrl.Split('/').Last());
                        if (workGroupIds.Contains(id)) // 没有需要更新的信息
                            continue;
                        var workGroup = new WorkGroup();
                        var title = name_node.SelectSingleNode(".//a").InnerText.Trim();
                        workGroup.title = title.ReplaceInvalidCharInFilenameWithReturnValue();
                        workGroup.Id = id;
                        workGroup.fetched = false;
                        workGroup.user = user;
                        database.WorkGroups.Add(workGroup);
                        ct++;
                    }
                    catch (Exception)
                    {
                        LogError($"fail to parse WorkGroup {user.name}({user.Id}) page {page}");
                    }
                await database.SaveChangesAsync();//每页存一次
                var next_page =doc.DocumentNode.SelectSingleNode("//a[@class='page-link'][@rel='next']");
                if(next_page is null)
                    break;
            }
            Log($"Fetch user {user.name} Done: +{ct} WorkGroups");
        }
        //获取workGroup详细信息
        protected async override Task FetchWorkGroup(WorkGroup workGroup)
        {
            if (workGroup.works.Count>0)
                return;
            var doc = await HttpGetHTML($"https://hentaivox.com/view/{workGroup.Id}");
            if (doc is null)
            {
                LogError($"Fail to fetch workGroup {workGroup.Id}");
                return;
            }
            /*
             * 
            <script type="text/javascript">
            plausible("gallery", {
                props: {
                    did: "1562177"
                }
            });
            </script>
             */
            int internalId = 0;
            foreach(var node in doc.DocumentNode.SelectNodes("//script[@type='text/javascript']"))
            {
                var script = node.InnerText;
                if (script.Contains("plausible"))
                {
                    var regex = new Regex("did: \"([0-9]+)\"");
                    var results = regex.Match(script).Groups;
                    if (results.Count > 1)
                    {
                        internalId = Int32.Parse(results[1].Value);
                        break;
                    }
                }
            }
            if(internalId == 0)
            {
                LogError($"Fail to get did workGroup {workGroup.Id}");
                return;
            }
            int pages = 0;
            /*<div class="tag-container field-name">Pages: 8</div>*/
            foreach(var node in doc.DocumentNode.SelectNodes("//div[@class='tag-container field-name']"))
            {
                var regex = new Regex("Pages:[ ]*([0-9]+)");
                var results = regex.Match(node.InnerText).Groups;
                if (results.Count > 1)
                {
                    pages = Int32.Parse(results[1].Value);
                    break;
                }
            }
            //         <div id="gallery-pages" class="container-xl">
            var thumb_parent = doc.DocumentNode.SelectSingleNode("//div[@id='gallery-pages']");
            if (thumb_parent is null)
            {
                LogError($"Fail to get thumb nodes {workGroup.Id}");
                return;
            }
            // <img class="lazy small-bg-load" data-src="https://a2.hentaivox.com/i/images/986203-2t.jpg" width="200" height="135" />
            var thumb_nodes = thumb_parent.SelectNodes(".//img[@class='lazy small-bg-load']");

            for (int i = 1; i <= pages; i++)
            {
                var work = new Work();
                database.Works.Add(work);
                work.workGroup = workGroup;
                work.index = i;
                work.title = $"{i}";
                // 假设thumb的ext和原图一样
                var thumb_url=thumb_nodes[i].Attributes["data-src"].Value;
                work.ext = Path.GetExtension(thumb_url);
                //是否都是a2?
                work.url = $"https://a2.hentaivox.com/i/images/{internalId}-{i}{work.ext}";
                work.fileName = $"{workGroup.Id}_{i:d3}";
                work.ext = ".jpg";
                workGroup.works.Add(work);
            }
            workGroup.fetched = true;
            await database.SaveChangesAsync();
        }

        
        public override bool ListenerUtil_IsValidUrl(string url)
        {
            return url.StartsWith($"{baseUrl}");
        }
        public override async Task<bool> ListenerUtil_FollowUser(string url)
        {
            if (url.StartsWith($"{baseUrl}/artist/"))
            {
                var regex = new Regex($"{baseUrl}/artist/([^/]+)(/[0-9]+)?");
                var results = regex.Match(url).Groups;
                if (results.Count > 1)
                {
                    var id = results[1].Value;
                    return await AddQueuedUser(id);
                }
            }
            return false;
        }
    }
}
