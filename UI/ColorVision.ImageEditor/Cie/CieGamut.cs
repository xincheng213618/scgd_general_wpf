using System.Collections.Generic;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Cie
{
    public sealed class CieGamut
    {
        public CieGamut(string name, IReadOnlyList<CieChromaticity> vertices, Brush stroke, Brush? fill = null)
        {
            Name = name;
            Vertices = vertices;
            Stroke = stroke;
            Fill = fill;
        }

        public string Name { get; }

        public IReadOnlyList<CieChromaticity> Vertices { get; }

        public Brush Stroke { get; }

        public Brush? Fill { get; }
    }

    public sealed class CieMarker
    {
        public CieMarker(string name, CieChromaticity chromaticity, Color color)
        {
            Name = name;
            Chromaticity = chromaticity;
            Color = color;
        }

        public string Name { get; }

        public CieChromaticity Chromaticity { get; }

        public Color Color { get; }
    }

    public static class CieGamuts
    {
        public static readonly CieGamut SRgb = new(
            "sRGB",
            new[]
            {
                new CieChromaticity(0.6400, 0.3300),
                new CieChromaticity(0.3000, 0.6000),
                new CieChromaticity(0.1500, 0.0600)
            },
            CreateBrush(230, 235, 64, 52),
            CreateBrush(36, 235, 64, 52));

        public static readonly CieGamut AdobeRgb = new(
            "Adobe RGB",
            new[]
            {
                new CieChromaticity(0.6400, 0.3300),
                new CieChromaticity(0.2100, 0.7100),
                new CieChromaticity(0.1500, 0.0600)
            },
            CreateBrush(230, 126, 87, 194),
            CreateBrush(28, 126, 87, 194));

        public static readonly CieGamut Ntsc1953 = new(
            "NTSC 1953",
            new[]
            {
                new CieChromaticity(0.6700, 0.3300),
                new CieChromaticity(0.2100, 0.7100),
                new CieChromaticity(0.1400, 0.0800)
            },
            CreateBrush(230, 255, 149, 0),
            CreateBrush(26, 255, 149, 0));

        public static readonly CieGamut DciP3 = new(
            "DCI-P3",
            new[]
            {
                new CieChromaticity(0.6800, 0.3200),
                new CieChromaticity(0.2650, 0.6900),
                new CieChromaticity(0.1500, 0.0600)
            },
            CreateBrush(230, 42, 130, 218),
            CreateBrush(32, 42, 130, 218));

        public static readonly CieGamut Pal = new(
            "PAL",
            new[]
            {
                new CieChromaticity(0.6400, 0.3300),
                new CieChromaticity(0.2900, 0.6000),
                new CieChromaticity(0.1500, 0.0600)
            },
            CreateBrush(230, 46, 160, 67),
            CreateBrush(26, 46, 160, 67));

        public static readonly CieGamut Rec2020 = new(
            "BT.2020",
            new[]
            {
                new CieChromaticity(0.7080, 0.2920),
                new CieChromaticity(0.1700, 0.7970),
                new CieChromaticity(0.1310, 0.0460)
            },
            CreateBrush(230, 51, 153, 102),
            CreateBrush(28, 51, 153, 102));

        public static IReadOnlyList<CieGamut> Defaults { get; } = new[]
        {
            SRgb,
            AdobeRgb,
            Ntsc1953,
            DciP3,
            Pal,
            Rec2020
        };

        private static SolidColorBrush CreateBrush(byte alpha, byte red, byte green, byte blue)
        {
            SolidColorBrush brush = new(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
