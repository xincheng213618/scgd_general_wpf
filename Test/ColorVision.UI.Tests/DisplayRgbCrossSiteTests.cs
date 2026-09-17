using ColorVision.Algorithms;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class RgbCrossSiteFactAttribute : FactAttribute
{
    public RgbCrossSiteFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COLORVISION_RGB_CROSS_SOURCE")))
            Skip = "Set COLORVISION_RGB_CROSS_SOURCE and COLORVISION_RGB_CROSS_OUTPUT for a read-only field run.";
    }
}

public sealed partial class DisplayMetrologyTests
{
    [RgbCrossSiteFact]
    public async Task RgbCrossSiteRawProducesAuditableMeasurements()
    {
        string path = Path.GetFullPath(Environment.GetEnvironmentVariable("COLORVISION_RGB_CROSS_SOURCE")!);
        string directory = Path.GetFullPath(Environment.GetEnvironmentVariable("COLORVISION_RGB_CROSS_OUTPUT")
            ?? throw new InvalidOperationException("An explicit output directory is required."));
        Directory.CreateDirectory(directory);
        static string Hash(string file) { using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)); }
        string before = Hash(path);
        int header = CVFileUtil.ReadCIEFileHeader(path, out var raw);
        using (raw)
        {
            Assert.True(header > 0);
            Assert.Equal(2u, raw.Version);
            Assert.Equal(3, raw.Channels);
            Assert.Equal(16, raw.Bpp);
            Assert.InRange((long)raw.Cols * raw.Rows, 1024, DisplayMetrologyProvider.MaximumRgbCrossPixels);
            Assert.True(CVFileUtil.ReadCIEFileData(path, ref raw, header));
            Assert.Equal((long)raw.Cols * raw.Rows * 6, raw.Data.LongLength);
            using var input = new AlgorithmImageBuffer(raw.Cols, raw.Rows, raw.Cols * 6, AlgorithmImageFormat.Bgr48, raw.Data);
            var parameters = new RgbCrossRegistrationParameters();
            var clock = Stopwatch.StartNew();
            using var result = await Run(DisplayMetrologyIds.RgbCrossRegistration, parameters, input);
            clock.Stop();
            Success(result);
            var table = result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!;
            var metrics = result.Artifacts.OfType<AlgorithmMeasurementArtifact>().SelectMany(a => a.Measurements).ToArray();
            string after = Hash(path);
            var report = new
            {
                source = path, sha256Before = before, sha256After = after,
                raw.Version, width = raw.Cols, height = raw.Rows, raw.Channels, raw.Bpp, exposure = raw.Exp,
                elapsedMilliseconds = clock.Elapsed.TotalMilliseconds, parameters,
                measurementMethod = "source-resolution per-arm profile threshold crossings; median across common sample intervals",
                acceptanceBoundary = "Unlabelled field image: no accuracy, repeatability, detection-rate or product pass/fail ground truth. Saturated profiles can bias threshold edges.",
                metrics, points = table.Rows,
            };
            File.WriteAllText(Path.Combine(directory, "measurements.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            File.WriteAllText(Path.Combine(directory, "profile-quality.json"), JsonSerializer.Serialize(
                result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-profile-quality")!.Rows, new JsonSerializerOptions { WriteIndented = true }));
            string[] columns = table.Columns.Select(c => c.Name).ToArray();
            static string Csv(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";
            File.WriteAllLines(Path.Combine(directory, "points.csv"), new[] { string.Join(",", columns) }
                .Concat(table.Rows.Select(row => string.Join(",", columns.Select(c => Csv(row[c].ValueKind == JsonValueKind.Null ? "" : row[c].ToString()))))));
            ExportCrossEvidence(directory, raw, result, table);
            Assert.Equal(before, after);
            Assert.Equal(9, table.Rows.Count);
            // This fixture is an unlabelled measurement path, not a golden accuracy test.
            // Detection/invalid counts remain in the report even if the specimen is rejected.
        }
    }

    private static void ExportCrossEvidence(string directory, CVCIEFile raw, AlgorithmResult result, AlgorithmTableArtifact table)
    {
        var previews = result.Artifacts.OfType<AlgorithmImageArtifact>().ToArray();
        var channelMats = new List<Mat>();
        try
        {
            foreach (var preview in previews)
            {
                var mat = Mat.FromPixelData(preview.Image.Height, preview.Image.Width, MatType.CV_8UC1, preview.Image.Data.ToArray());
                channelMats.Add(mat);
                Cv2.ImWrite(Path.Combine(directory, preview.Name + ".png"), mat);
            }
            using var overview = new Mat();
            Cv2.Merge(channelMats.AsEnumerable().Reverse().ToArray(), overview);
            double step = double.Parse(previews[0].Metadata!["sourcePixelsPerPreviewPixel"], CultureInfo.InvariantCulture);
            using var source = Mat.FromPixelData(raw.Rows, raw.Cols, MatType.CV_16UC3, raw.Data);
            using var montage = new Mat(3 * 400, 3 * 800, MatType.CV_8UC3, Scalar.Black);
            var geometries = result.Artifacts.OfType<AlgorithmGeometryArtifact>().SelectMany(a => a.Geometries).ToArray();
            foreach (var row in table.Rows)
            {
                int x = row["roiX_px"].GetInt32(), y = row["roiY_px"].GetInt32();
                int width = row["roiWidth_px"].GetInt32(), height = row["roiHeight_px"].GetInt32();
                if (width == 0 || height == 0) continue;
                string point = row["point"].GetString()!;
                Cv2.Rectangle(overview, new Rect((int)(x / step), (int)(y / step), Math.Max(1, (int)(width / step)), Math.Max(1, (int)(height / step))), Scalar.White);
                Cv2.PutText(overview, point, new Point((int)(x / step), (int)(y / step) - 3), HersheyFonts.HersheySimplex, 0.5, Scalar.White);
                using var crop = new Mat(source, new Rect(x, y, width, height));
                using var original = new Mat(); crop.ConvertTo(original, MatType.CV_8UC3, 1.0 / 257);
                using var annotated = original.Clone();
                foreach (var geometry in geometries.Where(g => g.Id.StartsWith(point + "-", StringComparison.Ordinal) && (g.Id.EndsWith("horizontal") || g.Id.EndsWith("vertical"))))
                {
                    Scalar color = geometry.Id.Contains("-R-") ? new Scalar(0, 0, 255) : geometry.Id.Contains("-G-") ? new Scalar(0, 255, 0) : new Scalar(255, 100, 0);
                    var a = geometry.Points[0]; var b = geometry.Points[1];
                    Cv2.Rectangle(annotated, new Point((int)Math.Round((a.X - x) * 16), (int)Math.Round((a.Y - y) * 16)),
                        new Point((int)Math.Round((b.X - x) * 16), (int)Math.Round((b.Y - y) * 16)), color, 1, LineTypes.AntiAlias, 4);
                }
                Cv2.ImWrite(Path.Combine(directory, point + "-source.png"), original);
                Cv2.ImWrite(Path.Combine(directory, point + "-overlay.png"), annotated);
                int r = row["row"].GetInt32() - 1, c = row["column"].GetInt32() - 1;
                using var left = new Mat(montage, new Rect(c * 800, r * 400, 400, 400));
                using var right = new Mat(montage, new Rect(c * 800 + 400, r * 400, 400, 400));
                Cv2.Resize(original, left, new Size(400, 400)); Cv2.Resize(annotated, right, new Size(400, 400));
                Cv2.PutText(left, point + " source", new Point(8, 22), HersheyFonts.HersheySimplex, 0.5, Scalar.White);
                Cv2.PutText(right, point + " " + row["result"].GetString(), new Point(8, 22), HersheyFonts.HersheySimplex, 0.5, Scalar.White);
            }
            Cv2.ImWrite(Path.Combine(directory, "array-overview.png"), overview);
            Cv2.ImWrite(Path.Combine(directory, "nine-point-comparison.png"), montage);
        }
        finally { foreach (var mat in channelMats) mat.Dispose(); }
    }
}
