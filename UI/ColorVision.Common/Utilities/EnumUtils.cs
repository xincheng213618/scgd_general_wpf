using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ColorVision.Common.Utilities
{
    public static class EnumExtensions
    {
        public static string ToDescription(this Enum This) => This?.GetType()?.GetRuntimeField(This.ToString())?.GetCustomAttributes<System.ComponentModel.DescriptionAttribute>().FirstOrDefault()?.Description ?? string.Empty;

        public static IEnumerable<KeyValuePair<TEnum, string>> ToKeyValuePairs<TEnum>() where TEnum : Enum
        {
            return Enum.GetValues(typeof(TEnum)).Cast<TEnum>().Select(e => new KeyValuePair<TEnum, string>(e, e.ToString()));
        }
    }
}
