using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    internal static class TextRenderCore
    {
        private const double MinimumFontSize = 1;

        public static double NormalizeFontSize(double fontSize)
        {
            return double.IsFinite(fontSize) && fontSize > 0 ? fontSize : MinimumFontSize;
        }

        public static double NormalizePixelsPerDip(double pixelsPerDip)
        {
            return double.IsFinite(pixelsPerDip) && pixelsPerDip > 0 ? pixelsPerDip : 1;
        }

        public static FormattedText CreateFormattedText(TextAttribute attribute, string? text, double fontSize, double pixelsPerDip, bool measureEmptyText = false)
        {
            ArgumentNullException.ThrowIfNull(attribute);

            string value = text ?? string.Empty;
            if (measureEmptyText && value.Length == 0)
            {
                value = " ";
            }
            else if (EndsWithLineBreak(value))
            {
                // FormattedText omits the empty line after a trailing newline. A zero-width
                // character keeps its height aligned with the WPF TextBox and caret.
                value += "\u200B";
            }

            return new FormattedText(
                value,
                CultureInfo.CurrentCulture,
                attribute.FlowDirection,
                new Typeface(attribute.FontFamily, attribute.FontStyle, attribute.FontWeight, attribute.FontStretch),
                NormalizeFontSize(fontSize),
                attribute.Brush,
                NormalizePixelsPerDip(pixelsPerDip));
        }

        private static bool EndsWithLineBreak(string value)
        {
            return value.Length > 0 && value[^1] is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029';
        }

        public static Rect Measure(TextAttribute attribute, string? text, Point position, double fontSize, double pixelsPerDip, bool measureEmptyText = false)
        {
            FormattedText formattedText = CreateFormattedText(attribute, text, fontSize, pixelsPerDip, measureEmptyText);
            return GetBounds(formattedText, position);
        }

        public static Rect GetBounds(FormattedText formattedText, Point position)
        {
            ArgumentNullException.ThrowIfNull(formattedText);

            return new Rect(position, new Size(
                Math.Max(formattedText.WidthIncludingTrailingWhitespace, 0),
                Math.Max(formattedText.Height, 0)));
        }
    }
}
