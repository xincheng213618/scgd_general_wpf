using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Conoscope.Core
{
    internal static class CompositeFormatCache
    {
        private static readonly ConcurrentDictionary<string, CompositeFormat> cache = new(StringComparer.Ordinal);

        public static string Format(string format, params object?[] args)
        {
            return Format(CultureInfo.CurrentCulture, format, args);
        }

        public static string Format(IFormatProvider? provider, string format, params object?[] args)
        {
            ArgumentNullException.ThrowIfNull(format);
            return string.Format(provider, Get(format), args);
        }

        private static CompositeFormat Get(string format)
        {
            return cache.GetOrAdd(format, static value => CompositeFormat.Parse(value));
        }
    }
}