using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace ColorVision.Solution
{
    /// <summary>
    /// Parses ANSI/VT100 escape sequences and converts them to WPF formatted text
    /// Supports colors (16-color, 256-color, RGB), text formatting (bold, italic, underline)
    /// </summary>
    public class AnsiEscapeSequenceParser
    {
        // ANSI color definitions (standard 16 colors)
        private static readonly Dictionary<int, Color> AnsiColorMap = new()
        {
            // Normal colors (30-37, 40-47)
            { 0, Color.FromRgb(0, 0, 0) },         // Black
            { 1, Color.FromRgb(205, 49, 49) },     // Red
            { 2, Color.FromRgb(13, 188, 121) },    // Green
            { 3, Color.FromRgb(229, 229, 16) },    // Yellow
            { 4, Color.FromRgb(36, 114, 200) },    // Blue
            { 5, Color.FromRgb(188, 63, 188) },    // Magenta
            { 6, Color.FromRgb(17, 168, 205) },    // Cyan
            { 7, Color.FromRgb(229, 229, 229) },   // White
            
            // Bright colors (90-97, 100-107)
            { 8, Color.FromRgb(102, 102, 102) },   // Bright Black (Gray)
            { 9, Color.FromRgb(241, 76, 76) },     // Bright Red
            { 10, Color.FromRgb(35, 209, 139) },   // Bright Green
            { 11, Color.FromRgb(245, 245, 67) },   // Bright Yellow
            { 12, Color.FromRgb(59, 142, 234) },   // Bright Blue
            { 13, Color.FromRgb(214, 112, 214) },  // Bright Magenta
            { 14, Color.FromRgb(41, 184, 219) },   // Bright Cyan
            { 15, Color.FromRgb(255, 255, 255) }   // Bright White
        };

        // Regex to match ANSI escape sequences
        private static readonly Regex AnsiEscapeRegex = new(@"\x1b\[([0-9;]*)m", RegexOptions.Compiled);

        /// <summary>
        /// Current text formatting state
        /// </summary>
        private class TextFormat
        {
            public Color? Foreground { get; set; }
            public Color? Background { get; set; }
            public bool Bold { get; set; }
            public bool Italic { get; set; }
            public bool Underline { get; set; }

            public void Reset()
            {
                Foreground = null;
                Background = null;
                Bold = false;
                Italic = false;
                Underline = false;
            }

            public TextFormat Clone()
            {
                return new TextFormat
                {
                    Foreground = Foreground,
                    Background = Background,
                    Bold = Bold,
                    Italic = Italic,
                    Underline = Underline
                };
            }
        }

        /// <summary>
        /// Parses ANSI escape sequences and returns a list of formatted text segments
        /// </summary>
        public static List<Inline> Parse(string text, Color defaultForeground, Color defaultBackground)
        {
            var result = new List<Inline>();
            if (string.IsNullOrEmpty(text))
                return result;

            var format = new TextFormat();
            int lastIndex = 0;

            // Find all ANSI escape sequences
            var matches = AnsiEscapeRegex.Matches(text);

            foreach (Match match in matches)
            {
                // Add text before this escape sequence
                if (match.Index > lastIndex)
                {
                    string textSegment = text.Substring(lastIndex, match.Index - lastIndex);
                    result.Add(CreateRun(textSegment, format, defaultForeground, defaultBackground));
                }

                // Parse and apply the escape sequence
                string codes = match.Groups[1].Value;
                ParseEscapeSequence(codes, format);

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text after last escape sequence
            if (lastIndex < text.Length)
            {
                string textSegment = text.Substring(lastIndex);
                result.Add(CreateRun(textSegment, format, defaultForeground, defaultBackground));
            }

            return result;
        }

        /// <summary>
        /// Creates a Run with the specified format
        /// </summary>
        private static Run CreateRun(string text, TextFormat format, Color defaultForeground, Color defaultBackground)
        {
            if (string.IsNullOrEmpty(text))
                return new Run();

            var run = new Run(text)
            {
                Foreground = new SolidColorBrush(format.Foreground ?? defaultForeground)
            };

            if (format.Background.HasValue)
            {
                run.Background = new SolidColorBrush(format.Background.Value);
            }

            if (format.Bold)
            {
                run.FontWeight = FontWeights.Bold;
            }

            if (format.Italic)
            {
                run.FontStyle = FontStyles.Italic;
            }

            if (format.Underline)
            {
                run.TextDecorations = TextDecorations.Underline;
            }

            return run;
        }

        /// <summary>
        /// Parses ANSI SGR (Select Graphic Rendition) codes
        /// Format: ESC[{code};{code}m
        /// </summary>
        private static void ParseEscapeSequence(string codes, TextFormat format)
        {
            if (string.IsNullOrEmpty(codes))
            {
                format.Reset();
                return;
            }

            var parts = codes.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out int code))
                    continue;

                switch (code)
                {
                    case 0: // Reset
                        format.Reset();
                        break;

                    case 1: // Bold
                        format.Bold = true;
                        break;

                    case 3: // Italic
                        format.Italic = true;
                        break;

                    case 4: // Underline
                        format.Underline = true;
                        break;

                    case 22: // Normal intensity (not bold)
                        format.Bold = false;
                        break;

                    case 23: // Not italic
                        format.Italic = false;
                        break;

                    case 24: // Not underlined
                        format.Underline = false;
                        break;

                    // Foreground colors (30-37)
                    case 30:
                    case 31:
                    case 32:
                    case 33:
                    case 34:
                    case 35:
                    case 36:
                    case 37:
                        format.Foreground = AnsiColorMap[code - 30];
                        break;

                    // Background colors (40-47)
                    case 40:
                    case 41:
                    case 42:
                    case 43:
                    case 44:
                    case 45:
                    case 46:
                    case 47:
                        format.Background = AnsiColorMap[code - 40];
                        break;

                    // Bright foreground colors (90-97)
                    case 90:
                    case 91:
                    case 92:
                    case 93:
                    case 94:
                    case 95:
                    case 96:
                    case 97:
                        format.Foreground = AnsiColorMap[code - 90 + 8];
                        break;

                    // Bright background colors (100-107)
                    case 100:
                    case 101:
                    case 102:
                    case 103:
                    case 104:
                    case 105:
                    case 106:
                    case 107:
                        format.Background = AnsiColorMap[code - 100 + 8];
                        break;

                    case 38: // Extended foreground color
                        i = ParseExtendedColor(parts, i, format, true);
                        break;

                    case 48: // Extended background color
                        i = ParseExtendedColor(parts, i, format, false);
                        break;

                    case 39: // Default foreground
                        format.Foreground = null;
                        break;

                    case 49: // Default background
                        format.Background = null;
                        break;
                }
            }
        }

        /// <summary>
        /// Parses extended color codes (256-color and RGB)
        /// Format: 38;5;{n} for 256-color foreground
        ///         38;2;{r};{g};{b} for RGB foreground
        /// </summary>
        private static int ParseExtendedColor(string[] parts, int index, TextFormat format, bool isForeground)
        {
            if (index + 1 >= parts.Length)
                return index;

            if (!int.TryParse(parts[index + 1], out int colorType))
                return index;

            if (colorType == 5 && index + 2 < parts.Length) // 256-color
            {
                if (int.TryParse(parts[index + 2], out int colorIndex))
                {
                    Color color = Get256Color(colorIndex);
                    if (isForeground)
                        format.Foreground = color;
                    else
                        format.Background = color;
                }
                return index + 2;
            }
            else if (colorType == 2 && index + 4 < parts.Length) // RGB
            {
                if (int.TryParse(parts[index + 2], out int r) &&
                    int.TryParse(parts[index + 3], out int g) &&
                    int.TryParse(parts[index + 4], out int b))
                {
                    Color color = Color.FromRgb((byte)r, (byte)g, (byte)b);
                    if (isForeground)
                        format.Foreground = color;
                    else
                        format.Background = color;
                }
                return index + 4;
            }

            return index;
        }

        /// <summary>
        /// Converts 256-color index to RGB color
        /// </summary>
        private static Color Get256Color(int index)
        {
            // 0-15: Standard colors (same as 16-color palette)
            if (index < 16)
            {
                return AnsiColorMap[index];
            }
            // 16-231: 6x6x6 color cube
            else if (index >= 16 && index <= 231)
            {
                int colorIndex = index - 16;
                int r = (colorIndex / 36) * 51;
                int g = ((colorIndex % 36) / 6) * 51;
                int b = (colorIndex % 6) * 51;
                return Color.FromRgb((byte)r, (byte)g, (byte)b);
            }
            // 232-255: Grayscale
            else if (index >= 232 && index <= 255)
            {
                int gray = 8 + (index - 232) * 10;
                return Color.FromRgb((byte)gray, (byte)gray, (byte)gray);
            }

            // Default to white for invalid indices
            return Colors.White;
        }
    }
}
