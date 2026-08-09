#pragma warning disable CS8602,CS8603
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace ColorVision.Common.Utilities
{
    internal static class EnumExtensions
    {
        public static string ToDescription(this Enum This)
        {
            var field = This?.GetType()?.GetRuntimeField(This.ToString());
            if (field == null) return This?.ToString() ?? string.Empty;

            var displayAttr = field.GetCustomAttributes<DisplayAttribute>().FirstOrDefault();
            if (displayAttr != null)
            {
                var resourceType = displayAttr.ResourceType;
                if (resourceType != null && displayAttr.Name != null)
                {
                    var prop = resourceType.GetProperty(displayAttr.Name, BindingFlags.Public | BindingFlags.Static);
                    if (prop != null)
                    {
                        return prop.GetValue(null)?.ToString() ?? displayAttr.Name;
                    }
                }
                return displayAttr.Name ?? This.ToString();
            }

            return field.GetCustomAttributes<System.ComponentModel.DescriptionAttribute>().FirstOrDefault()?.Description ?? string.Empty;
        }
    }
}
