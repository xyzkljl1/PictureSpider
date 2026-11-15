using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PictureSpider.Pawchive
{
    public enum PostVersionRule
    {
        None,
        TitlePriority
    }

    public record PostDownloadSelection(string Id, string BodyTitle, int? Priority, bool ShouldDownload);

    // 只返回判定结果，不读取数据库、不修改作品状态或下载队列。
    public static class PostDownloadPolicy
    {
        public static readonly (string[] Keywords, int? PlanPriority, int? DownloadPriority)[] TitlePriorityRules =
        {
            (new[] { @"(?<![\d.,])1000円", "プラン" }, 30, null),
            (new[] { @"(?<![\d.,])500円", "プラン" }, 20, null),
            (new[] { "全体公開" }, 10, null),
            (new[] { "一括ダウンロード" }, null, 1),
            (new[] { "個別ダウンロード" }, null, 0)
        };

        // 旧帖仅作为比较依据，只返回待判断帖子的结果。
        public static List<PostDownloadSelection> SelectPosts(IEnumerable<(string Id, string Title)> posts,
            PostVersionRule versionRule, IReadOnlySet<string> pendingIds)
        {
            var rules = versionRule == PostVersionRule.TitlePriority ? TitlePriorityRules
                : Array.Empty<(string[] Keywords, int? PlanPriority, int? DownloadPriority)>();
            // 仅提取两端连续的括号说明，正文中间的括号不参与识别。
            const string content = @"[^【】\[\]［］()（）〔〕「」]*";
            var annotation = @"(?<annotation>【" + content + @"】|\[" + content + @"\]|［" + content
                + @"］|\(" + content + @"\)|（" + content + @"）|〔" + content + @"〕|「" + content + @"」)";
            var edges = new Regex(@"\A\s*(?:" + annotation + @"\s*)+|\s*(?:" + annotation + @"\s*)+\z");
            const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            var result = new List<PostDownloadSelection>();
            foreach (var post in posts)
            {
                var bodyTitle = post.Title;
                int? priority = null;
                if (!String.IsNullOrEmpty(post.Title))
                {
                    var plans = new HashSet<int>();
                    var downloads = new HashSet<int>();
                    var recognized = new List<Capture>();
                    var ambiguous = false;
                    foreach (Match edge in edges.Matches(post.Title))
                        foreach (Capture part in edge.Groups["annotation"].Captures)
                        {
                            var text = Regex.Replace(part.Value.Substring(1, part.Length - 2)
                                .Normalize(NormalizationForm.FormKC), @"\s+", "");
                            var matches = rules.Where(x => x.Keywords.All(keyword => Regex.IsMatch(text, keyword, options))).ToList();
                            var remaining = text;
                            foreach (var rule in matches)
                                foreach (var keyword in rule.Keywords)
                                    remaining = Regex.Replace(remaining, keyword, "", options);
                            remaining = Regex.Replace(remaining, @"以上|限定|ver\.?|用|[\p{P}]", "", options);
                            if (matches.Count == 0 || remaining.Length > 0)
                            {
                                // 未知档次、形式或预览说明不参与淘汰；普通正文括号原样保留。
                                if (matches.Count > 0 || Regex.IsMatch(text,
                                    @"プラン|ダウンロード|形式|sample|preview|\d[\d.,]*円", options))
                                    ambiguous = true;
                                continue;
                            }
                            foreach (var rule in matches)
                            {
                                if (rule.PlanPriority.HasValue)
                                    plans.Add(rule.PlanPriority.Value);
                                if (rule.DownloadPriority.HasValue)
                                    downloads.Add(rule.DownloadPriority.Value);
                            }
                            recognized.Add(part);
                        }
                    if (!ambiguous && plans.Count == 1 && downloads.Count <= 1)
                    {
                        var body = post.Title;
                        foreach (var part in recognized.OrderByDescending(x => x.Index))
                            body = body.Remove(part.Index, part.Length);
                        body = body.Trim();
                        if (body.Length > 0)
                        {
                            bodyTitle = body;
                            priority = plans.Single() + downloads.SingleOrDefault();
                        }
                    }
                }
                result.Add(new PostDownloadSelection(post.Id, bodyTitle, priority, true));
            }

            var highestPriorities = result.Where(x => x.Priority.HasValue)
                .GroupBy(x => x.BodyTitle, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Max(p => p.Priority.Value), StringComparer.Ordinal);
            result.RemoveAll(x => !pendingIds.Contains(x.Id));
            for (int i = 0; i < result.Count; i++)
                if (result[i].Priority.HasValue)
                    result[i] = result[i] with
                    {
                        ShouldDownload = result[i].Priority.Value == highestPriorities[result[i].BodyTitle]
                    };
            return result;
        }

        // 每个附件独立检查，不影响同帖的图片、视频或其它附件。
        public static async Task<(bool success, bool isImageArchive)> CheckImageArchive(string name, string urlPath,
            Func<string, string, CancellationToken, Task<(bool success, List<RemoteArchiveEntry> entries)>> readArchiveEntries,
            CancellationToken cancellationToken = default)
        {
            var ext = Path.GetExtension(name ?? "").ToLowerInvariant();
            if (!ext.IsZip())
                return (true, false);
            if (String.IsNullOrWhiteSpace(urlPath))
            {
                Console.Error.WriteLine($"[Paw] Attachment has no path: {name}");
                return (false, false);
            }
            var url = $"https://file.{Server.baseHost}/data{urlPath}?f={Uri.EscapeDataString(name)}";
            var check = await readArchiveEntries(url, ext, cancellationToken);
            if (!check.success || check.entries is null)
            {
                Console.Error.WriteLine($"[Paw] Can't classify image archive: {name}");
                return (false, false);
            }
            var files = check.entries.Where(x => !x.IsDirectory).ToList();
            return (true, files.Count > 0 && !check.entries.Any(x => x.IsEncrypted)
                && files.All(x => Path.GetExtension(x.FullName ?? "").IsImage()));
        }
    }
}
