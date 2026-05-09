using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    internal static class TextStyleSerialization
    {
        private static readonly BrushConverter BrushConverter = new();
        private static readonly FontStyleConverter FontStyleConverter = new();
        private static readonly FontStretchConverter FontStretchConverter = new();

        public static string SerializeBrush(Brush brush)
        {
            return brush == null ? string.Empty : BrushConverter.ConvertToInvariantString(brush) ?? brush.ToString();
        }

        public static Brush DeserializeBrush(string value, Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            try
            {
                return BrushConverter.ConvertFromInvariantString(value) as Brush ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        public static string SerializeFontFamily(FontFamily fontFamily)
        {
            return fontFamily?.Source ?? string.Empty;
        }

        public static FontFamily DeserializeFontFamily(string value, FontFamily fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            try
            {
                return new FontFamily(value);
            }
            catch
            {
                return fallback;
            }
        }

        public static string SerializeFontStyle(FontStyle fontStyle)
        {
            return fontStyle.ToString();
        }

        public static FontStyle DeserializeFontStyle(string value, FontStyle fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            try
            {
                return (FontStyle)(FontStyleConverter.ConvertFromInvariantString(value) ?? fallback);
            }
            catch
            {
                return fallback;
            }
        }

        public static int SerializeFontWeight(FontWeight fontWeight)
        {
            return fontWeight.ToOpenTypeWeight();
        }

        public static FontWeight DeserializeFontWeight(int value, FontWeight fallback)
        {
            if (value < 1 || value > 999)
                return fallback;

            try
            {
                return FontWeight.FromOpenTypeWeight(value);
            }
            catch
            {
                return fallback;
            }
        }

        public static string SerializeFontStretch(FontStretch fontStretch)
        {
            return fontStretch.ToString();
        }

        public static FontStretch DeserializeFontStretch(string value, FontStretch fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            try
            {
                return (FontStretch)(FontStretchConverter.ConvertFromInvariantString(value) ?? fallback);
            }
            catch
            {
                return fallback;
            }
        }

        public static string SerializeFlowDirection(FlowDirection flowDirection)
        {
            return flowDirection.ToString();
        }

        public static FlowDirection DeserializeFlowDirection(string value, FlowDirection fallback)
        {
            return Enum.TryParse(value, true, out FlowDirection flowDirection) ? flowDirection : fallback;
        }
    }
}