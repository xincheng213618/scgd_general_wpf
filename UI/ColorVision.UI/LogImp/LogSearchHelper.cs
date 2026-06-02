using System.Text.RegularExpressions;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// 日志搜索过滤工具类，支持关键词和正则表达式两种搜索模式
    /// </summary>
    public static class LogSearchHelper
    {
        private static readonly char[] RegexSpecialChars =
            { '.', '*', '+', '?', '^', '$', '(', ')', '[', ']', '{', '}', '|', '\\' };
        private static readonly string[] NewLineSeparators = { Environment.NewLine };
        private static readonly object RegexCacheLock = new();
        private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromMilliseconds(250);
        private static string? _cachedRegexPattern;
        private static Regex? _cachedRegex;
        private static string? _cachedInvalidRegexPattern;

        /// <summary>
        /// 对日志行执行搜索过滤，支持正则表达式和多关键词两种模式
        /// </summary>
        /// <param name="searchText">搜索文本；为空时直接返回 true 且 filteredLines 为空数组</param>
        /// <param name="logLines">原始日志行序列</param>
        /// <param name="filteredLines">过滤后的日志行；正则不可用时为空数组</param>
        /// <returns>操作是否成功（正则不可用时返回 false，调用方应显示错误提示）</returns>
        public static bool FilterLines(string searchText, IEnumerable<string> logLines, out string[] filteredLines)
        {
            filteredLines = Array.Empty<string>();
            if (!FilterItems(searchText, logLines, line => line, out var matches))
            {
                return false;
            }

            filteredLines = matches.ToArray();
            return true;
        }

        public static bool FilterItems<T>(
            string searchText,
            IEnumerable<T> items,
            Func<T, string> textSelector,
            out List<T> filteredItems)
        {
            ArgumentNullException.ThrowIfNull(items);
            ArgumentNullException.ThrowIfNull(textSelector);

            filteredItems = new List<T>();
            if (string.IsNullOrEmpty(searchText)) return true;

            if (!TryCreateMatcher(searchText, out var matcher))
            {
                return false;
            }

            try
            {
                foreach (var item in items)
                {
                    if (matcher(textSelector(item)))
                    {
                        filteredItems.Add(item);
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }

            return true;
        }

        public static bool FilterText(string searchText, string logText, out string filteredText)
        {
            var logLines = logText.Split(NewLineSeparators, StringSplitOptions.None);
            return FilterText(searchText, logLines, out filteredText);
        }

        public static bool FilterText(string searchText, IEnumerable<string> logLines, out string filteredText)
        {
            filteredText = string.Empty;
            if (!FilterLines(searchText, logLines, out var filteredLines))
            {
                return false;
            }

            filteredText = string.Join(Environment.NewLine, filteredLines);
            return true;
        }

        private static bool ContainsAllKeywords(string line, string[] keywords)
        {
            for (var i = 0; i < keywords.Length; i++)
            {
                if (!line.Contains(keywords[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryCreateMatcher(string searchText, out Func<string, bool> matcher)
        {
            var containsRegex = searchText.IndexOfAny(RegexSpecialChars) >= 0;
            if (containsRegex)
            {
                if (!TryGetRegex(searchText, out var regex))
                {
                    matcher = null!;
                    return false;
                }

                matcher = regex.IsMatch;
                return true;
            }

            var keywords = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            matcher = line => ContainsAllKeywords(line, keywords);
            return true;
        }

        private static bool TryGetRegex(string pattern, out Regex regex)
        {
            lock (RegexCacheLock)
            {
                if (string.Equals(_cachedRegexPattern, pattern, StringComparison.Ordinal) && _cachedRegex != null)
                {
                    regex = _cachedRegex;
                    return true;
                }

                if (string.Equals(_cachedInvalidRegexPattern, pattern, StringComparison.Ordinal))
                {
                    regex = null!;
                    return false;
                }
            }

            try
            {
                var createdRegex = new Regex(pattern, RegexOptions.IgnoreCase, RegexMatchTimeout);
                lock (RegexCacheLock)
                {
                    _cachedRegexPattern = pattern;
                    _cachedRegex = createdRegex;
                    _cachedInvalidRegexPattern = null;
                }

                regex = createdRegex;
                return true;
            }
            catch (RegexParseException)
            {
                lock (RegexCacheLock)
                {
                    _cachedInvalidRegexPattern = pattern;
                    if (string.Equals(_cachedRegexPattern, pattern, StringComparison.Ordinal))
                    {
                        _cachedRegexPattern = null;
                        _cachedRegex = null;
                    }
                }

                regex = null!;
                return false;
            }
        }
    }
}
