using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColorVision.Common.Extension
{
    public static class StringExtensions
    {
        public static bool IsNullOrEmpty([NotNullWhen(false)] this string? input)
        {
            return string.IsNullOrEmpty(input);
        }

        public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? input)
        {
            return string.IsNullOrWhiteSpace(input);
        }

        public static string RemoveEmptyLines(this string input)
        {
            // 正则表达式匹配空白行
            string pattern = @"^\s*$\n|\r";
            string replacement = "";
            string result = Regex.Replace(input, pattern, replacement, RegexOptions.Multiline);

            return result;
        }

        public static bool BeginWithAny(this string s, IEnumerable<char> chars)
        {
            if (s.IsNullOrEmpty()) return false;
            return chars.Contains(s[0]);
        }

        public static bool IsWhiteSpace(this string input)
        {
            foreach (char c in input)
            {
                if (char.IsWhiteSpace(c)) continue;

                return false;
            }
            return true;
        }

        public static IEnumerable<string> NonWhiteSpaceLines(this TextReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.IsWhiteSpace()) continue;
                yield return line;
            }
        }

        public static string RemovePrefix(this string input, char prefix)
        {
            if (input.StartsWith(prefix))
            {
                return input.Substring(1);
            }
            else
            {
                return input;
            }
        }

        public static string UpperFirstChar(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            return char.ToUpper(input[0],System.Globalization.CultureInfo.CurrentCulture) + input.Substring(1);
        }

    }
}
