using Conoscope.Core;

namespace Conoscope.Presentation.Formatters
{
    internal static class ConoscopeChannelDisplayFormatter
    {
        public static string GetLabel(ExportChannel channel)
        {
            return ConoscopeColorimetry.GetChannelLabel(channel);
        }

        public static string GetAxisLabel(ExportChannel channel)
        {
            string label = GetLabel(channel);
            return channel is ExportChannel.X or ExportChannel.Y or ExportChannel.Z
                ? $"{label} (cd/m2)"
                : label;
        }

        public static string FormatValue(double value, ExportChannel channel)
        {
            return ConoscopeColorimetry.FormatChannelValue(value, channel);
        }
    }
}