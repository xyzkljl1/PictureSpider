using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PictureSpider
{
    public enum AuthorMatchStatus
    {
        Matched,
        Unmatched,
        NeedsReview
    }

    public record AuthorMatchResult(string OriginalName, AuthorMatchStatus Status, long? AuthorId, string WorkName);

    public static class AuthorMatcher
    {
        public static string NormalizeName(string name)
        {
            var value = Regex.Replace(name.Normalize(NormalizationForm.FormKC), @"\p{Cf}", "");
            return Regex.Replace(value, @"\s+", " ").Trim();
        }

        // 每批分别构建普通和兜底别名的正则；同名目录复用结果。
        public static Dictionary<string, AuthorMatchResult> MatchAuthors(IEnumerable<string> names,
            IEnumerable<(long AuthorId, string Name, bool AllowAutoMatch, bool IsFallback)> aliases)
        {
            var match = CreateMatcher(aliases);
            return names.Distinct(StringComparer.Ordinal).ToDictionary(x => x, match, StringComparer.Ordinal);
        }

        public static Func<string, AuthorMatchResult> CreateMatcher(
            IEnumerable<(long AuthorId, string Name, bool AllowAutoMatch, bool IsFallback)> aliases)
        {
            var aliasesByFallback = aliases.Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => (x.AuthorId, Name: NormalizeName(x.Name), x.AllowAutoMatch, x.IsFallback))
                .ToLookup(x => x.IsFallback);
            var matchers = new[] { false, true }.Select(isFallback =>
            {
                var lookup = aliasesByFallback[isFallback].Select(x => (x.AuthorId, x.Name, x.AllowAutoMatch))
                    .ToLookup(x => x.Name, StringComparer.OrdinalIgnoreCase);
                var pattern = string.Join("|", lookup.Select(x => x.Key).OrderByDescending(x => x.Length)
                    .ThenBy(x => x, StringComparer.Ordinal).Select(Regex.Escape));
                var matcher = pattern.Length == 0 ? null : new Regex(@"(?<![\p{L}\p{N}])(?:" + pattern + @")(?![\p{L}\p{N}])",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
                var joinedMatcher = isFallback || pattern.Length == 0 ? null : new Regex(
                    @"^(?<author>" + pattern + @")(?:(?:[\s_\-:&+×#/,、x]*)(?<author>" + pattern + @"))+$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
                var leadingJoinedMatcher = joinedMatcher == null ? null : new Regex(
                    joinedMatcher.ToString().TrimEnd('$') + @"(?=$|[\s_:\-])",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
                return (Lookup: lookup, Pattern: pattern, Matcher: matcher, JoinedMatcher: joinedMatcher, LeadingJoinedMatcher: leadingJoinedMatcher);
            }).ToArray();
            var fallbackPattern = string.Join("|", matchers[1].Lookup
                .Where(x => x.Any(a => a.AllowAutoMatch) && x.Select(a => a.AuthorId).Distinct().Count() == 1
                    && !matchers[0].Lookup.Contains(x.Key))
                .Select(x => x.Key).OrderByDescending(x => x.Length).ThenBy(x => x, StringComparer.Ordinal).Select(Regex.Escape));
            var fallbackPrefix = fallbackPattern.Length == 0 ? null : new Regex(
                @"^(?:\[(?:Fanbox|Fantia|Patreon|Pixiv|Twitter|" + fallbackPattern + @")\]\s*)+",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var repeatedFallbackPrefix = fallbackPattern.Length == 0 ? null : new Regex(
                @"^(?:(?:" + fallbackPattern + @")(?=[\s_:\-]|$)[\s_:\-]*|\[(?:Fanbox|Fantia|Patreon|Pixiv|Twitter|"
                    + fallbackPattern + @")\][\s_:\-]*)+",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var datedAuthorMatcher = matchers[0].Pattern.Length == 0 ? null : new Regex(
                @"^(?<work>[0-9]+(?:-[0-9]+)*-{3,}.+?)-(?<author>" + matchers[0].Pattern
                    + @")(?<date>-(?:0?[1-9]|1[0-2])-(?:0?[1-9]|[12][0-9]|3[01]))$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var brandedAuthorMatcher = fallbackPattern.Length == 0 || matchers[0].Pattern.Length == 0 ? null : new Regex(
                @"^(?:" + fallbackPattern + @")(?<author>" + matchers[0].Pattern + @")-{3,}(?<work>.+)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var cache = new Dictionary<string, AuthorMatchResult>(StringComparer.Ordinal);
            return originalName =>
            {
                var name = NormalizeName(originalName);
                if (cache.TryGetValue(name, out var cached))
                    return cached with { OriginalName = originalName };

                // 编号和明确的来源标签不作为作品名或作者名。
                var text = Regex.Replace(name, @"^\d+_", "");
                text = Regex.Replace(text, @"^(?:\[(?:Fanbox|Fantia|Patreon|Pixiv|Twitter)\]\s*)+", "",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                // 品牌前缀不挡住普通作者；普通作者待确认时不使用兜底覆盖。
                var authorText = fallbackPrefix?.Replace(text, "") ?? text;
                var item = MatchAliases(originalName, authorText, matchers[0].Lookup, matchers[0].Matcher);
                if (item.Status != AuthorMatchStatus.Matched && repeatedFallbackPrefix != null)
                {
                    // 仅补救未匹配成功的名称；完整品牌可不带括号并重复出现。
                    var withoutBrands = repeatedFallbackPrefix.Replace(authorText, "");
                    if (withoutBrands != authorText)
                    {
                        authorText = withoutBrands;
                        item = MatchAliases(originalName, authorText, matchers[0].Lookup, matchers[0].Matcher);
                    }
                }
                if (item.Status != AuthorMatchStatus.Matched)
                {
                    var header = Regex.Match(authorText, @"^\[([^\]]+)\]");
                    if (header.Success && !matchers[0].Lookup.Contains(header.Groups[1].Value))
                    {
                        // 仅在整个括号能拆成多个已知别名时分开，避免把作者名的一部分误当成另一个人。
                        var joined = matchers[0].JoinedMatcher?.Match(header.Groups[1].Value);
                        if (joined != null && joined.Success)
                        {
                            var separated = "[" + string.Join(" ", joined.Groups["author"].Captures.Select(x => x.Value)) + "]"
                                + authorText.Substring(header.Length);
                            item = MatchAliases(originalName, separated, matchers[0].Lookup, matchers[0].Matcher);
                        }
                    }
                    else if (!header.Success)
                    {
                        // 无括号首字段也须完整拆成至少两个已知别名，不能截断未登记名称或作品正文。
                        var joined = matchers[0].LeadingJoinedMatcher?.Match(authorText);
                        if (joined != null && joined.Success)
                        {
                            var separated = "[" + string.Join(" ", joined.Groups["author"].Captures.Select(x => x.Value)) + "]"
                                + authorText.Substring(joined.Length);
                            item = MatchAliases(originalName, separated, matchers[0].Lookup, matchers[0].Matcher);
                        }
                    }
                }
                if (item.Status != AuthorMatchStatus.Matched && authorText != text)
                {
                    // 品牌后的卷号和字母数字编号可位于作者之前；匹配后仍保留在作品名中。
                    var prefixLength = 0;
                    while (prefixLength < authorText.Length)
                    {
                        var prefix = Regex.Match(authorText.Substring(prefixLength), @"^(?:(?:vol|no)\.?\s*\d+|[a-z]+\d+)[\s_:\-]+",
                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                        if (!prefix.Success)
                            break;
                        prefixLength += prefix.Length;
                        var prefixMatch = MatchAliases(originalName, authorText.Substring(prefixLength),
                            matchers[0].Lookup, matchers[0].Matcher);
                        if (prefixMatch.Status == AuthorMatchStatus.Matched)
                        {
                            item = prefixMatch with { WorkName = (authorText.Substring(0, prefixLength) + prefixMatch.WorkName).TrimEnd(' ', '-', '_', ':') };
                            break;
                        }
                    }
                }
                if (item.Status != AuthorMatchStatus.Matched)
                {
                    // 编号---作品-作者-日期，或品牌作者---作品：只补救完整格式，保留作品字段中的其它名字。
                    foreach (var matcher in new[] { datedAuthorMatcher, brandedAuthorMatcher })
                    {
                        var match = matcher?.Match(authorText);
                        if (match == null || !match.Success)
                            continue;
                        var hits = matchers[0].Lookup[match.Groups["author"].Value].ToList();
                        item = item with { Status = AuthorMatchStatus.NeedsReview };
                        if (hits.Select(x => x.AuthorId).Distinct().Count() != 1 || !hits.Any(x => x.AllowAutoMatch))
                            continue;
                        item = item with { Status = AuthorMatchStatus.Matched, AuthorId = hits[0].AuthorId,
                            WorkName = (match.Groups["work"].Value + match.Groups["date"].Value).Trim(' ', '-', '_', ':') };
                        break;
                    }
                }
                if (item.Status == AuthorMatchStatus.Unmatched)
                    item = MatchAliases(originalName, text, matchers[1].Lookup, matchers[1].Matcher);
                cache.Add(name, item);
                return item;
            };
        }

        private static AuthorMatchResult MatchAliases(string originalName, string text,
            ILookup<string, (long AuthorId, string Name, bool AllowAutoMatch)> aliasLookup, Regex matcher)
        {
            var status = AuthorMatchStatus.Unmatched;
            long? authorId = null;
            var workName = "";
            var match = matcher?.Match(text);
            if (match != null && match.Success)
            {
                status = AuthorMatchStatus.NeedsReview;
                var header = Regex.Match(text, @"^\[([^\]]+)\]");
                // 括号内可包含多位作者，选最靠左且允许自动匹配的别名。
                if (header.Success)
                    while (match.Success && match.Index < header.Length
                        && !aliasLookup[match.Value].Any(x => x.AllowAutoMatch))
                        match = match.NextMatch();
                if (match.Success)
                {
                    var hits = aliasLookup[match.Value].ToList();
                    var inHeader = header.Success && match.Index >= header.Groups[1].Index
                        && match.Index + match.Length <= header.Groups[1].Index + header.Groups[1].Length;
                    var prefix = text.Substring(0, match.Index);
                    var allowedPrefix = Regex.IsMatch(prefix,
                        @"^(?:(?:cosplay|cos|artist|western pixiv artist)[\s_:\-]+|[\s_\-])*$",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (hits.Select(x => x.AuthorId).Distinct().Count() == 1 && hits.Any(x => x.AllowAutoMatch)
                        && (inHeader || !header.Success && allowedPrefix))
                    {
                        authorId = hits[0].AuthorId;
                        status = AuthorMatchStatus.Matched;
                        var remainder = text.Substring(inHeader ? header.Length : match.Index + match.Length);
                        workName = remainder.Trim(' ', '-', '_', ':');
                        if (inHeader)
                        {
                            var remainingNames = header.Groups[1].Value
                                .Remove(match.Index - header.Groups[1].Index, match.Length)
                                .Trim(' ', '-', '_', ':', '&', '+', '×', '#', '/', ',', '、');
                            remainingNames = Regex.Replace(remainingNames, @"^x\s+|\s+x$", "",
                                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                            workName = (remainingNames + " " + workName).Trim();
                        }
                        // 作者_作者 - 作品：只去掉紧接着重复的同一作者，不删除作品正文中的名字。
                        else if (remainder.TrimStart().StartsWith('_') && workName.Length > 0)
                        {
                            var repeated = matcher.Match(workName);
                            if (repeated.Success && repeated.Index == 0
                                && aliasLookup[repeated.Value].All(x => x.AuthorId == authorId))
                                workName = workName.Substring(repeated.Length).Trim(' ', '-', '_', ':');
                        }
                        else if (!inHeader)
                        {
                            var partner = matcher.Match(workName);
                            if (partner.Success && Regex.IsMatch(workName.Substring(0, partner.Index), @"^(?:[&+×]\s*|x\s+)$",
                                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                                workName = workName.Substring(partner.Index);
                        }
                        var description = workName.IndexOf("__", StringComparison.Ordinal);
                        if (description >= 0)
                            workName = workName.Substring(0, description).Trim();
                    }
                }
                // 非括号格式还可通过任意位置的 #完整别名确定作者，不推断其余字段的位置。
                if (status != AuthorMatchStatus.Matched && !header.Success)
                    for (var tagged = match; tagged.Success; tagged = tagged.NextMatch())
                    {
                        if (tagged.Index == 0 || text[tagged.Index - 1] != '#')
                            continue;
                        var hits = aliasLookup[tagged.Value].ToList();
                        if (!hits.Any(x => x.AllowAutoMatch))
                            continue;
                        if (hits.Select(x => x.AuthorId).Distinct().Count() != 1)
                            break;
                        authorId = hits[0].AuthorId;
                        status = AuthorMatchStatus.Matched;
                        var before = text.Substring(0, tagged.Index - 1).TrimEnd(' ', '-', '_', ':');
                        var after = text.Substring(tagged.Index + tagged.Length).TrimStart(' ', '-', '_', ':');
                        workName = (before + " " + after).Trim();
                        workName = Regex.Replace(workName, @"^(?:cosplay|cos)(?:[\s_:\-]+|$)", "",
                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                        break;
                    }
            }
            // 三个及以上横线或下划线作为字段边界；整段匹配作者，不依赖字段顺序。
            foreach (var separator in new[] { "---", "_" })
            {
                if (status == AuthorMatchStatus.Matched || !text.Contains(separator)
                    || separator == "_" && text.StartsWith('['))
                    continue;
                var parts = Regex.Split(text, separator == "---" ? @"-{3,}" : "_").Select(x => x.Trim()).ToArray();
                for (var i = 0; i < parts.Length; i++)
                {
                    var hits = aliasLookup[parts[i]].ToList();
                    if (hits.Count == 0)
                        continue;
                    status = AuthorMatchStatus.NeedsReview;
                    if (!hits.Any(x => x.AllowAutoMatch))
                        continue;
                    if (hits.Select(x => x.AuthorId).Distinct().Count() != 1)
                        break;
                    authorId = hits[0].AuthorId;
                    status = AuthorMatchStatus.Matched;
                    workName = string.Join(separator, parts.Where((x, index) => index != i && x.Length > 0));
                    if (separator == "_")
                        workName = Regex.Replace(workName, @"^(?:cosplay|cos)(?:[\s_:\-]+|$)", "",
                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    break;
                }
            }
            return new AuthorMatchResult(originalName, status, authorId, workName);
        }
    }
}
