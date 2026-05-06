using System.Collections.Generic;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Cie
{
    public sealed class CieGamut
    {
        public CieGamut(
            string name,
            IReadOnlyList<CieChromaticity> vertices,
            Brush stroke,
            Brush? fill = null,
            CieChromaticity? whitePoint = null,
            string? description = null)
        {
            Name = name;
            Vertices = vertices;
            Stroke = stroke;
            Fill = fill;
            WhitePoint = whitePoint;
            Description = description;
        }

        public string Name { get; }

        public IReadOnlyList<CieChromaticity> Vertices { get; }

        public Brush Stroke { get; }

        public Brush? Fill { get; }

        public CieChromaticity? WhitePoint { get; }

        public string? Description { get; }
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
            CreateBrush(36, 235, 64, 52),
            new CieChromaticity(0.31271, 0.32902),
            "IEC 61966-2-1 primaries, D65 white.");

        public static readonly CieGamut Rec709 = new(
            "BT.709",
            new[]
            {
                new CieChromaticity(0.6400, 0.3300),
                new CieChromaticity(0.3000, 0.6000),
                new CieChromaticity(0.1500, 0.0600)
            },
            CreateBrush(230, 28, 120, 210),
            CreateBrush(24, 28, 120, 210),
            new CieChromaticity(0.31271, 0.32902),
            "HDTV primaries match sRGB; transfer and usage are different.");

        public static readonly CieGamut AdobeRgb = new(
            "Adobe RGB",
            new[]
            {
                new CieChromaticity(0.6400, 0.3300),
                new CieChromaticity(0.2100, 0.7100),
                new CieChromaticity(0.1500, 0.0600)
            },
            CreateBrush(230, 126, 87, 194),
            CreateBrush(28, 126, 87, 194),
            new CieChromaticity(0.31271, 0.32902));

        public static readonly CieGamut Ntsc1953 = new(
            "NTSC 1953",
            new[]
            {
                new CieChromaticity(0.6700, 0.3300),
                new CieChromaticity(0.2100, 0.7100),
                new CieChromaticity(0.1400, 0.0800)
            },
            CreateBrush(230, 255, 149, 0),
            CreateBrush(26, 255, 149, 0),
            new CieChromaticity(0.31006, 0.31616));

        public static readonly CieGamut DciP3 = new(
            "DCI-P3",
            new[]
            {
                new CieChromaticity(0.6800, 0.3200),
                new CieChromaticity(0.2650, 0.6900),
                new CieChromaticity(0.1500, 0.0600)
            },
            CreateBrush(230, 42, 130, 218),
            CreateBrush(32, 42, 130, 218),
            new CieChromaticity(0.3140, 0.3510),
            "Cinema P3 primaries with cinema white.");

        public static readonly CieGamut DisplayP3 = new(
            "Display P3",
            new[]
            {
                new CieChromaticity(0.6800, 0.3200),
                new CieChromaticity(0.2650, 0.6900),
                new CieChromaticity(0.1500, 0.0600)
            },
            CreateBrush(230, 0, 145, 175),
            CreateBrush(28, 0, 145, 175),
            new CieChromaticity(0.31271, 0.32902),
            "P3 primaries with D65 white.");

        public static readonly CieGamut EbuPal = new(
            "EBU/PAL",
            new[]
            {
                new CieChromaticity(0.6400, 0.3300),
                new CieChromaticity(0.2900, 0.6000),
                new CieChromaticity(0.1500, 0.0600)
            },
            CreateBrush(230, 46, 160, 67),
            CreateBrush(26, 46, 160, 67),
            new CieChromaticity(0.31271, 0.32902));

        public static readonly CieGamut Pal = EbuPal;

        public static readonly CieGamut SmpteC = new(
            "SMPTE-C",
            new[]
            {
                new CieChromaticity(0.6300, 0.3400),
                new CieChromaticity(0.3100, 0.5950),
                new CieChromaticity(0.1550, 0.0700)
            },
            CreateBrush(230, 115, 145, 36),
            CreateBrush(24, 115, 145, 36),
            new CieChromaticity(0.31271, 0.32902));

        public static readonly CieGamut Rec2020 = new(
            "BT.2020",
            new[]
            {
                new CieChromaticity(0.7080, 0.2920),
                new CieChromaticity(0.1700, 0.7970),
                new CieChromaticity(0.1310, 0.0460)
            },
            CreateBrush(230, 51, 153, 102),
            CreateBrush(28, 51, 153, 102),
            new CieChromaticity(0.31271, 0.32902));

        public static readonly CieGamut ProPhotoRgb = new(
            "ProPhoto RGB",
            new[]
            {
                new CieChromaticity(0.7347, 0.2653),
                new CieChromaticity(0.1596, 0.8404),
                new CieChromaticity(0.0366, 0.0001)
            },
            CreateBrush(230, 180, 96, 32),
            CreateBrush(20, 180, 96, 32),
            new CieChromaticity(0.34567, 0.35850));

        public static readonly CieGamut AcesCg = new(
            "ACEScg",
            new[]
            {
                new CieChromaticity(0.7130, 0.2930),
                new CieChromaticity(0.1650, 0.8300),
                new CieChromaticity(0.1280, 0.0440)
            },
            CreateBrush(230, 115, 87, 22),
            CreateBrush(20, 115, 87, 22),
            new CieChromaticity(0.32168, 0.33767));

        public static IReadOnlyList<CieGamut> Defaults { get; } = new[]
        {
            SRgb,
            Rec709,
            AdobeRgb,
            DisplayP3,
            DciP3,
            Rec2020,
            Ntsc1953,
            EbuPal,
            SmpteC,
            ProPhotoRgb,
            AcesCg
        };

        private static SolidColorBrush CreateBrush(byte alpha, byte red, byte green, byte blue)
        {
            SolidColorBrush brush = new(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
