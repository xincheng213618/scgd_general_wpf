using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ProjectStarkSemi.Conoscope
{
    public readonly record struct ConoscopeXyzValue(double X, double Y, double Z);

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
    }

    public static class ConoscopeExportService
    {
        public static void ExportAngleModeToCsv(string filePath, ExportChannel channel, ConoscopeExportContext context)
        {
            List<ExportLine> angleLines = CreateAzimuthLines(context, 1, 1, 0, 180);
            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteMatrix(writer, "Azimuth Export Data (Phi \\ Theta Format)", channel, context,
                "# Phi (Column): Diameter line direction (0°-180°)",
                "# Theta (Row): Sample point position (0 to MaxAngle)",
                "Phi \\ Theta",
                angleLines,
                item => item.HeaderLabel("F0"));
        }

        public static void ExportCircleModeToCsv(string filePath, ExportChannel channel, ConoscopeExportContext context)
        {
            List<ExportLine> circles = CreatePolarCircles(context, 1, 1);
            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteMatrix(writer, "Polar Angle Export Data (Phi \\ Theta Format)", channel, context,
                $"# Polar Angle Count: {circles.Count} (including 0-degree center point)\n# Phi (Column): Radius angle (viewing angle, 0-{context.MaxAngle}°)",
                "# Theta (Row): Circumferential angle (0-359°)",
                "Phi \\ Theta",
                circles,
                item => item.HeaderLabel("F0"));
        }

        public static void ExportAzimuthWithStep(string filePath, ExportChannel channel, ConoscopeExportContext context, double azimuthStep, double radialStep)
        {
            List<ExportLine> angleLines = CreateAzimuthLines(context, azimuthStep, radialStep, 0, 180);
            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteMatrix(writer, $"Azimuth Export Data (azimuth step = {azimuthStep}°, radial step = {radialStep}°)", channel, context,
                $"# Phi (Column): Azimuth angle (0°-180°, step={azimuthStep}°)",
                $"# Theta (Row): Polar radius (0 to MaxAngle, step={radialStep}°)",
                "Phi \\ Theta",
                angleLines,
                item => item.HeaderLabel("F2"));
        }

        public static void ExportPolarWithStep(string filePath, ExportChannel channel, ConoscopeExportContext context, double polarStep, double circumStep)
        {
            List<ExportLine> circles = CreatePolarCircles(context, polarStep, circumStep);
            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteMatrix(writer, $"Polar Angle Export Data (ring step = {polarStep}°, circumferential step = {circumStep}°)", channel, context,
                $"# Phi (Column): Polar radius angle (0-{context.MaxAngle}°, step={polarStep}°)",
                $"# Theta (Row): Circumferential angle (0-360°, step={circumStep}°)",
                "Phi \\ Theta",
                circles,
                item => item.HeaderLabel("F2"));
        }

        public static void ExportAzimuthCrossSection(string filePath, ExportChannel channel, ConoscopeExportContext context, double azimuthAngle)
        {
            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteHeader(writer, $"Azimuth Cross-Section Export (Angle = {azimuthAngle}°)", channel, context);
            writer.WriteLine("Polar Radius (degrees),Value");

            double radians = (90 - azimuthAngle) * Math.PI / 180.0;
            for (int theta = -(int)context.MaxAngle; theta <= (int)context.MaxAngle; theta++)
            {
                double radiusPixels = theta * context.PixelsPerDegree;
                int ix = ClampToInt((int)Math.Round(context.Center.X + radiusPixels * Math.Cos(radians)), 0, context.ImageWidth - 1);
                int iy = ClampToInt((int)Math.Round(context.Center.Y + radiusPixels * Math.Sin(radians)), 0, context.ImageHeight - 1);
                double value = ReadExportValue(channel, context, ix, iy);
                writer.WriteLine($"{theta},{ConoscopeColorimetry.FormatChannelValue(value, channel)}");
            }
        }

        public static void ExportPolarCrossSection(string filePath, ExportChannel channel, ConoscopeExportContext context, double polarAngle)
        {
            using StreamWriter writer = new StreamWriter(filePath, false, Encoding.UTF8);
            WriteHeader(writer, $"Polar Cross-Section Export (Radius Angle = {polarAngle}°)", channel, context);
            writer.WriteLine("Circumferential Angle (degrees),Value");

            double radiusPixels = polarAngle * context.PixelsPerDegree;
            for (int theta = 0; theta <= 360; theta++)
            {
                double radians = (90 - theta) * Math.PI / 180.0;
                int ix = ClampToInt((int)Math.Round(context.Center.X + radiusPixels * Math.Cos(radians)), 0, context.ImageWidth - 1);
                int iy = ClampToInt((int)Math.Round(context.Center.Y + radiusPixels * Math.Sin(radians)), 0, context.ImageHeight - 1);
                double value = ReadExportValue(channel, context, ix, iy);
                writer.WriteLine($"{theta},{ConoscopeColorimetry.FormatChannelValue(value, channel)}");
            }
        }

        private static List<ExportLine> CreateAzimuthLines(ConoscopeExportContext context, double azimuthStep, double radialStep, double startPhi, double endPhi)
        {
            List<ExportLine> lines = new List<ExportLine>();
            for (double phi = startPhi; phi <= endPhi + 0.0001; phi += azimuthStep)
            {
                ExportLine line = new ExportLine(phi);
                double radians = (180 - phi) * Math.PI / 180.0;
                for (double theta = 0; theta <= context.MaxAngle + 0.0001; theta += radialStep)
                {
                    double radiusPixels = theta * context.PixelsPerDegree;
                    int ix = ClampToInt((int)Math.Round(context.Center.X + radiusPixels * Math.Cos(radians)), 0, context.ImageWidth - 1);
                    int iy = ClampToInt((int)Math.Round(context.Center.Y + radiusPixels * Math.Sin(radians)), 0, context.ImageHeight - 1);
                    line.Samples.Add(new ExportSample(theta, ix, iy, context.ReadXyz(ix, iy)));
                }

                lines.Add(line);
            }

            return lines;
        }

        private static List<ExportLine> CreatePolarCircles(ConoscopeExportContext context, double polarStep, double circumStep)
        {
            List<ExportLine> circles = new List<ExportLine>();
            for (double polarAngle = 0; polarAngle <= context.MaxAngle + 0.0001; polarAngle += polarStep)
            {
                ExportLine circle = new ExportLine(polarAngle);
                double radiusPixels = polarAngle * context.PixelsPerDegree;
                for (double theta = 0; theta <= 360 + 0.0001; theta += circumStep)
                {
                    double radians = (90 - theta) * Math.PI / 180.0;
                    int ix = ClampToInt((int)Math.Round(context.Center.X + radiusPixels * Math.Cos(radians)), 0, context.ImageWidth - 1);
                    int iy = ClampToInt((int)Math.Round(context.Center.Y + radiusPixels * Math.Sin(radians)), 0, context.ImageHeight - 1);
                    circle.Samples.Add(new ExportSample(theta, ix, iy, context.ReadXyz(ix, iy)));
                }

                circles.Add(circle);
            }

            return circles;
        }

        private static void WriteMatrix(StreamWriter writer, string title, ExportChannel channel, ConoscopeExportContext context, string firstAxisComment, string secondAxisComment, string headerTitle, List<ExportLine> lines, Func<ExportLine, string> formatHeader)
        {
            if (lines.Count == 0)
            {
                return;
            }

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
                        dataLine.Append($",{ConoscopeColorimetry.FormatChannelValue(value, channel)}");
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

        private static double ReadExportValue(ExportChannel channel, ConoscopeExportContext context, int imageX, int imageY)
        {
            return ReadExportValue(channel, context, imageX, imageY, context.ReadXyz(imageX, imageY));
        }

        private static double ReadExportValue(ExportChannel channel, ConoscopeExportContext context, int imageX, int imageY, ConoscopeXyzValue xyz)
        {
            if (channel == ExportChannel.ColorDifference)
            {
                if (context.ReadColorDifference == null)
                {
                    throw new InvalidOperationException("色差通道需要先设置色差基准");
                }

                return context.ReadColorDifference(imageX, imageY);
            }

            return ConoscopeColorimetry.GetChannelValue(xyz.X, xyz.Y, xyz.Z, channel);
        }

        private static int ClampToInt(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            return Math.Max(min, Math.Min(value, max));
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