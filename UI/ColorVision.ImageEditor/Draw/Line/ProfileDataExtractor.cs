using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw.Line
{
    /// <summary>Compatibility façade over the catalog-backed image-profile algorithm.</summary>
    public static class ProfileDataExtractor
    {
        public static bool IsMultiChannelFormat(PixelFormat format)
            => format == PixelFormats.Bgr24
                || format == PixelFormats.Rgb24
                || format == PixelFormats.Bgr32
                || format == PixelFormats.Bgra32
                || format == PixelFormats.Pbgra32
                || format == PixelFormats.Rgb48
                || format == PixelFormats.Rgba64;

        public static ProfileData ExtractAlongPath(IList<Point> points, WriteableBitmap bitmap, int totalSteps = 500, bool closePath = false)
        {
            ArgumentNullException.ThrowIfNull(points);
            ArgumentNullException.ThrowIfNull(bitmap);
            if (points.Count < 2 || totalSteps < 2) return ProfileData.CreateSingleChannel([]);

            double scaleX = SafeDpi(bitmap.DpiX) / 96;
            double scaleY = SafeDpi(bitmap.DpiY) / 96;
            AlgorithmPoint[] pixelPoints = points.Select(point => new AlgorithmPoint(point.X * scaleX, point.Y * scaleY)).ToArray();
            double totalLength = Length(pixelPoints, closePath);
            if (totalLength <= 0) return ProfileData.CreateSingleChannel([]);

            AlgorithmImageBuffer input;
            try { input = ImageAlgorithmInputFactory.Copy(bitmap); }
            catch (NotSupportedException) { return ProfileData.CreateSingleChannel([]); }
            ImageProfileParameters parameters = new()
            {
                SampleSpacingPixels = totalLength / (totalSteps - 1),
                Interpolation = ImageProfileInterpolation.Nearest,
                BoundaryMode = ImageProfileBoundaryMode.Skip,
                ClosePath = closePath,
                IncludeLuminance = true,
                IncludeAlpha = false,
                MaximumSamples = Math.Max(totalSteps + 1, 2),
            };
            AlgorithmInvocation invocation = AlgorithmInvocation.Create(
                StandardAlgorithmIds.ImageProfile,
                parameters,
                new PolylineAlgorithmRoi(pixelPoints));
            using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = invocation,
                Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Transferred }],
                RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
            }).AsTask().GetAwaiter().GetResult();
            if (result.Status != AlgorithmResultStatus.Succeeded) return ProfileData.CreateSingleChannel([]);

            AlgorithmTableArtifact table = result.GetArtifact<AlgorithmTableArtifact>("image-profile-samples")!;
            if (!IsMultiChannelFormat(bitmap.Format))
                return ProfileData.CreateSingleChannel(table.Rows.Select(row => Read(row, "Gray")).ToList());
            return ProfileData.CreateMultiChannel(
                table.Rows.Select(row => Read(row, "R")).ToList(),
                table.Rows.Select(row => Read(row, "G")).ToList(),
                table.Rows.Select(row => Read(row, "B")).ToList(),
                table.Rows.Select(row => Read(row, "Luminance")).ToList());
        }

        private static double Read(IReadOnlyDictionary<string, JsonElement> row, string name)
        {
            JsonElement value = row[name];
            if (value.ValueKind == JsonValueKind.Number) return value.GetDouble();
            string? status = row[name + "Status"].GetString();
            return status switch
            {
                "NaN" => double.NaN,
                "+Infinity" => double.PositiveInfinity,
                "-Infinity" => double.NegativeInfinity,
                _ => double.NaN,
            };
        }

        private static double Length(IReadOnlyList<AlgorithmPoint> points, bool closed)
        {
            double length = 0;
            int count = closed ? points.Count : points.Count - 1;
            for (int index = 0; index < count; index++)
            {
                AlgorithmPoint start = points[index];
                AlgorithmPoint end = points[(index + 1) % points.Count];
                double x = end.X - start.X;
                double y = end.Y - start.Y;
                length += Math.Sqrt(x * x + y * y);
            }
            return length;
        }

        private static double SafeDpi(double dpi) => double.IsFinite(dpi) && dpi > 0 ? dpi : 96;
    }
}
