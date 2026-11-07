using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PictureSpider.LocalSingleFile
{
    public partial class Server
    {
        // 只读预览，不接入队列加载或定时任务；作者/作品多层目录不递归参与匹配。
        public static Dictionary<string, AuthorMatchResult> MatchAuthorDirectories(IEnumerable<string> roots,
            IEnumerable<(long AuthorId, string Name, bool AllowAutoMatch, bool IsFallback)> aliases)
        {
            var directories = new List<string>();
            foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(root))
                    continue;
                foreach (var directory in Directory.EnumerateDirectories(root))
                {
                    var name = AuthorMatcher.NormalizeName(Path.GetFileName(directory));
                    if (name != "未知作者" && !Regex.IsMatch(name, @"^[+-]?\d+$")
                        && Directory.EnumerateFiles(directory).Any(x => valid_exts.Contains(Path.GetExtension(x).ToLowerInvariant())))
                        directories.Add(Path.GetFullPath(directory));
                }
            }
            var matches = AuthorMatcher.MatchAuthors(directories.Select(Path.GetFileName), aliases);
            return directories.Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, NaturalStringComparer.Instance)
                .ToDictionary(x => x, x => matches[Path.GetFileName(x)], StringComparer.OrdinalIgnoreCase);
        }
    }
}
