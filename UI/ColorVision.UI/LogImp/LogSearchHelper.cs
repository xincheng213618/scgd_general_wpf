using System.Text.RegularExpressions;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// 日志搜索过滤工具类，支持关键词和正则表达式两种搜索模式
    /// </summary>
    public static class LogSearchHelper
    {
        private static readonly string[] RegexSpecialChars =
            { ".", "*", "+", "?", "^", "$", "(", ")", "[", "]", "{", "}", "|", "\\" };
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
            if (string.IsNullOrEmpty(searchText)) return true;

            var containsRegex = RegexSpecialChars.Any(searchText.Contains);

            if (containsRegex)
            {
                if (!TryGetRegex(searchText, out var regex))
                {
                    return false;
                }

                try
                {
                    filteredLines = logLines.Where(line => regex.IsMatch(line)).ToArray();
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            }
            else
            {
                // 以空格作为关键词分隔符（多个关键词之间为 AND 关系）
                var keywords = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                filteredLines = logLines
                    .Where(line => keywords.All(kw => line.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
            }
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
