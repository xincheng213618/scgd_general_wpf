using System;
using System.Linq;
using System.Reflection;

namespace ColorVision.Extension
{
    public static class EnumExtensions
    {
        public static string ToDescription(this Enum This) => This?.GetType()?.GetRuntimeField(This.ToString())?.GetCustomAttributes<System.ComponentModel.DescriptionAttribute>().FirstOrDefault()?.Description ?? string.Empty;
    }
}
