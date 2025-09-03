using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace ColorVision.Common.Utilities
{
    public static class StringUtils
    {
        public const string CarriageReturnLineFeed = "\r\n";
        public const string Empty = "";
        public const char CarriageReturn = '\r';
        public const char LineFeed = '\n';
        public const char Tab = '\t';


        public static string RemoveEmptyLines(this string value)
        {
            // 正则表达式匹配空白行
            string pattern = @"^\s*$\n|\r";
            string replacement = "";
            string result = Regex.Replace(value, pattern, replacement, RegexOptions.Multiline);

            return result;
        }


        private static char ToLower(char c)
        {
            c = char.ToLower(c, CultureInfo.InvariantCulture);
            return c;
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

        public static string ToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0]))
            {
                return s;
            }

            char[] chars = s.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (i == 1 && !char.IsUpper(chars[i]))
                {
                    break;
                }

                bool hasNext = i + 1 < chars.Length;
                if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
                {
                    // if the next character is a space, which is not considered uppercase 
                    // (otherwise we wouldn't be here...)
                    // we want to ensure that the following:
                    // 'FOO bar' is rewritten as 'foo bar', and not as 'foO bar'
                    // The code was written in such a way that the first word in uppercase
                    // ends when if finds an uppercase letter followed by a lowercase letter.
                    // now a ' ' (space, (char)32) is considered not upper
                    // but in that case we still want our current character to become lowercase
                    if (char.IsSeparator(chars[i + 1]))
                    {
                        chars[i] = ToLower(chars[i]);
                    }

                    break;
                }

                chars[i] = ToLower(chars[i]);
            }

            return new string(chars);
        }

        public static bool StartsWith(this string source, char value)
        {
            return source.Length > 0 && source[0] == value;
        }

        public static bool EndsWith(this string source, char value)
        {
            return source.Length > 0 && source[source.Length - 1] == value;
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
            return char.ToUpper(input[0], CultureInfo.CurrentCulture) + input.Substring(1);
        }

    }
}
