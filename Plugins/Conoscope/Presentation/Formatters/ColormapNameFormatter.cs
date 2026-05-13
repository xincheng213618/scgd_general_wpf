using ColorVision.Core;
using System;

namespace Conoscope.Presentation.Formatters
{
    internal static class ColormapNameFormatter
    {
        public static string Format(ColormapTypes colormapType)
        {
            const string prefix = "COLORMAP_";
            string name = colormapType.ToString();
            return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? name[prefix.Length..]
                : name;
        }
    }
}