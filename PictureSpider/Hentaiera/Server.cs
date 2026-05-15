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
using HtmlAgilityPack;

namespace PictureSpider.Hentaiera
{
    public partial class Server : TypicalServer<Database,Work, WorkGroup, User>, IDisposable
    {
        private string base_host = "hentaiera.com";
        private string baseUrl = "https://hentaiera.com";
       
        public Server(Config config): base(config.Proxy,config)
        {
            downloader = new Aria2DownloadQueue(Downloader.DownloaderPostfix.Hentaiera, config.Proxy, baseUrl,1);
        }
#pragma warning disable CS4014 // 由于此调用不会等待，因此在调用完成前将继续执行当前方法
#pragma warning disable CS1998 // 此异步方法缺少 "await" 运算符，将以同步方式运行
#pragma warning disable CS0162 // 检测到无法访问的代码
        public override async Task Init()
        {
#if DEBUG
            //await FetchUserAndWorkGroups();
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
                        <a href="https://hentaiera.com/category/comic" rel="nofollow">
                            Comic
                        </a><span class="flag flag-jpn"></span>
                    </h3><a class="gallery-thumb" href="https://hentaiera.com/view/456356" style="padding:0 0 142% 0"><img class="lazy small-bg-load" data-src="https://a2.hentaiera.com/i/images/986203-thumb.jpg" /></a>
                    <div class="gallery-name">
                        <h2>
                            <a href="https://hentaiera.com/view/456356">
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
            var nextUrl = $"{baseUrl}/artist/{user.name}/";
            for (int page = 1; !string.IsNullOrEmpty(nextUrl); page++) 
            {
                var url = nextUrl;
                nextUrl = "";
                var doc = await HttpGetHTML(url);
                if (doc is null)
                {
                    LogError($"fail to get {user.name}({user.Id}) page {page}");
                    break;
                }
                foreach (var gallery_node in SelectGalleryNodes(doc))
                    try
                    {
                        var link = gallery_node.SelectSingleNode(".//h2[contains(concat(' ', normalize-space(@class), ' '), ' gallery_title ')]//a[contains(@href, '/gallery/')]")
                                   ?? gallery_node.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' gallery-name ')]//a")
                                   ?? gallery_node.SelectSingleNode(".//a[contains(@href, '/gallery/')]")
                                   ?? gallery_node.SelectSingleNode(".//a[contains(@href, '/view/')]");
                        if (link is null)
                            continue;
                        var workGroupUrl = link.Attributes["href"].Value;
                        var id = Int32.Parse(workGroupUrl.TrimEnd('/').Split('/').Last());
                        if (workGroupIds.Contains(id)) // 没有需要更新的信息
                            continue;
                        var workGroup = new WorkGroup();
                        var title = HtmlEntity.DeEntitize(link.InnerText).Trim();
                        workGroup.title = title.ReplaceInvalidCharInFilenameWithReturnValue();
                        workGroup.Id = id;
                        workGroup.fetched = false;
                        workGroup.user = user;
                        database.WorkGroups.Add(workGroup);
                        workGroupIds.Add(id);
                        ct++;
                    }
                    catch (Exception)
                    {
                        LogError($"fail to parse WorkGroup {user.name}({user.Id}) page {page}");
                    }
                await database.SaveChangesAsync();//每页存一次
                var next_page = doc.DocumentNode.SelectSingleNode("//a[contains(concat(' ', normalize-space(@class), ' '), ' page-link ') and contains(normalize-space(.), 'Next')]")
                                ?? doc.DocumentNode.SelectSingleNode("//a[contains(concat(' ', normalize-space(@class), ' '), ' page-link ') and @rel='next']")
                                ?? doc.DocumentNode.SelectSingleNode("//a[@rel='next']");
                nextUrl = ResolveUrl(next_page?.GetAttributeValue("href", ""));
            }
            Log($"Fetch user {user.name} Done: +{ct} WorkGroups");
        }
        //获取workGroup详细信息
        protected async override Task FetchWorkGroup(WorkGroup workGroup)
        {
            var doc = await HttpGetHTML($"{baseUrl}/gallery/{workGroup.Id}/");
            if (doc is null)
            {
                LogError($"Fail to fetch workGroup {workGroup.Id}");
                return;
            }
            int pages = 0;
            /*<div class="tag-container field-name">Pages: 8</div>*/
            foreach(var node in doc.DocumentNode.SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' tag-container ')]")?.ToList() ?? new List<HtmlNode>())
            {
                var regex = new Regex("Pages:[ ]*([0-9]+)", RegexOptions.IgnoreCase);
                var results = regex.Match(node.InnerText).Groups;
                if (results.Count > 1)
                {
                    pages = Int32.Parse(results[1].Value);
                    break;
                }
            }
            var thumb_nodes = SelectPageThumbs(doc);
            if (thumb_nodes.Count == 0)
            {
                LogError($"Fail to get thumb nodes {workGroup.Id}");
                return;
            }
            if (pages == 0)
                pages = thumb_nodes.Count;

            for (int i = 1; i <= pages && i <= thumb_nodes.Count; i++)
            {
                var work = workGroup.works?.FirstOrDefault(x => x.index == i);
                if (work is null)
                {
                    work = new Work();
                    database.Works.Add(work);
                    work.workGroup = workGroup;
                    workGroup.works.Add(work);
                }
                work.index = i;
                work.title = $"{i}";
                var thumb_url = GetImageUrl(thumb_nodes[i - 1]);
                var url = await FetchOriginalImageUrl(workGroup.Id, i, thumb_url);
                if (string.IsNullOrWhiteSpace(url))
                {
                    LogError($"Fail to get original image url {workGroup.Id}/{i}");
                    continue;
                }
                if (work.url != url)
                {
                    work.url = url;
                    work.downloaded = false;
                }
                work.ext = Path.GetExtension(url);
                work.fileName = $"{workGroup.Id}_{i:d3}";
                work.ext = work.ext.ToLowerInvariant();
            }
            workGroup.fetched = true;
            await database.SaveChangesAsync();
        }

        private List<HtmlNode> SelectGalleryNodes(HtmlDocument doc)
        {
            return doc.DocumentNode
                .SelectNodes("//div[contains(concat(' ', normalize-space(@class), ' '), ' gallery ')] | //div[contains(concat(' ', normalize-space(@class), ' '), ' thumb ')]")
                ?.ToList() ?? new List<HtmlNode>();
        }

        private List<HtmlNode> SelectPageThumbs(HtmlDocument doc)
        {
            return doc.DocumentNode
                .SelectNodes("//img[contains(@data-src, 'hentaiera.com') or contains(@src, 'hentaiera.com')]")
                ?.Where(node => Regex.IsMatch(GetImageUrl(node), @"/[0-9]+t\.[a-zA-Z0-9]+$"))
                .ToList() ?? new List<HtmlNode>();
        }

        private string GetImageUrl(HtmlNode node)
        {
            var url = node.GetAttributeValue("data-src", "");
            if (string.IsNullOrWhiteSpace(url))
                url = node.GetAttributeValue("src", "");
            return url;
        }

        private async Task<string> FetchOriginalImageUrl(int workGroupId, int page, string fallbackUrl)
        {
            var doc = await HttpGetHTML($"{baseUrl}/view/{workGroupId}/{page}/");
            var img = doc?.DocumentNode.SelectSingleNode("//img[@id='gimg']")
                      ?? doc?.DocumentNode.SelectSingleNode($"//img[contains(concat(' ', normalize-space(@class), ' '), ' image_{page} ')]")
                      ?? doc?.DocumentNode.SelectSingleNode("//img[contains(@data-src, 'hentaiera.com') or contains(@src, 'hentaiera.com')]");
            var url = img is null ? "" : GetImageUrl(img);
            if (!string.IsNullOrWhiteSpace(url))
                return ResolveUrl(url);
            return Regex.Replace(fallbackUrl, @"([0-9]+)t(\.[a-zA-Z0-9]+)$", "$1$2");
        }

        private string ResolveUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url == "#")
                return "";
            if (url.StartsWith("//"))
                return "https:" + url;
            if (url.StartsWith("/"))
                return baseUrl + url;
            return url;
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
