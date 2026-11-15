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
    public enum PostDownloadKind
    {
        AttachmentImages,
        ImageArchives
    }

    public record PostDownloadSelection(string Id, string BodyTitle, int? Priority, bool ShouldDownload);

    public record PostDownloadContent(PostDownloadKind Kind, IReadOnlyList<(string Name, string UrlPath)> Files);

    // 第一阶段只返回判定结果，不读取数据库、不修改作品状态或下载队列。
    public static class PostDownloadPolicy
    {
        public static readonly (string[] Keywords, int? PlanPriority, int? DownloadPriority)[] MankaiRules =
        {
            (new[] { @"(?<![\d.,])1000円", "プラン" }, 30, null),
            (new[] { @"(?<![\d.,])500円", "プラン" }, 20, null),
            (new[] { "全体公開" }, 10, null),
            (new[] { "一括ダウンロード" }, null, 1),
            (new[] { "個別ダウンロード" }, null, 0)
        };

        // 调用方选择规则并传入同一作者的完整帖子列表，已读或已收藏的高档版本仍参与比较。
        public static List<PostDownloadSelection> SelectPosts(IEnumerable<(string Id, string Title)> posts,
            IReadOnlyList<(string[] Keywords, int? PlanPriority, int? DownloadPriority)> rules)
        {
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
            for (int i = 0; i < result.Count; i++)
                if (result[i].Priority.HasValue)
                    result[i] = result[i] with
                    {
                        ShouldDownload = result[i].Priority.Value == highestPriorities[result[i].BodyTitle]
                    };
            return result;
        }

        // 传入原帖附件清单，不能只用已被全局去重、拆分到子图组后的 works。
        public static async Task<(bool success, PostDownloadContent content)> ClassifyAttachments(
            IEnumerable<(string Name, string UrlPath)> attachments,
            Func<string, string, CancellationToken, Task<(bool success, List<RemoteArchiveEntry> entries)>> readArchiveEntries,
            CancellationToken cancellationToken = default)
        {
            var images = new List<(string Name, string UrlPath)>();
            var archives = new List<(string Name, string UrlPath)>();
            var seenPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var attachment in attachments)
            {
                var ext = Path.GetExtension(attachment.Name ?? "").ToLowerInvariant();
                if (!ext.IsImage() && !ext.IsZip())
                    continue;
                if (String.IsNullOrWhiteSpace(attachment.UrlPath))
                {
                    Console.Error.WriteLine($"[Paw] Attachment has no path: {attachment.Name}");
                    return (false, null);
                }
                if (!seenPaths.Add(attachment.UrlPath))
                    continue;
                if (ext.IsImage())
                {
                    images.Add(attachment);
                    continue;
                }

                var url = $"https://file.{Server.baseHost}/data{attachment.UrlPath}?f={Uri.EscapeDataString(attachment.Name)}";
                var check = await readArchiveEntries(url, ext, cancellationToken);
                if (!check.success || check.entries is null)
                {
                    Console.Error.WriteLine($"[Paw] Can't classify image archive: {attachment.Name}");
                    return (false, null);
                }
                var files = check.entries.Where(x => !x.IsDirectory).ToList();
                if (files.Count > 0 && !check.entries.Any(x => x.IsEncrypted)
                    && files.All(x => Path.GetExtension(x.FullName ?? "").IsImage()))
                    archives.Add(attachment);
            }
            return archives.Count > 0
                ? (true, new PostDownloadContent(PostDownloadKind.ImageArchives, archives))
                : (true, new PostDownloadContent(PostDownloadKind.AttachmentImages, images));
        }
    }
}
