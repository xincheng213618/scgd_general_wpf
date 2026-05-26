using ColorVision.ImageEditor.Cie;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Conoscope.Analysis
{
    public enum MeasurementCaptureKind
    {
        ManualFile,
        FocusPoints
    }

    public sealed record MeasurementPoint(
        string Key,
        string Name,
        ImageMeasurement Measurement,
        double? AzimuthDegrees,
        double? PolarDegrees,
        double? RadiusDegrees)
    {
        public string CoordinateDisplay => AzimuthDegrees.HasValue && PolarDegrees.HasValue && RadiusDegrees.HasValue
            ? $"A={AzimuthDegrees.Value:F2}°, P={PolarDegrees.Value:F2}°, R={RadiusDegrees.Value:F2}°"
            : "-";
    }

    public sealed record MeasurementCapture(
        string SlotName,
        string SourceLabel,
        MeasurementCaptureKind Kind,
        IReadOnlyList<MeasurementPoint> Points)
    {
        public int PointCount => Points.Count;
        public bool IsSinglePoint => Points.Count == 1;

        public string SourceDisplayName => Kind == MeasurementCaptureKind.ManualFile
            ? Path.GetFileName(SourceLabel)
            : SourceLabel;

        public static MeasurementCapture FromManualFile(string slotName, ImageMeasurement measurement)
        {
            return new MeasurementCapture(
                slotName,
                measurement.FilePath,
                MeasurementCaptureKind.ManualFile,
                new[]
                {
                    new MeasurementPoint(
                        "ManualAverage",
                        "整体平均",
                        measurement,
                        null,
                        null,
                        null)
                });
        }

        public static MeasurementCapture FromFocusPoints(string slotName, string sourceLabel, IReadOnlyList<MeasurementPoint> points)
        {
            return new MeasurementCapture(slotName, sourceLabel, MeasurementCaptureKind.FocusPoints, points);
        }
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

    public sealed class DefaultBatchColorGamutCalculator
    {
        private readonly DefaultColorGamutCalculator singleCalculator = new();

        public ColorGamutComputationResult Calculate(MeasurementCapture redCapture, MeasurementCapture greenCapture, MeasurementCapture blueCapture, ColorGamutStandard standard)
        {
            IReadOnlyList<AlignedPointSet> alignedPoints = MeasurementCaptureAlignment.Align(redCapture, greenCapture, blueCapture);
            List<ColorGamutPointResult> results = new(alignedPoints.Count);

            foreach (AlignedPointSet alignedPoint in alignedPoints)
            {
                ColorGamutResult result = singleCalculator.Calculate(
                    alignedPoint.Points[0].Measurement,
                    alignedPoint.Points[1].Measurement,
                    alignedPoint.Points[2].Measurement,
                    standard);

                results.Add(new ColorGamutPointResult(
                    alignedPoint.Index,
                    alignedPoint.DisplayPoint.Key,
                    alignedPoint.DisplayPoint.Name,
                    alignedPoint.DisplayPoint.AzimuthDegrees,
                    alignedPoint.DisplayPoint.PolarDegrees,
                    alignedPoint.DisplayPoint.RadiusDegrees,
                    alignedPoint.Points[0].Measurement,
                    alignedPoint.Points[1].Measurement,
                    alignedPoint.Points[2].Measurement,
                    result.SampleArea,
                    result.StandardArea,
                    result.CoveragePercent));
            }

            return new ColorGamutComputationResult(standard, results);
        }
    }

    public sealed class DefaultBatchContrastCalculator
    {
        private readonly DefaultContrastCalculator singleCalculator = new();

        public ContrastComputationResult Calculate(MeasurementCapture whiteCapture, MeasurementCapture blackCapture)
        {
            IReadOnlyList<AlignedPointSet> alignedPoints = MeasurementCaptureAlignment.Align(whiteCapture, blackCapture);
            List<ContrastPointResult> results = new(alignedPoints.Count);

            foreach (AlignedPointSet alignedPoint in alignedPoints)
            {
                ContrastResult contrast = singleCalculator.Calculate(
                    alignedPoint.Points[1].Measurement,
                    alignedPoint.Points[0].Measurement);

                results.Add(new ContrastPointResult(
                    alignedPoint.Index,
                    alignedPoint.DisplayPoint.Key,
                    alignedPoint.DisplayPoint.Name,
                    alignedPoint.DisplayPoint.AzimuthDegrees,
                    alignedPoint.DisplayPoint.PolarDegrees,
                    alignedPoint.DisplayPoint.RadiusDegrees,
                    alignedPoint.Points[0].Measurement,
                    alignedPoint.Points[1].Measurement,
                    contrast.Ratio));
            }

            return new ContrastComputationResult(results);
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

            throw new InvalidOperationException(Conoscope.Core.CompositeFormatCache.Format(Conoscope.Properties.Resources.MsgMeasurementCaptureMissingFocusPoint, capture.SlotName, key, capture.SourceDisplayName));
        }
    }
}