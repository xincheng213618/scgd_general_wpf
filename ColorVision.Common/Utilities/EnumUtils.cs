using System;
using System.Linq;
using System.Reflection;

namespace ColorVision.Common.Utilities
{
    public static class EnumUtils
    {
        public static string ToDescription(this Enum This) => This?.GetType()?.GetRuntimeField(This.ToString())?.GetCustomAttributes<System.ComponentModel.DescriptionAttribute>().FirstOrDefault()?.Description ?? string.Empty;

    }
}
