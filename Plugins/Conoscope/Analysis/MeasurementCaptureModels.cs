using ColorVision.ImageEditor.Cie;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Conoscope.Analysis
{
    public sealed record MeasurementPoint(
        string Key,
        string Name,
        ImageMeasurement Measurement,
        double? AzimuthDegrees,
        double? PolarDegrees,
        double? RadiusDegrees);

    public sealed record MeasurementCapture(
        string SlotName,
        string SourceLabel,
        IReadOnlyList<MeasurementPoint> Points)
    {
        public int PointCount => Points.Count;
    }

    public sealed record ColorGamutPointResult(
        int Index,
        string PointKey,
        string PointName,
        double? AzimuthDegrees,
        double? PolarDegrees,
        double? RadiusDegrees,
        ImageMeasurement Red,
        ImageMeasurement Green,
        ImageMeasurement Blue,
        double SampleArea,
        double StandardArea,
        double CoveragePercent)
    {
        public CieChromaticity RedChromaticity => new(Red.Chromaticity.x, Red.Chromaticity.y);
        public CieChromaticity GreenChromaticity => new(Green.Chromaticity.x, Green.Chromaticity.y);
        public CieChromaticity BlueChromaticity => new(Blue.Chromaticity.x, Blue.Chromaticity.y);
    }

    public sealed record ColorGamutComputationResult(ColorGamutStandard Standard, IReadOnlyList<ColorGamutPointResult> Points)
    {
        public double AverageCoveragePercent => Points.Count == 0 ? 0 : Points.Average(item => item.CoveragePercent);
        public double MinimumCoveragePercent => Points.Count == 0 ? 0 : Points.Min(item => item.CoveragePercent);
        public double MaximumCoveragePercent => Points.Count == 0 ? 0 : Points.Max(item => item.CoveragePercent);
    }

    public sealed record ContrastPointResult(
        int Index,
        string PointKey,
        string PointName,
        double? AzimuthDegrees,
        double? PolarDegrees,
        double? RadiusDegrees,
        ImageMeasurement White,
        ImageMeasurement Black,
        double Ratio)
    {
        public string RatioText => double.IsFinite(Ratio) ? $"{Ratio:F3}:1" : Properties.Resources.Invalid;
    }

    public sealed record ContrastComputationResult(IReadOnlyList<ContrastPointResult> Points)
    {
        public double AverageRatio => Points.Count == 0 ? 0 : Points.Average(item => item.Ratio);
        public double MinimumRatio => Points.Count == 0 ? 0 : Points.Min(item => item.Ratio);
        public double MaximumRatio => Points.Count == 0 ? 0 : Points.Max(item => item.Ratio);
    }

    public static class ConoscopeAnalysis
    {
        public static ColorGamutComputationResult CalculateColorGamut(MeasurementCapture redCapture, MeasurementCapture greenCapture, MeasurementCapture blueCapture, ColorGamutStandard standard)
        {
            ArgumentNullException.ThrowIfNull(standard);
            double standardArea = TriangleArea(standard.Red, standard.Green, standard.Blue);
            if (standardArea <= 0)
            {
                throw new InvalidOperationException(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.StandardGamutAreaInvalid, standard.Name));
            }

            IReadOnlyList<AlignedPointSet> alignedPoints = MeasurementCaptureAlignment.Align(redCapture, greenCapture, blueCapture);
            List<ColorGamutPointResult> results = new(alignedPoints.Count);

            foreach (AlignedPointSet alignedPoint in alignedPoints)
            {
                ImageMeasurement red = alignedPoint.Points[0].Measurement;
                ImageMeasurement green = alignedPoint.Points[1].Measurement;
                ImageMeasurement blue = alignedPoint.Points[2].Measurement;
                double sampleArea = TriangleArea(ToPoint(red), ToPoint(green), ToPoint(blue));

                results.Add(new ColorGamutPointResult(
                    alignedPoint.Index,
                    alignedPoint.DisplayPoint.Key,
                    alignedPoint.DisplayPoint.Name,
                    alignedPoint.DisplayPoint.AzimuthDegrees,
                    alignedPoint.DisplayPoint.PolarDegrees,
                    alignedPoint.DisplayPoint.RadiusDegrees,
                    red,
                    green,
                    blue,
                    sampleArea,
                    standardArea,
                    sampleArea / standardArea * 100.0));
            }

            return new ColorGamutComputationResult(standard, results);
        }

        public static ContrastComputationResult CalculateContrast(MeasurementCapture whiteCapture, MeasurementCapture blackCapture)
        {
            IReadOnlyList<AlignedPointSet> alignedPoints = MeasurementCaptureAlignment.Align(whiteCapture, blackCapture);
            List<ContrastPointResult> results = new(alignedPoints.Count);

            foreach (AlignedPointSet alignedPoint in alignedPoints)
            {
                ImageMeasurement white = alignedPoint.Points[0].Measurement;
                ImageMeasurement black = alignedPoint.Points[1].Measurement;
                if (black.Luminance <= 0)
                {
                    throw new InvalidOperationException(Properties.Resources.BlackLuminanceMustBePositive);
                }

                results.Add(new ContrastPointResult(
                    alignedPoint.Index,
                    alignedPoint.DisplayPoint.Key,
                    alignedPoint.DisplayPoint.Name,
                    alignedPoint.DisplayPoint.AzimuthDegrees,
                    alignedPoint.DisplayPoint.PolarDegrees,
                    alignedPoint.DisplayPoint.RadiusDegrees,
                    white,
                    black,
                    white.Luminance / black.Luminance));
            }

            return new ContrastComputationResult(results);
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

    internal sealed record AlignedPointSet(int Index, MeasurementPoint DisplayPoint, IReadOnlyList<MeasurementPoint> Points);

    internal static class MeasurementCaptureAlignment
    {
        public static IReadOnlyList<AlignedPointSet> Align(params MeasurementCapture[] captures)
        {
            if (captures == null || captures.Length == 0)
            {
                throw new ArgumentException(Conoscope.Properties.Resources.MsgNoMeasurementDataToAlign, paramName: nameof(captures));
            }

            if (captures.Any(capture => capture.Points.Count == 0))
            {
                throw new InvalidOperationException(Conoscope.Properties.Resources.MsgEmptyMeasurementDataCannotAlignFocusPoints);
            }

            List<MeasurementCapture> multiPointCaptures = captures.Where(capture => capture.Points.Count > 1).ToList();
            if (multiPointCaptures.Count == 0)
            {
                MeasurementPoint displayPoint = captures[0].Points[0];
                return new[]
                {
                    new AlignedPointSet(1, displayPoint, captures.Select(capture => capture.Points[0]).ToArray())
                };
            }

            List<string> sharedKeys = multiPointCaptures[0].Points.Select(point => point.Key).ToList();
            foreach (MeasurementCapture capture in multiPointCaptures.Skip(1))
            {
                HashSet<string> captureKeys = capture.Points.Select(point => point.Key).ToHashSet(StringComparer.Ordinal);
                sharedKeys = sharedKeys.Where(captureKeys.Contains).ToList();
            }

            if (sharedKeys.Count > 0)
            {
                return sharedKeys
                    .Select((key, index) => new AlignedPointSet(
                        index + 1,
                        ResolveDisplayPoint(captures, key),
                        captures.Select(capture => ResolvePoint(capture, key)).ToArray()))
                    .ToArray();
            }

            int? commonCount = multiPointCaptures.Select(capture => capture.Points.Count).Distinct().Count() == 1
                ? multiPointCaptures[0].Points.Count
                : null;

            if (commonCount.HasValue)
            {
                return Enumerable.Range(0, commonCount.Value)
                    .Select(index => new AlignedPointSet(
                        index + 1,
                        multiPointCaptures[0].Points[index],
                        captures.Select(capture => capture.Points.Count == 1 ? capture.Points[0] : capture.Points[index]).ToArray()))
                    .ToArray();
            }

            throw new InvalidOperationException(Properties.Resources.FocusPointMismatchError);
        }

        private static MeasurementPoint ResolveDisplayPoint(IReadOnlyList<MeasurementCapture> captures, string key)
        {
            foreach (MeasurementCapture capture in captures)
            {
                MeasurementPoint? point = capture.Points.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
                if (point != null && (point.AzimuthDegrees.HasValue || point.PolarDegrees.HasValue || point.RadiusDegrees.HasValue))
                {
                    return point;
                }
            }

            foreach (MeasurementCapture capture in captures)
            {
                MeasurementPoint? point = capture.Points.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
                if (point != null)
                {
                    return point;
                }
            }

            throw new InvalidOperationException(Conoscope.Core.CompositeFormatCache.Format(Conoscope.Properties.Resources.MsgFocusPointDisplayInfoNotFound, key));
        }

        private static MeasurementPoint ResolvePoint(MeasurementCapture capture, string key)
        {
            MeasurementPoint? matchedPoint = capture.Points.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal));
            if (matchedPoint != null)
            {
                return matchedPoint;
            }

            if (capture.Points.Count == 1)
            {
                return capture.Points[0];
            }

            throw new InvalidOperationException(Conoscope.Core.CompositeFormatCache.Format(Conoscope.Properties.Resources.MsgMeasurementCaptureMissingFocusPoint, capture.SlotName, key, capture.SourceLabel));
        }
    }
}
