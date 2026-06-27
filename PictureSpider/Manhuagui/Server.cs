using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PictureSpider.Manhuagui
{
    public class Server : BaseServerWithDB<Database>, IDisposable
    {
        private readonly string downloadDir;
        private readonly HttpClient httpClient;
        private readonly string[] imageHosts = { "i", "eu", "eu1", "eu2", "us", "us1", "us2", "us3" };

        public Server(Config config) : base(config.ManhuaguiConnectStr)
        {
            logPrefix = "MG";
            downloadDir = config.LMangaRootDir;
            Util.TouchDir(downloadDir);
            httpClient = new HttpClient(new HttpClientHandler
            {
                Proxy = new WebProxy(config.Proxy),
                UseProxy = true
            });
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.manhuagui.com/");
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }

        public override Task Init()
        {
#if !DEBUG
            _ = Task.Run(RunSchedule);
#endif
            return Task.CompletedTask;
        }

        private async Task RunSchedule()
        {
            while (true)
            {
                await Task.Delay(new TimeSpan(24, 0, 0));
                var comics = await database.Comics
                    .Where(x => x.Fav)
                    .OrderBy(x => x.Id)
                    .ToListAsync();
                foreach (var comic in comics)
                    await DownloadComic(comic.Id);
            }
        }

        public async Task DownloadComic(int comicId)
        {
            await FetchComic(comicId);
            await DownloadStoredComic(comicId);
        }

        private async Task DownloadStoredComic(int comicId)
        {
            var comic = await database.Comics.FirstAsync(x => x.Id == comicId);
            var chapters = await database.Chapters
                .Where(x => x.ComicId == comic.Id)
                .OrderBy(x => x.Index)
                .ToListAsync();
            var comicDir = Path.Combine(downloadDir, SafeName(comic.Title));
            Util.TouchDir(comicDir);
            Log($"{comic.Title}: {chapters.Count} chapters");

            foreach (var chapter in chapters)
            {
                try
                {
                    await DownloadChapter(comicDir, chapter);
                }
                catch (Exception ex)
                {
                    Log($"{chapter.Title} failed: {ex.Message}");
                }
            }
        }

        private async Task FetchComic(int comicId)
        {
            var comicInfo = await FetchComicInfo(comicId);
            var comic = await database.Comics.FirstOrDefaultAsync(x => x.Id == comicInfo.Id);
            if (comic == null)
            {
                comic = new Comic
                {
                    Id = comicInfo.Id
                };
                database.Comics.Add(comic);
            }
            comic.Title = comicInfo.Title;
            comic.UpdatedAt = DateTime.UtcNow;
            comic.LastCheckedAt = DateTime.UtcNow;

            var chapters = await database.Chapters
                .Where(x => x.ComicId == comic.Id)
                .ToDictionaryAsync(x => x.Id);
            for (var i = 0; i < comicInfo.Chapters.Count; i++)
            {
                var chapterInfo = comicInfo.Chapters[i];
                if (!chapters.TryGetValue(chapterInfo.Id, out var chapter))
                {
                    chapter = new Chapter
                    {
                        Id = chapterInfo.Id,
                        ComicId = comic.Id
                    };
                    database.Chapters.Add(chapter);
                }
                chapter.Title = chapterInfo.Title;
                chapter.Index = i;
                chapter.UpdatedAt = DateTime.UtcNow;
            }
            await database.SaveChangesAsync();
        }

        private async Task<ComicInfo> FetchComicInfo(int comicId)
        {
            var comicUrl = BuildComicUrl(comicId);
            var html = await httpClient.GetStringAsync(comicUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = WebUtility.HtmlDecode(doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim());
            if (string.IsNullOrWhiteSpace(title))
                title = "manhuagui";

            var chapterNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'chapter-list')]//a[contains(@href,'/comic/')]");
            if (chapterNodes == null)
                throw new InvalidOperationException($"No chapters found: {comicUrl}");

            var chapters = chapterNodes
                .Select(x => new ChapterInfo(
                    ParseChapterId(x.GetAttributeValue("href", "")),
                    WebUtility.HtmlDecode(x.GetAttributeValue("title", x.InnerText).Trim())))
                .Where(x => x.Id > 0)
                .Reverse()
                .ToList();

            return new ComicInfo(comicId, title, chapters);
        }

        public override bool ListenerUtil_IsValidUrl(string url)
        {
            return ParseComicId(url) > 0;
        }

        public override async Task<bool> ListenerUtil_FollowUser(string url)
        {
            var comicId = ParseComicId(url);
            if (comicId <= 0)
                return false;

            // ListenerServer 复用其它模块的 Follow 入口；Manhuagui 这里关注的是漫画本身，
            // 并且触发的是 follow/fav，不是其它模块常见的 queued 作者抓取。
            await FetchComic(comicId);
            var comic = await database.Comics.FirstAsync(x => x.Id == comicId);
            comic.Fav = true;
            await database.SaveChangesAsync();
            return true;
        }

        private async Task DownloadChapter(string comicDir, Chapter chapter)
        {
            var chapterUrl = BuildChapterUrl(chapter.ComicId, chapter.Id);
            var data = await FetchChapter(chapterUrl);
            chapter.Title = data.Title;
            chapter.PageCount = data.Images.Count;
            chapter.LastFetchedAt = DateTime.UtcNow;
            var chapterDir = Path.Combine(comicDir, SafeNameWithIndex(chapter.Index + 1, chapter.Title));
            Util.TouchDir(chapterDir);
            Log($"{data.Title}: {data.Images.Count} pages");

            var pages = await database.Pages
                .Where(x => x.ChapterId == chapter.Id)
                .ToDictionaryAsync(x => x.Index);
            for (var i = 0; i < data.Images.Count; i++)
            {
                var imagePath = data.Images[i];
                if (!pages.TryGetValue(i, out var page))
                {
                    page = new Page
                    {
                        ChapterId = chapter.Id,
                        Index = i
                    };
                    database.Pages.Add(page);
                }
                page.ImagePath = imagePath;
                page.FileName = $"{i + 1:D4}{Path.GetExtension(imagePath)}";
                var output = Path.Combine(chapterDir, page.FileName);
                if (File.Exists(output))
                {
                    page.Downloaded = true;
                    page.DownloadedAt ??= DateTime.UtcNow;
                    page.LastError = "";
                    continue;
                }
                try
                {
                    await DownloadImage(chapterUrl, imagePath, data.Query, output);
                    page.Downloaded = true;
                    page.DownloadedAt = DateTime.UtcNow;
                    page.LastError = "";
                }
                catch (Exception ex)
                {
                    page.LastError = ex.Message;
                    Log($"{page.FileName} failed: {ex.Message}");
                }
                await Task.Delay(200);
            }
            await database.SaveChangesAsync();
        }

        private async Task<ChapterData> FetchChapter(string chapterUrl)
        {
            var mobileUrl = chapterUrl.Replace("://www.manhuagui.com/", "://m.manhuagui.com/");
            var html = await httpClient.GetStringAsync(mobileUrl);
            var script = UnpackReaderScript(html);
            var jsonText = Regex.Match(script, @"SMH\.(?:reader|imgData)\((\{.*\})\)\.(?:preInit|init)\(\);").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(jsonText))
                throw new InvalidOperationException($"No reader data found: {chapterUrl}");

            var json = JObject.Parse(jsonText);
            var title = json.Value<string>("chapterTitle") ?? Path.GetFileNameWithoutExtension(chapterUrl);
            var images = json["images"]?.Select(x => x.ToString()).ToList()
                ?? json["files"]?.Select(x => json.Value<string>("path") + x).ToList()
                ?? new List<string>();
            var query = json["sl"]?.Children<JProperty>().ToDictionary(x => x.Name, x => x.Value.ToString())
                ?? new Dictionary<string, string>();

            return new ChapterData(title, images, query);
        }

        private async Task DownloadImage(string chapterUrl, string imagePath, Dictionary<string, string> query, string output)
        {
            Exception lastException = null;
            foreach (var host in imageHosts)
            {
                var url = BuildImageUrl(host, imagePath, query);
                try
                {
                    using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        request.Headers.Referrer = new Uri(chapterUrl.Replace("://www.manhuagui.com/", "://m.manhuagui.com/"));
                        using (var response = await httpClient.SendAsync(request))
                        {
                            response.EnsureSuccessStatusCode();
                            using (var stream = await response.Content.ReadAsStreamAsync())
                            using (var file = File.Create(output))
                                await stream.CopyToAsync(file);
                        }
                    }
                    return;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }
            throw new InvalidOperationException($"Download failed: {imagePath}", lastException);
        }

        private static string BuildImageUrl(string host, string imagePath, Dictionary<string, string> query)
        {
            var url = $"https://{host}.hamreus.com{imagePath}";
            if (query.Count == 0)
                return url;
            return url + "?" + string.Join("&", query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));
        }

        private static string UnpackReaderScript(string html)
        {
            var match = Regex.Match(html, @"\}\('(?<payload>.*?)',(?<base>\d+),(?<count>\d+),'(?<keys>[^']+)'\[", RegexOptions.Singleline);
            if (!match.Success)
                throw new InvalidOperationException("Packed reader script not found.");

            var payload = match.Groups["payload"].Value;
            var radix = int.Parse(match.Groups["base"].Value);
            var count = int.Parse(match.Groups["count"].Value);
            var keys = DecompressFromBase64(match.Groups["keys"].Value).Split('|');

            for (var i = count - 1; i >= 0; i--)
                if (i < keys.Length && !string.IsNullOrEmpty(keys[i]))
                    payload = Regex.Replace(payload, $@"\b{Regex.Escape(ToBase(i, radix))}\b", keys[i]);
            return payload;
        }

        private static string ToBase(int value, int radix)
        {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            return (value >= radix ? ToBase(value / radix, radix) : "") +
                   (value % radix > 35 ? ((char)(value % radix + 29)).ToString() : chars[value % radix].ToString());
        }

        private static string DecompressFromBase64(string input)
        {
            const string keyStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=";
            var reverse = keyStr.Select((c, i) => new { c, i }).ToDictionary(x => x.c, x => x.i);
            return Decompress(input.Length, 32, index => reverse.TryGetValue(input[index], out var value) ? value : 0);
        }

        private static string Decompress(int length, int resetValue, Func<int, int> getNextValue)
        {
            var dictionary = new List<string> { "0", "1", "2" };
            var enlargeIn = 4;
            var dictSize = 4;
            var numBits = 3;
            var data = new LzData(getNextValue(0), resetValue, 1);

            var next = ReadBits(2, resetValue, getNextValue, data);
            string c;
            if (next == 0)
                c = ((char)ReadBits(8, resetValue, getNextValue, data)).ToString();
            else if (next == 1)
                c = ((char)ReadBits(16, resetValue, getNextValue, data)).ToString();
            else
                return "";

            dictionary.Add(c);
            var w = c;
            var result = new StringBuilder(c);

            while (true)
            {
                if (data.Index > length)
                    return "";
                var cc = ReadBits(numBits, resetValue, getNextValue, data);
                if (cc == 0)
                {
                    dictionary.Add(((char)ReadBits(8, resetValue, getNextValue, data)).ToString());
                    cc = dictSize++;
                    enlargeIn--;
                }
                else if (cc == 1)
                {
                    dictionary.Add(((char)ReadBits(16, resetValue, getNextValue, data)).ToString());
                    cc = dictSize++;
                    enlargeIn--;
                }
                else if (cc == 2)
                    return result.ToString();

                if (enlargeIn == 0)
                {
                    enlargeIn = 1 << numBits;
                    numBits++;
                }

                string entry;
                if (cc < dictionary.Count)
                    entry = dictionary[cc];
                else if (cc == dictSize)
                    entry = w + w[0];
                else
                    throw new InvalidOperationException("Invalid Manhuagui reader data.");
                result.Append(entry);
                dictionary.Add(w + entry[0]);
                dictSize++;
                enlargeIn--;
                w = entry;

                if (enlargeIn == 0)
                {
                    enlargeIn = 1 << numBits;
                    numBits++;
                }
            }
        }

        private static int ReadBits(int count, int resetValue, Func<int, int> getNextValue, LzData data)
        {
            var bits = 0;
            var maxPower = 1 << count;
            for (var power = 1; power != maxPower; power <<= 1)
            {
                var resb = data.Value & data.Position;
                data.Position >>= 1;
                if (data.Position == 0)
                {
                    data.Position = resetValue;
                    data.Value = getNextValue(data.Index++);
                }
                if (resb > 0)
                    bits |= power;
            }
            return bits;
        }

        private static string BuildComicUrl(int comicId)
        {
            return $"https://www.manhuagui.com/comic/{comicId}/";
        }

        private static string BuildChapterUrl(int comicId, int chapterId)
        {
            return $"https://www.manhuagui.com/comic/{comicId}/{chapterId}.html";
        }

        private static int ParseComicId(string url)
        {
            var match = Regex.Match(url ?? "", @"^https?://(?:www|m)\.manhuagui\.com/comic/(\d+)(?:/|/[\d]+\.html)?(?:[?#].*)?$");
            if (!match.Success)
                return 0;
            return int.Parse(match.Groups[1].Value);
        }

        private static int ParseChapterId(string url)
        {
            var match = Regex.Match(url, @"/comic/\d+/(\d+)\.html");
            if (!match.Success)
                return 0;
            return int.Parse(match.Groups[1].Value);
        }

        private static string SafeName(string name)
        {
            name = string.IsNullOrWhiteSpace(name) ? "untitled" : name;
            return name.ReplaceInvalidCharInFilenameWithReturnValue();
        }

        private static string SafeNameWithIndex(int index, string name)
        {
            return $"{index:D4}_{SafeName(name)}";
        }

        private class LzData
        {
            public int Value;
            public int Position;
            public int Index;

            public LzData(int value, int position, int index)
            {
                Value = value;
                Position = position;
                Index = index;
            }
        }

        private class ComicInfo
        {
            public int Id { get; }
            public string Title { get; }
            public List<ChapterInfo> Chapters { get; }

            public ComicInfo(int id, string title, List<ChapterInfo> chapters)
            {
                Id = id;
                Title = title;
                Chapters = chapters;
            }
        }

        private class ChapterInfo
        {
            public int Id { get; }
            public string Title { get; }

            public ChapterInfo(int id, string title)
            {
                Id = id;
                Title = title;
            }
        }

        private class ChapterData
        {
            public string Title { get; }
            public List<string> Images { get; }
            public Dictionary<string, string> Query { get; }

            public ChapterData(string title, List<string> images, Dictionary<string, string> query)
            {
                Title = title;
                Images = images;
                Query = query;
            }
        }
    }
}
