using System;

namespace ProjectStarkSemi.Conoscope
{
    public readonly record struct ConoscopeChromaticity(double x, double y, double u, double v, double Cct);

    public static class ConoscopeColorimetry
    {
        public static ConoscopeChromaticity Calculate(double X, double Y, double Z)
        {
            double x = 0;
            double y = 0;
            double u = 0;
            double v = 0;
            double cct = 0;

            double xyzSum = X + Y + Z;
            if (Math.Abs(xyzSum) > double.Epsilon)
            {
                x = X / xyzSum;
                y = Y / xyzSum;
            }

            double uvDenominator = X + 15 * Y + 3 * Z;
            if (Math.Abs(uvDenominator) > double.Epsilon)
            {
                u = 4 * X / uvDenominator;
                v = 9 * Y / uvDenominator;
            }

            if (Math.Abs(0.1858 - y) > double.Epsilon)
            {
                double n = (x - 0.3320) / (0.1858 - y);
                cct = 449.0 * Math.Pow(n, 3) + 3525.0 * Math.Pow(n, 2) + 6823.3 * n + 5520.33;
            }

            if (!double.IsFinite(cct) || cct < 0)
            {
                cct = 0;
            }

            return new ConoscopeChromaticity(x, y, u, v, cct);
        }

        public static double GetChannelValue(double X, double Y, double Z, ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => X,
                ExportChannel.Y => Y,
                ExportChannel.Z => Z,
                ExportChannel.CieX => Calculate(X, Y, Z).x,
                ExportChannel.CieY => Calculate(X, Y, Z).y,
                ExportChannel.CieU => Calculate(X, Y, Z).u,
                ExportChannel.CieV => Calculate(X, Y, Z).v,
                _ => Y
            };
        }

        public static string GetChannelLabel(ExportChannel channel)
        {
            return channel switch
            {
                ExportChannel.X => "X",
                ExportChannel.Y => "Y",
                ExportChannel.Z => "Z",
                ExportChannel.CieX => "x",
                ExportChannel.CieY => "y",
                ExportChannel.CieU => "u",
                ExportChannel.CieV => "v",
                _ => "Y"
            };
        }

        public static string FormatChannelValue(double value, ExportChannel channel)
        {
            return channel is ExportChannel.CieX or ExportChannel.CieY or ExportChannel.CieU or ExportChannel.CieV
                ? value.ToString("F6")
                : value.ToString("F2");
        }

        public static string FormatCct(double cct)
        {
            return cct > 0 ? $"{cct:F0}K" : "--";
        }
    }
}