using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Cie
{
    public readonly record struct CieGamutPrimary(double X, double Y)
    {
        public bool IsFinite =>
            !double.IsNaN(X) &&
            !double.IsNaN(Y) &&
            !double.IsInfinity(X) &&
            !double.IsInfinity(Y);

        public string Text => $"({X:F4}, {Y:F4})";

        public CieChromaticity ToChromaticity() => new(X, Y);
    }

    public sealed record CieColorGamutStandard(string Name, CieGamutPrimary Red, CieGamutPrimary Green, CieGamutPrimary Blue)
    {
        public override string ToString() => Name;
    }

    public sealed record CieColorGamutCalculationResult(
        CieGamutPrimary Red,
        CieGamutPrimary Green,
        CieGamutPrimary Blue,
        CieColorGamutStandard Standard,
        double SampleArea,
        double StandardArea,
        double CoveragePercent);

    public static class DefaultCieColorGamutCalculator
    {
        public static CieColorGamutCalculationResult Calculate(CieGamutPrimary red, CieGamutPrimary green, CieGamutPrimary blue, CieColorGamutStandard standard)
        {
            ArgumentNullException.ThrowIfNull(standard);

            double sampleArea = TriangleArea(red, green, blue);
            double standardArea = TriangleArea(standard.Red, standard.Green, standard.Blue);
            if (standardArea <= 0)
            {
                throw new InvalidOperationException($"标准色域 {standard.Name} 的三角形面积无效。");
            }

            return new CieColorGamutCalculationResult(red, green, blue, standard, sampleArea, standardArea, sampleArea / standardArea * 100.0);
        }

        public static double TriangleArea(CieGamutPrimary red, CieGamutPrimary green, CieGamutPrimary blue)
        {
            return Math.Abs((red.X * (green.Y - blue.Y) + green.X * (blue.Y - red.Y) + blue.X * (red.Y - green.Y)) / 2.0);
        }
    }

    public static class CieColorGamutStandards
    {
        public static IReadOnlyList<CieColorGamutStandard> All { get; } = CieGamuts.Defaults
            .Select(FromGamut)
            .ToArray();

        public static CieColorGamutStandard FromGamut(CieGamut gamut)
        {
            ArgumentNullException.ThrowIfNull(gamut);

            if (gamut.Vertices.Count < 3)
            {
                throw new InvalidOperationException($"色域 {gamut.Name} 至少需要 3 个顶点。");
            }

            return new CieColorGamutStandard(
                gamut.Name,
                ToPrimary(gamut.Vertices[0]),
                ToPrimary(gamut.Vertices[1]),
                ToPrimary(gamut.Vertices[2]));
        }

        private static CieGamutPrimary ToPrimary(CieChromaticity chromaticity)
        {
            return new CieGamutPrimary(chromaticity.X, chromaticity.Y);
        }
    }
}
