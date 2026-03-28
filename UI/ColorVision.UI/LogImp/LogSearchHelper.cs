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

        /// <summary>
        /// 对日志行执行搜索过滤，支持正则表达式和多关键词两种模式
        /// </summary>
        /// <param name="searchText">搜索文本；为空时直接返回 true 且 filteredLines 为空数组</param>
        /// <param name="logLines">原始日志行数组</param>
        /// <param name="filteredLines">过滤后的日志行；正则解析失败时为空数组</param>
        /// <returns>操作是否成功（正则解析失败时返回 false，调用方应显示错误提示）</returns>
        public static bool FilterLines(string searchText, string[] logLines, out string[] filteredLines)
        {
            filteredLines = Array.Empty<string>();
            if (string.IsNullOrEmpty(searchText)) return true;

            var containsRegex = RegexSpecialChars.Any(searchText.Contains);

            if (containsRegex)
            {
                try
                {
                    var regex = new Regex(searchText, RegexOptions.IgnoreCase);
                    filteredLines = logLines.Where(line => regex.IsMatch(line)).ToArray();
                }
                catch (RegexParseException)
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
    }
}
