using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace Conoscope.Core
{
    public readonly record struct ConoscopeXyzValue(double X, double Y, double Z);

    public sealed class ConoscopeCrossSectionExportOptions
    {
        public double StepDegrees { get; init; } = 0.01;
        public bool IncludeMetadata { get; init; } = true;
        public int DecimalPlaces { get; init; } = 4;
    }

    public sealed class ConoscopeExportContext
    {
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
        public static void ExportAngleModeToCsv(string filePath, ExportChannel channel, ConoscopeExportContext context, int decimalPlaces = 4)
        {
            List<ExportLine> angleLines = CreateAzimuthLines(context, 1, 1, 0, 180);
            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteMatrix(writer, "Azimuth Export Data (Phi \\ Theta Format)", channel, context,
                "# Phi (Column): Diameter line direction (0°-179°, 180 columns)",
                "# Theta (Row): Sample point position along the full diameter (-MaxAngle to MaxAngle)",
                "Phi \\ Theta",
                angleLines,
                item => item.HeaderLabel("F0"),
                decimalPlaces);
        }

        public static void ExportCircleModeToCsv(string filePath, ExportChannel channel, ConoscopeExportContext context, int decimalPlaces = 4)
        {
            List<ExportLine> circles = CreatePolarCircles(context, 1, 1);
            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteMatrix(writer, "Polar Angle Export Data (Phi \\ Theta Format)", channel, context,
                $"# Polar Angle Count: {circles.Count} (including 0-degree center point)\n# Phi (Column): Radius angle (viewing angle, 0-{context.MaxAngle}°)",
                "# Theta (Row): Circumferential angle (0-360°)",
                "Phi \\ Theta",
                circles,
                item => item.HeaderLabel("F0"),
                decimalPlaces);
        }

        public static void ExportAzimuthWithStep(string filePath, ExportChannel channel, ConoscopeExportContext context, double azimuthStep, double radialStep, int decimalPlaces = 4)
        {
            List<ExportLine> angleLines = CreateAzimuthLines(context, azimuthStep, radialStep, 0, 180);
            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteMatrix(writer, $"Azimuth Export Data (azimuth step = {azimuthStep}°, radial step = {radialStep}°)", channel, context,
                $"# Phi (Column): Azimuth angle (0°-<180°, step={azimuthStep}°)",
                $"# Theta (Row): Full-diameter sample position (-MaxAngle to MaxAngle, step={radialStep}°)",
                "Phi \\ Theta",
                angleLines,
                item => item.HeaderLabel("F2"),
                decimalPlaces);
        }

        public static void ExportPolarWithStep(string filePath, ExportChannel channel, ConoscopeExportContext context, double polarStep, double circumStep, int decimalPlaces = 4)
        {
            List<ExportLine> circles = CreatePolarCircles(context, polarStep, circumStep);
            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteMatrix(writer, $"Polar Angle Export Data (ring step = {polarStep}°, circumferential step = {circumStep}°)", channel, context,
                $"# Phi (Column): Polar radius angle (0-{context.MaxAngle}°, step={polarStep}°)",
                $"# Theta (Row): Circumferential angle (0-360°, step={circumStep}°)",
                "Phi \\ Theta",
                circles,
                item => item.HeaderLabel("F2"),
                decimalPlaces);
        }

        public static void ExportAzimuthCrossSection(string filePath, ExportChannel channel, ConoscopeExportContext context, double azimuthAngle, ConoscopeCrossSectionExportOptions options)
        {
            options ??= new ConoscopeCrossSectionExportOptions();
            int decimalPlaces = Math.Clamp(options.DecimalPlaces, 0, 8);

            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            if (options.IncludeMetadata) WriteHeader(writer, $"Azimuth Cross-Section Export (Angle = {azimuthAngle}°)", channel, context);
            writer.WriteLine($"Azimuth Position (degrees),{GetExportValueHeader(channel)}");

            foreach (ExportSample sample in CreateAzimuthCrossSection(context, azimuthAngle, options.StepDegrees))
            {
                double value = ReadExportValue(channel, context, sample.ImageX, sample.ImageY, sample.Xyz);
                writer.WriteLine($"{sample.Position:F2},{ConoscopeColorimetry.FormatChannelValue(value, channel, decimalPlaces)}");
            }
        }

        public static void ExportPolarCrossSection(string filePath, ExportChannel channel, ConoscopeExportContext context, double polarAngle, ConoscopeCrossSectionExportOptions options)
        {
            options ??= new ConoscopeCrossSectionExportOptions();
            int decimalPlaces = Math.Clamp(options.DecimalPlaces, 0, 8);

            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            if (options.IncludeMetadata) WriteHeader(writer, $"Polar Cross-Section Export (Radius Angle = {polarAngle}°)", channel, context);
            writer.WriteLine($"Circumferential Angle (degrees),{GetExportValueHeader(channel)}");

            foreach (ExportSample sample in CreatePolarCrossSection(context, polarAngle, options.StepDegrees))
            {
                double value = ReadExportValue(channel, context, sample.ImageX, sample.ImageY, sample.Xyz);
                writer.WriteLine($"{sample.Position:F2},{ConoscopeColorimetry.FormatChannelValue(value, channel, decimalPlaces)}");
            }
        }

        private static List<ExportLine> CreateAzimuthLines(ConoscopeExportContext context, double azimuthStep, double radialStep, double startPhi, double endPhi)
        {
            List<ExportLine> lines = new List<ExportLine>();
            double normalizedStep = Math.Max(0.0001, azimuthStep);
            double normalizedRadialStep = Math.Max(0.0001, radialStep);

            for (double phi = startPhi; phi < endPhi - 0.0001; phi += normalizedStep)
            {
                ExportLine line = new ExportLine(phi);
                for (double theta = -context.MaxAngle; theta <= context.MaxAngle + 0.0001; theta += normalizedRadialStep)
                {
                    line.Samples.Add(CreateAzimuthSample(context, phi, theta));
                }

                lines.Add(line);
            }

            return lines;
        }

        private static List<ExportSample> CreateAzimuthCrossSection(ConoscopeExportContext context, double azimuthAngle)
        {
            double diameterPixels = context.MaxAngle * context.PixelsPerDegree * 2.0;
            int sampleCount = Math.Max(2, (int)Math.Round(diameterPixels) + 1);
            double stepDegrees = sampleCount <= 1 ? context.MaxAngle * 2.0 : context.MaxAngle * 2.0 / (sampleCount - 1);
            return CreateAzimuthCrossSection(context, azimuthAngle, stepDegrees);
        }

        private static List<ExportSample> CreateAzimuthCrossSection(ConoscopeExportContext context, double azimuthAngle, double stepDegrees)
        {
            List<ExportSample> samples = new List<ExportSample>();
            foreach (double position in EnumerateRange(-context.MaxAngle, context.MaxAngle, stepDegrees))
            {
                samples.Add(CreateAzimuthSample(context, azimuthAngle, position));
            }

            return samples;
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

        private static List<ExportSample> CreatePolarCrossSection(ConoscopeExportContext context, double polarAngle, double stepDegrees)
        {
            List<ExportSample> samples = new List<ExportSample>();
            foreach (double angle in EnumerateRange(0, 360, stepDegrees))
            {
                samples.Add(CreatePolarSample(context, polarAngle, angle));
            }

            return samples;
        }

        private static List<ExportLine> CreatePolarCircles(ConoscopeExportContext context, double polarStep, double circumStep)
        {
            List<ExportLine> circles = new List<ExportLine>();
            for (double polarAngle = 0; polarAngle <= context.MaxAngle + 0.0001; polarAngle += polarStep)
            {
                ExportLine circle = new ExportLine(polarAngle);
                for (double theta = 0; theta <= 360 + 0.0001; theta += circumStep)
                {
                    circle.Samples.Add(CreatePolarSample(context, polarAngle, theta));
                }

                circles.Add(circle);
            }

            return circles;
        }

        private static void WriteMatrix(StreamWriter writer, string title, ExportChannel channel, ConoscopeExportContext context, string firstAxisComment, string secondAxisComment, string headerTitle, List<ExportLine> lines, Func<ExportLine, string> formatHeader, int decimalPlaces)
        {
            if (lines.Count == 0)
            {
                return;
            }

            int normalizedDecimalPlaces = Math.Clamp(decimalPlaces, 0, 8);
            WriteHeader(writer, title, channel, context);
            foreach (string line in firstAxisComment.Split('\n'))
            {
                writer.WriteLine(line);
            }
            writer.WriteLine(secondAxisComment);
            writer.WriteLine();

            StringBuilder headerLine = new StringBuilder(headerTitle);
            foreach (ExportLine line in lines)
            {
                headerLine.Append($",{formatHeader(line)}");
            }
            writer.WriteLine(headerLine.ToString());

            int maxSamples = lines.Max(line => line.Samples.Count);
            for (int index = 0; index < maxSamples; index++)
            {
                StringBuilder dataLine = new StringBuilder();
                dataLine.Append(lines[0].Samples.Count > index ? lines[0].Samples[index].Position.ToString("F2") : string.Empty);
                foreach (ExportLine line in lines)
                {
                    if (line.Samples.Count > index)
                    {
                        ExportSample sample = line.Samples[index];
                        double value = ReadExportValue(channel, context, sample.ImageX, sample.ImageY, sample.Xyz);
                        dataLine.Append($",{ConoscopeColorimetry.FormatChannelValue(value, channel, normalizedDecimalPlaces)}");
                    }
                    else
                    {
                        dataLine.Append(',');
                    }
                }
                writer.WriteLine(dataLine.ToString());
            }
        }

        private static void WriteHeader(StreamWriter writer, string title, ExportChannel channel, ConoscopeExportContext context)
        {
            writer.WriteLine($"# {title}");
            writer.WriteLine($"# Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"# Export Channel: {channel}");
            writer.WriteLine($"# Model: {context.ModelName}");
            writer.WriteLine($"# Max Angle: {context.MaxAngle}°");
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

        private sealed class ExportLine
        {
            public ExportLine(double angle)
            {
                Angle = angle;
            }

            public double Angle { get; }
            public List<ExportSample> Samples { get; } = new List<ExportSample>();

            public string HeaderLabel(string format)
            {
                return Angle.ToString(format);
            }
        }

        private readonly record struct ExportSample(double Position, int ImageX, int ImageY, ConoscopeXyzValue Xyz);
    }
}
