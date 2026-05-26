using ColorVision.ImageEditor.Cie;
using Conoscope.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace Conoscope.Analysis
{
    public sealed record ImageMeasurement(
        string FilePath,
        double X,
        double Y,
        double Z,
        ConoscopeChromaticity Chromaticity)
    {
        public string FileName => Path.GetFileName(FilePath);
        public double Luminance => Y;
    }

    public sealed record ContrastResult(ImageMeasurement Black, ImageMeasurement White, double Ratio)
    {
        public string RatioText => double.IsFinite(Ratio) ? $"{Ratio:F3}:1" : Properties.Resources.Invalid;
    }

    public sealed class DefaultContrastCalculator
    {
        public ContrastResult Calculate(ImageMeasurement black, ImageMeasurement white)
        {
            if (black.Luminance <= 0)
            {
                throw new InvalidOperationException(Properties.Resources.BlackLuminanceMustBePositive);
            }

            return new ContrastResult(black, white, white.Luminance / black.Luminance);
        }
    }

    public readonly record struct ChromaticityPoint(double X, double Y)
    {
        public string Text => $"({X:F4}, {Y:F4})";
    }

    public sealed record ColorGamutStandard(string Name, ChromaticityPoint Red, ChromaticityPoint Green, ChromaticityPoint Blue)
    {
        public override string ToString() => Name;
    }

    public sealed record ColorGamutResult(
        ImageMeasurement Red,
        ImageMeasurement Green,
        ImageMeasurement Blue,
        ColorGamutStandard Standard,
        double SampleArea,
        double StandardArea,
        double CoveragePercent);

    public sealed class DefaultColorGamutCalculator
    {
        public ColorGamutResult Calculate(ImageMeasurement red, ImageMeasurement green, ImageMeasurement blue, ColorGamutStandard standard)
        {
            ArgumentNullException.ThrowIfNull(standard);

            double sampleArea = TriangleArea(ToPoint(red), ToPoint(green), ToPoint(blue));
            double standardArea = TriangleArea(standard.Red, standard.Green, standard.Blue);
            if (standardArea <= 0)
            {
                throw new InvalidOperationException(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.StandardGamutAreaInvalid, standard.Name));
            }

            return new ColorGamutResult(red, green, blue, standard, sampleArea, standardArea, sampleArea / standardArea * 100.0);
        }

        private static ChromaticityPoint ToPoint(ImageMeasurement measurement)
        {
            return new ChromaticityPoint(measurement.Chromaticity.x, measurement.Chromaticity.y);
        }

        private static double TriangleArea(ChromaticityPoint red, ChromaticityPoint green, ChromaticityPoint blue)
        {
            return Math.Abs((red.X * (green.Y - blue.Y) + green.X * (blue.Y - red.Y) + blue.X * (red.Y - green.Y)) / 2.0);
        }
    }

    public static class ColorGamutStandards
    {
        public static IReadOnlyList<ColorGamutStandard> All { get; } = new[]
        {
            FromGamut(CieGamuts.SRgb),
            FromGamut(CieGamuts.Rec709),
            FromGamut(CieGamuts.AdobeRgb),
            FromGamut(CieGamuts.DisplayP3),
            FromGamut(CieGamuts.DciP3),
            FromGamut(CieGamuts.Rec2020),
            FromGamut(CieGamuts.Ntsc1953),
            FromGamut(CieGamuts.EbuPal),
            FromGamut(CieGamuts.SmpteC),
            FromGamut(CieGamuts.ProPhotoRgb),
            FromGamut(CieGamuts.AcesCg)
        };

        private static ColorGamutStandard FromGamut(CieGamut gamut)
        {
            if (gamut.Vertices.Count < 3)
            {
                throw new InvalidOperationException(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.GamutNeedsAtLeastThreeVertices, gamut.Name));
            }

            return new ColorGamutStandard(
                gamut.Name,
                ToPoint(gamut.Vertices[0]),
                ToPoint(gamut.Vertices[1]),
                ToPoint(gamut.Vertices[2]));
        }

        private static ChromaticityPoint ToPoint(CieChromaticity chromaticity)
        {
            return new ChromaticityPoint(chromaticity.X, chromaticity.Y);
        }
    }
}
