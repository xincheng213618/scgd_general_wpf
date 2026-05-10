using System.Collections.Generic;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Cie
{
    public static class CieIlluminants
    {
        public static readonly CieMarker E = new("E", new CieChromaticity(1.0 / 3.0, 1.0 / 3.0), Color.FromRgb(68, 68, 68));

        public static readonly CieMarker D65 = new("D65", new CieChromaticity(0.31271, 0.32902), Color.FromRgb(25, 25, 25));

        public static readonly CieMarker D50 = new("D50", new CieChromaticity(0.34567, 0.35850), Color.FromRgb(31, 78, 121));

        public static readonly CieMarker D55 = new("D55", new CieChromaticity(0.33242, 0.34743), Color.FromRgb(60, 104, 150));

        public static readonly CieMarker D60 = new("D60", new CieChromaticity(0.32168, 0.33767), Color.FromRgb(42, 92, 145));

        public static readonly CieMarker A = new("A", new CieChromaticity(0.44757, 0.40745), Color.FromRgb(182, 91, 28));

        public static readonly CieMarker C = new("C", new CieChromaticity(0.31006, 0.31616), Color.FromRgb(48, 108, 152));

        public static readonly CieMarker D75 = new("D75", new CieChromaticity(0.29902, 0.31485), Color.FromRgb(0, 112, 150));

        public static IReadOnlyList<CieMarker> Defaults { get; } = new[]
        {
            E,
            D65,
            D50,
            D55,
            D60,
            A,
            C,
            D75
        };
    }
}
