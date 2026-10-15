using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Windows;

namespace Conoscope.Core
{
    public readonly record struct ConoscopeXyzValue(double X, double Y, double Z);

    public sealed class ConoscopeCrossSectionExportOptions
    {
        public double StepDegrees { get; init; } = ConoscopeExportService.MinimumStepDegrees;
        public bool IncludeMetadata { get; init; } = true;
        public int DecimalPlaces { get; init; } = 4;
    }

    public readonly record struct ConoscopeExportProgress(long CompletedSamples, long TotalSamples);

    public sealed class ConoscopeExportContext
    {
        public CancellationToken CancellationToken { get; init; }
        public IProgress<ConoscopeExportProgress>? Progress { get; init; }

        internal ConoscopeExportContext ForExecution(CancellationToken token, IProgress<ConoscopeExportProgress> progress) => new()
        {
            ModelName = ModelName, ImageWidth = ImageWidth, ImageHeight = ImageHeight, Center = Center,
            MaxAngle = MaxAngle, PixelsPerDegree = PixelsPerDegree, ReadXyz = ReadXyz,
            ReadColorDifference = ReadColorDifference, ReadContrast = ReadContrast, CancellationToken = token, Progress = progress
        };

        public required string ModelName { get; init; }
        public required int ImageWidth { get; init; }
        public required int ImageHeight { get; init; }
        public required Point Center { get; init; }
        public required double MaxAngle { get; init; }
        public required double PixelsPerDegree { get; init; }
        public required Func<int, int, ConoscopeXyzValue> ReadXyz { get; init; }
        public Func<int, int, double>? ReadColorDifference { get; init; }
        public Func<int, int, double>? ReadContrast { get; init; }
    }

    public static class ConoscopeExportService
    {
        public const double MinimumStepDegrees = 0.1;

        public static void ExportAngleModeToCsv(string filePath, ExportChannel channel, ConoscopeExportContext context, int decimalPlaces = 4)
        {
            double[] columns = MatrixAxis(0, 180, 1, true);
            double[] positions = MatrixAxis(-context.MaxAngle, context.MaxAngle, 1);
            WriteMatrix(filePath, "Azimuth Export Data (Phi \\ Theta Format)", channel, context,
                "# Phi (Column): Diameter line direction (0°-179°, 180 columns)",
                "# Theta (Row): Sample point position along the full diameter (-MaxAngle to MaxAngle)",
                "Phi \\ Theta",
                columns, positions, false, "F0",
                decimalPlaces);
        }

        public static void ExportCircleModeToCsv(string filePath, ExportChannel channel, ConoscopeExportContext context, int decimalPlaces = 4)
        {
            double[] columns = MatrixAxis(0, context.MaxAngle, 1);
            double[] positions = MatrixAxis(0, 360, 1);
            WriteMatrix(filePath, "Polar Angle Export Data (Phi \\ Theta Format)", channel, context,
                FormattableString.Invariant($"# Polar Angle Count: {columns.Length} (including 0-degree center point)\n# Phi (Column): Radius angle (viewing angle, 0-{context.MaxAngle}°)"),
                "# Theta (Row): Circumferential angle (0-360°)",
                "Phi \\ Theta",
                columns, positions, true, "F0",
                decimalPlaces);
        }

        public static void ExportAzimuthWithStep(string filePath, ExportChannel channel, ConoscopeExportContext context, double azimuthStep, double radialStep, int decimalPlaces = 4)
        {
            double[] columns = MatrixAxis(0, 180, azimuthStep, true);
            double[] positions = MatrixAxis(-context.MaxAngle, context.MaxAngle, radialStep);
            WriteMatrix(filePath, FormattableString.Invariant($"Azimuth Export Data (azimuth step = {azimuthStep}°, radial step = {radialStep}°)"), channel, context,
                FormattableString.Invariant($"# Phi (Column): Azimuth angle (0°-<180°, step={azimuthStep}°)"),
                FormattableString.Invariant($"# Theta (Row): Full-diameter sample position (-MaxAngle to MaxAngle, step={radialStep}°)"),
                "Phi \\ Theta",
                columns, positions, false, "F2",
                decimalPlaces);
        }

        public static void ExportPolarWithStep(string filePath, ExportChannel channel, ConoscopeExportContext context, double polarStep, double circumStep, int decimalPlaces = 4)
        {
            double[] columns = MatrixAxis(0, context.MaxAngle, polarStep);
            double[] positions = MatrixAxis(0, 360, circumStep);
            WriteMatrix(filePath, FormattableString.Invariant($"Polar Angle Export Data (ring step = {polarStep}°, circumferential step = {circumStep}°)"), channel, context,
                FormattableString.Invariant($"# Phi (Column): Polar radius angle (0-{context.MaxAngle}°, step={polarStep}°)"),
                FormattableString.Invariant($"# Theta (Row): Circumferential angle (0-360°, step={circumStep}°)"),
                "Phi \\ Theta",
                columns, positions, true, "F2",
                decimalPlaces);
        }

        public static void ExportAzimuthCrossSection(string filePath, ExportChannel channel, ConoscopeExportContext context, double azimuthAngle, ConoscopeCrossSectionExportOptions options)
        {
            options ??= new ConoscopeCrossSectionExportOptions();
            WriteCrossSection(filePath, channel, context, options,
                FormattableString.Invariant($"Azimuth Cross-Section Export (Angle = {azimuthAngle}°)"),
                "Azimuth Position (degrees)", EnumerateRange(-context.MaxAngle, context.MaxAngle, options.StepDegrees),
                position => CreateAzimuthSample(context, azimuthAngle, position));
        }

        public static void ExportPolarCrossSection(string filePath, ExportChannel channel, ConoscopeExportContext context, double polarAngle, ConoscopeCrossSectionExportOptions options)
        {
            options ??= new ConoscopeCrossSectionExportOptions();
            WriteCrossSection(filePath, channel, context, options,
                FormattableString.Invariant($"Polar Cross-Section Export (Radius Angle = {polarAngle}°)"),
                "Circumferential Angle (degrees)", EnumerateRange(0, 360, options.StepDegrees),
                position => CreatePolarSample(context, polarAngle, position));
        }

        private static void WriteCrossSection(string path, ExportChannel channel, ConoscopeExportContext context,
            ConoscopeCrossSectionExportOptions options, string title, string axis, IEnumerable<double> range, Func<double, ExportSample> sampleAt)
        {
            double[] positions = range.ToArray();
            int digits = Math.Clamp(options.DecimalPlaces, 0, 8);
            ConoscopeAtomicFile.Write(path, writer =>
            {
                if (options.IncludeMetadata) WriteHeader(writer, title, channel, context);
                writer.WriteLine($"{axis},{GetExportValueHeader(channel)}");
                Stopwatch timer = Stopwatch.StartNew();
                for (int index = 0; index < positions.Length; index++)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    ExportSample sample = sampleAt(positions[index]);
                    double value = ReadExportValue(channel, context, sample.ImageX, sample.ImageY, sample.Xyz);
                    writer.WriteLine($"{sample.Position.ToString("F2", CultureInfo.InvariantCulture)},{ConoscopeColorimetry.FormatChannelValue(value, channel, digits)}");
                    if (timer.ElapsedMilliseconds >= 100 || index == positions.Length - 1)
                    {
                        context.Progress?.Report(new(index + 1, positions.Length));
                        timer.Restart();
                    }
                }
            }, context.CancellationToken);
        }

        // Accumulate exactly as the legacy exporter did, including its endpoint tolerance.
        internal static double[] MatrixAxis(double start, double end, double step, bool exclusiveEnd = false)
        {
            ValidateStep(step);
            if (!double.IsFinite(start) || !double.IsFinite(end) || start > end || end - start > 360)
                throw new ArgumentOutOfRangeException(nameof(end));
            double normalized = Math.Max(0.0001, step);
            List<double> values = new();
            for (double value = start; exclusiveEnd ? value < end - 0.0001 : value <= end + 0.0001; value += normalized)
                values.Add(value);
            return values.ToArray();
        }

        internal static long EstimateMatrixSamples(double maxAngle, double columnStep, double rowStep, bool polar)
            => (long)MatrixAxis(0, polar ? maxAngle : 180, columnStep, !polar).Length
                * MatrixAxis(polar ? 0 : -maxAngle, polar ? 360 : maxAngle, rowStep).Length;

        private static void ValidateStep(double step)
        {
            if (!double.IsFinite(step) || step < MinimumStepDegrees) throw new ArgumentOutOfRangeException(nameof(step), "Export step must be at least 0.1 degrees.");
        }

        private static ExportSample CreateAzimuthSample(ConoscopeExportContext context, double azimuthAngle, double polarAngle)
        {
            double normalizedAngle = ConoscopeCoordinateAxisParam.NormalizeAzimuthAngle(azimuthAngle);
            double radians = normalizedAngle * Math.PI / 180.0;
            double radiusPixels = polarAngle * context.PixelsPerDegree;
            int imageX = Math.Clamp((int)Math.Round(context.Center.X + radiusPixels * Math.Cos(radians)), 0, context.ImageWidth - 1);
            int imageY = Math.Clamp((int)Math.Round(context.Center.Y - radiusPixels * Math.Sin(radians)), 0, context.ImageHeight - 1);
            return new ExportSample(polarAngle, imageX, imageY, context.ReadXyz(imageX, imageY));
        }

        private static ExportSample CreatePolarSample(ConoscopeExportContext context, double polarAngle, double circumferentialAngle)
        {
            double normalizedAngle = circumferentialAngle % 360.0;
            if (normalizedAngle < 0)
            {
                normalizedAngle += 360.0;
            }

            double radians = normalizedAngle * Math.PI / 180.0;
            double radiusPixels = polarAngle * context.PixelsPerDegree;
            int imageX = Math.Clamp((int)Math.Round(context.Center.X + radiusPixels * Math.Cos(radians)), 0, context.ImageWidth - 1);
            int imageY = Math.Clamp((int)Math.Round(context.Center.Y - radiusPixels * Math.Sin(radians)), 0, context.ImageHeight - 1);
            return new ExportSample(circumferentialAngle, imageX, imageY, context.ReadXyz(imageX, imageY));
        }

        private static void WriteMatrix(string path, string title, ExportChannel channel, ConoscopeExportContext context,
            string firstAxisComment, string secondAxisComment, string headerTitle, double[] columns, double[] positions,
            bool polar, string headerFormat, int decimalPlaces)
        {
            int digits = Math.Clamp(decimalPlaces, 0, 8);
            long total = (long)columns.Length * positions.Length;
            ConoscopeAtomicFile.Write(path, writer =>
            {
                WriteHeader(writer, title, channel, context);
                foreach (string line in firstAxisComment.Split('\n')) writer.WriteLine(line);
                writer.WriteLine(secondAxisComment);
                writer.WriteLine();
                writer.Write(headerTitle);
                foreach (double column in columns) writer.Write($",{column.ToString(headerFormat, CultureInfo.InvariantCulture)}");
                writer.WriteLine();
                Stopwatch timer = Stopwatch.StartNew();
                long completed = 0;
                foreach (double position in positions)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    writer.Write(position.ToString("F2", CultureInfo.InvariantCulture));
                    foreach (double column in columns)
                    {
                        if ((completed & 255) == 0) context.CancellationToken.ThrowIfCancellationRequested();
                        ExportSample sample = polar ? CreatePolarSample(context, column, position) : CreateAzimuthSample(context, column, position);
                        double value = ReadExportValue(channel, context, sample.ImageX, sample.ImageY, sample.Xyz);
                        writer.Write($",{ConoscopeColorimetry.FormatChannelValue(value, channel, digits)}");
                        completed++;
                    }
                    writer.WriteLine();
                    if (timer.ElapsedMilliseconds >= 100 || completed == total)
                    {
                        context.Progress?.Report(new(completed, total));
                        timer.Restart();
                    }
                }
            }, context.CancellationToken);
        }

        private static void WriteHeader(StreamWriter writer, string title, ExportChannel channel, ConoscopeExportContext context)
        {
            writer.WriteLine($"# {title}");
            writer.WriteLine(FormattableString.Invariant($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));
            writer.WriteLine($"# Export Channel: {channel}");
            writer.WriteLine($"# Model: {context.ModelName}");
            writer.WriteLine(FormattableString.Invariant($"# Max Angle: {context.MaxAngle}°"));
        }

        private static string GetExportValueHeader(ExportChannel channel)
        {
            string label = ConoscopeColorimetry.GetChannelLabel(channel);
            return channel is ExportChannel.X or ExportChannel.Y or ExportChannel.Z
                ? $"{label} (cd/m2)"
                : label;
        }

        private static double ReadExportValue(ExportChannel channel, ConoscopeExportContext context, int imageX, int imageY, ConoscopeXyzValue xyz)
        {
            if (channel == ExportChannel.ColorDifference)
            {
                if (context.ReadColorDifference == null)
                {
                    throw new InvalidOperationException(Conoscope.Properties.Resources.MsgColorDifferenceReferenceRequired);
                }

                return context.ReadColorDifference(imageX, imageY);
            }

            if (channel == ExportChannel.Contrast)
            {
                if (context.ReadContrast == null)
                {
                    throw new InvalidOperationException(Conoscope.Properties.Resources.MsgContrastReferenceRequired);
                }

                return context.ReadContrast(imageX, imageY);
            }

            return ConoscopeColorimetry.GetChannelValue(xyz.X, xyz.Y, xyz.Z, channel);
        }

        private static IEnumerable<double> EnumerateRange(double start, double end, double step)
        {
            ValidateStep(step);
            if (!double.IsFinite(start) || !double.IsFinite(end) || start > end || end - start > 360)
                throw new ArgumentOutOfRangeException(nameof(end));
            double normalizedStep = Math.Max(0.0001, step);
            double epsilon = normalizedStep / 1000.0;
            double current = start;

            while (current < end - epsilon)
            {
                yield return current;
                current += normalizedStep;
            }

            yield return end;
        }

        private readonly record struct ExportSample(double Position, int ImageX, int ImageY, ConoscopeXyzValue Xyz);
    }
}
