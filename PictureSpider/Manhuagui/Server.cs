using HtmlAgilityPack;
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
    public class Server : BaseServer, IDisposable
    {
        private readonly string downloadDir;
        private readonly List<string> comicUrls;
        private readonly HttpClient httpClient;
        private readonly string[] imageHosts = { "i", "eu", "eu1", "eu2", "us", "us1", "us2", "us3" };

        public Server(Config config)
        {
            logPrefix = "MG";
            downloadDir = config.ManhuaguiDownloadDir;
            comicUrls = config.ManhuaguiComicUrls;
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
            return Task.CompletedTask;
        }

        public async Task DownloadTrackedComics()
        {
            foreach (var comicUrl in comicUrls)
                await DownloadComic(comicUrl);
        }

        public async Task DownloadComic(string comicUrl)
        {
            var comic = await FetchComic(comicUrl);
            var comicDir = Path.Combine(downloadDir, SafeName(comic.Title));
            Util.TouchDir(comicDir);
            Log($"{comic.Title}: {comic.Chapters.Count} chapters");

            foreach (var chapter in comic.Chapters)
                await DownloadChapter(comicDir, chapter);
        }

        private async Task<Comic> FetchComic(string comicUrl)
        {
            var html = await httpClient.GetStringAsync(NormalizeUrl(comicUrl));
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var title = WebUtility.HtmlDecode(doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim());
            if (string.IsNullOrWhiteSpace(title))
                title = "manhuagui";

            var chapterNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'chapter-list')]//a[contains(@href,'/comic/')]");
            if (chapterNodes == null)
                throw new InvalidOperationException($"No chapters found: {comicUrl}");

            var chapters = chapterNodes
                .Select(x => new Chapter(
                    WebUtility.HtmlDecode(x.GetAttributeValue("title", x.InnerText).Trim()),
                    NormalizeUrl(x.GetAttributeValue("href", ""))))
                .Where(x => Regex.IsMatch(x.Url, @"/comic/\d+/\d+\.html$"))
                .Reverse()
                .ToList();

            return new Comic(title, chapters);
        }

        private async Task DownloadChapter(string comicDir, Chapter chapter)
        {
            var data = await FetchChapter(chapter.Url);
            var chapterDir = Path.Combine(comicDir, SafeName(data.Title));
            Util.TouchDir(chapterDir);
            Log($"{data.Title}: {data.Images.Count} pages");

            for (var i = 0; i < data.Images.Count; i++)
            {
                var imagePath = data.Images[i];
                var output = Path.Combine(chapterDir, $"{i + 1:D4}{Path.GetExtension(imagePath)}");
                if (File.Exists(output))
                    continue;
                await DownloadImage(chapter.Url, imagePath, data.Query, output);
                await Task.Delay(200);
            }
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

        private static string NormalizeUrl(string url)
        {
            if (url.StartsWith("//"))
                return "https:" + url;
            if (url.StartsWith("/"))
                return "https://www.manhuagui.com" + url;
            return url;
        }

        private static string SafeName(string name)
        {
            name = string.IsNullOrWhiteSpace(name) ? "untitled" : name;
            return name.ReplaceInvalidCharInFilenameWithReturnValue();
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

        private class Comic
        {
            public string Title { get; }
            public List<Chapter> Chapters { get; }

            public Comic(string title, List<Chapter> chapters)
            {
                Title = title;
                Chapters = chapters;
            }
        }

        private class Chapter
        {
            public string Title { get; }
            public string Url { get; }

            public Chapter(string title, string url)
            {
                Title = title;
                Url = url;
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
