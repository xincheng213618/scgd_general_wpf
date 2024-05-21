using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ColorVision.Common.Utilities
{
    public static class FilterHelper
    {
        private readonly static char[] Chars = new char[] { ' ' };
        public static List<T> FilterByKeywords<T>(IEnumerable source, string keywords)
        {
            var keywordArray = keywords.Split(Chars, StringSplitOptions.RemoveEmptyEntries);

            return source.OfType<T>()
                .Where(item => keywordArray.All(keyword =>
                    typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Any(prop =>
                        {
                            var value = prop.GetValue(item)?.ToString();
                            return value != null && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                        })))
                .ToList();
        }

        public static List<T> FilterByKeywords<T>(IEnumerable<T> source, string keywords)
        {
            var keywordArray = keywords.Split(Chars, StringSplitOptions.RemoveEmptyEntries);

            return source
                .Where(item => keywordArray.All(keyword =>
                    typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Any(prop =>
                        {
                            var value = prop.GetValue(item)?.ToString();
                            return value != null && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
                        })))
                .ToList();
        }
    }
}
