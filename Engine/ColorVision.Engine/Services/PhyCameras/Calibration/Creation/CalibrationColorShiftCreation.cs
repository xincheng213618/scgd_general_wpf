using ColorVision.FileIO;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Creation;

public sealed record CalibrationChannelPoints(string ImageId, Point2f[] Red, Point2f[] Green, Point2f[] Blue);
public sealed record CalibrationShiftError(double RmsPixels, double MaximumPixels, int PointCount);
public sealed record CalibrationColorShiftFit(JObject Json, CalibrationShiftError Training, CalibrationShiftError Validation, CalibrationShiftError BeforeValidation);

/// <summary>Fits integer channel translations to G. The last complete image is held out.</summary>
public static class CalibrationColorShiftCreation
{
    public static CalibrationColorShiftFit Fit(IReadOnlyList<string> paths, int columns, int rows, double maximumResidualPixels, bool interleavedBgr, CancellationToken token = default, IProgress<string>? progress = null)
    {
        // Opposite parity and a rectangular board remove the 90-degree index ambiguity.
        if (columns < 3 || rows < 3 || columns > 100 || rows > 100 || columns % 2 == rows % 2)
            throw new ArgumentException("棋盘内角点行列须为 3~100，且一奇一偶（例如 9,6），以避免通道匹配方向歧义。");
        ValidateLimit(maximumResidualPixels);
        if (paths.Count < 3) throw new InvalidDataException("至少导入三张独立棋盘 CVRAW；最后一张只用于验证。");
        List<CalibrationChannelPoints> frames = new();
        HashSet<string> hashes = new(StringComparer.Ordinal);
        JArray sources = new();
        int width = 0, height = 0, bits = 0;
        float? gain = null;
        float[]? exposure = null;
        foreach (string path in paths)
        {
            token.ThrowIfCancellationRequested();
            using (FileStream stream = File.OpenRead(path))
            {
                string hash = Convert.ToHexString(SHA256.HashData(stream));
                if (!hashes.Add(hash)) throw new InvalidDataException("输入含内容完全相同的文件；请使用独立拍摄的拟合和验证图像。");
                sources.Add(new JObject { ["file"] = Path.GetFileName(path), ["sha256"] = hash });
            }
            CalibrationRawAverage image = CalibrationMapCreation.Average(new[] { path }, token, interleavedBgr: interleavedBgr);
            if (image.Channels != 3) throw new InvalidDataException("色偏标定需要三通道 CVRAW。");
            if (frames.Count > 0 && (image.Width != width || image.Height != height || image.BitsPerChannel != bits))
                throw new InvalidDataException("色偏样本的尺寸和位深必须一致。");
            width = image.Width; height = image.Height; bits = image.BitsPerChannel;
            if (CVFileUtil.ReadCIEFileHeader(path, out CVCIEFile header) < 0) throw new InvalidDataException("CVRAW 头无效。");
            using (header)
            {
                if (gain.HasValue && (gain.Value != header.Gain || !exposure!.SequenceEqual(header.Exp)))
                    throw new InvalidDataException("色偏样本须使用相同增益和逐通道曝光。");
                gain = header.Gain; exposure = (float[])header.Exp.Clone();
            }
            Point2f[][] points = new Point2f[3][];
            int planeSize = checked(width * height);
            for (int channel = 0; channel < 3; channel++)
            {
                token.ThrowIfCancellationRequested();
                float[] pixels = new float[planeSize];
                for (int i = 0; i < planeSize; i++) pixels[i] = (float)image.Values[channel * planeSize + i];
                using Mat source = new(height, width, MatType.CV_32FC1);
                Marshal.Copy(pixels, 0, source.Data, pixels.Length);
                using Mat gray = new();
                Cv2.Normalize(source, gray, 0, 255, NormTypes.MinMax, (int)MatType.CV_8UC1);
                if (!Cv2.FindChessboardCorners(gray, new Size(columns, rows), out points[channel], ChessboardFlags.AdaptiveThresh | ChessboardFlags.NormalizeImage))
                    throw new InvalidDataException($"{Path.GetFileName(path)} 的 {"RGB"[channel]} 通道未找到完整棋盘；请检查响应、清晰度和遮挡。");
                Cv2.CornerSubPix(gray, points[channel], new Size(5, 5), new Size(-1, -1), new TermCriteria(CriteriaTypes.Eps | CriteriaTypes.Count, 40, 0.001));
            }
            AlignToGreen(points[0], points[1], columns);
            AlignToGreen(points[2], points[1], columns);
            frames.Add(new(Path.GetFullPath(path), points[0], points[1], points[2]));
            progress?.Report($"已匹配 RGB 棋盘 {frames.Count}/{paths.Count}；最后一张保留验证。");
        }
        token.ThrowIfCancellationRequested();
        CalibrationColorShiftFit result = FitPoints(frames, width, height, maximumResidualPixels);
        JObject provenance = (JObject)result.Json["colorvision.creation"]!;
        provenance["sources"] = sources;
        provenance["input_layout"] = interleavedBgr ? "interleaved_bgr" : "planar_rgb";
        provenance["bits"] = bits;
        provenance["board_columns"] = columns; provenance["board_rows"] = rows;
        return result;
    }

    public static CalibrationColorShiftFit FitPoints(IReadOnlyList<CalibrationChannelPoints> frames, int width, int height, double maximumResidualPixels)
    {
        ValidateLimit(maximumResidualPixels);
        if (width < 2 || height < 2 || frames.Count < 3) throw new InvalidDataException("需要有效图像尺寸、至少两张拟合图和一张独立验证图。");
        HashSet<string> identifiers = new(StringComparer.OrdinalIgnoreCase);
        foreach (CalibrationChannelPoints frame in frames)
        {
            if (string.IsNullOrWhiteSpace(frame.ImageId) || !identifiers.Add(frame.ImageId)) throw new InvalidDataException("每张拟合和验证图必须有独立标识。");
            if (frame.Green.Length < 6 || frame.Red.Length != frame.Green.Length || frame.Blue.Length != frame.Green.Length)
                throw new InvalidDataException("每张图至少需要六组一一对应的 RGB 点。");
            foreach (Point2f[] channel in new[] { frame.Red, frame.Green, frame.Blue })
            {
                foreach (Point2f p in channel)
                    if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || p.X < 0 || p.Y < 0 || p.X >= width || p.Y >= height)
                        throw new InvalidDataException("特征点必须是图像范围内的有限坐标。");
            }
            // A line or repeated point cannot validate a two-dimensional target.
            Point2f origin = frame.Green[0];
            Point2f axis = frame.Green.MaxBy(p => DistanceSquared(p, origin));
            if (!frame.Green.Any(p => Math.Abs((axis.X - origin.X) * (double)(p.Y - origin.Y) - (axis.Y - origin.Y) * (double)(p.X - origin.X)) > 1e-6))
                throw new InvalidDataException("参考点退化为直线或重复位置。");
        }
        CalibrationChannelPoints[] training = frames.Take(frames.Count - 1).ToArray();
        CalibrationChannelPoints[] validation = { frames[^1] };
        Point red = Estimate(training, true), blue = Estimate(training, false);
        if (Math.Abs(red.X) >= width || Math.Abs(blue.X) >= width || Math.Abs(red.Y) >= height || Math.Abs(blue.Y) >= height)
            throw new InvalidDataException("拟合平移超出图像范围。");
        CalibrationShiftError trainingError = Measure(training, red, blue);
        CalibrationShiftError validationError = Measure(validation, red, blue);
        CalibrationShiftError before = Measure(validation, default, default);
        if (trainingError.MaximumPixels > maximumResidualPixels || validationError.MaximumPixels > maximumResidualPixels)
            throw new InvalidDataException($"整数平移不满足设定的最大残差 {maximumResidualPixels:F3} 像素：拟合 {trainingError.MaximumPixels:F3}，独立验证 {validationError.MaximumPixels:F3}。请检查错配或径向色差；未丢弃异常点，未生成文件。");
        if (validationError.RmsPixels > before.RmsPixels)
            throw new InvalidDataException("整数平移使独立验证图的 RMS 误差增大，未生成文件；请检查通道偏移是否随图像条件变化。");
        JObject json = new()
        {
            ["fillOffset"] = false,
            ["offset"] = new JArray(Offset(blue), Offset(default), Offset(red)),
            ["colorvision.creation"] = new JObject
            {
                ["method"] = "chessboard_integer_translation_to_green", ["width"] = width, ["height"] = height,
                ["training_images"] = training.Length, ["validation_image"] = Path.GetFileName(frames[^1].ImageId),
                ["maximum_residual_limit_pixels"] = maximumResidualPixels,
                ["training_rms_pixels"] = trainingError.RmsPixels, ["training_max_pixels"] = trainingError.MaximumPixels,
                ["validation_rms_pixels"] = validationError.RmsPixels, ["validation_max_pixels"] = validationError.MaximumPixels,
                ["validation_before_rms_pixels"] = before.RmsPixels,
                ["training_points"] = trainingError.PointCount, ["validation_points"] = validationError.PointCount
            }
        };
        return new(json, trainingError, validationError, before);
    }

    private static JObject Offset(Point point) => new() { ["X"] = point.X, ["Y"] = point.Y };

    private static void ValidateLimit(double value)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value), "最大允许残差须为正的有限像素数。");
    }

    private static Point Estimate(IEnumerable<CalibrationChannelPoints> frames, bool red)
    {
        double x = 0, y = 0; int count = 0;
        foreach (CalibrationChannelPoints frame in frames)
        {
            Point2f[] source = red ? frame.Red : frame.Blue;
            for (int i = 0; i < source.Length; i++) { x += frame.Green[i].X - source[i].X; y += frame.Green[i].Y - source[i].Y; count++; }
        }
        // Nearest integer to the mean minimizes the squared point residual over integer translations.
        return new((int)Math.Round(x / count, MidpointRounding.AwayFromZero), (int)Math.Round(y / count, MidpointRounding.AwayFromZero));
    }

    private static CalibrationShiftError Measure(IEnumerable<CalibrationChannelPoints> frames, Point red, Point blue)
    {
        double sum = 0, maximum = 0; int count = 0;
        foreach (CalibrationChannelPoints frame in frames)
        {
            foreach ((Point2f[] source, Point offset) in new[] { (frame.Red, red), (frame.Blue, blue) })
                for (int i = 0; i < source.Length; i++)
                {
                    double dx = source[i].X + offset.X - frame.Green[i].X, dy = source[i].Y + offset.Y - frame.Green[i].Y;
                    double error = dx * dx + dy * dy;
                    maximum = Math.Max(maximum, error); sum += error; count++;
                }
        }
        return new(Math.Sqrt(sum / count), Math.Sqrt(maximum), count);
    }

    private static void AlignToGreen(Point2f[] channel, Point2f[] green, int columns)
    {
        double direct = 0, reverse = 0;
        for (int i = 0; i < green.Length; i++)
        {
            direct += DistanceSquared(channel[i], green[i]);
            reverse += DistanceSquared(channel[^(i + 1)], green[i]);
        }
        if (reverse < direct) Array.Reverse(channel);
        double spacingSquared = double.PositiveInfinity;
        for (int i = 0; i < green.Length; i++)
        {
            if (i % columns != columns - 1) spacingSquared = Math.Min(spacingSquared, DistanceSquared(green[i], green[i + 1]));
            if (i + columns < green.Length) spacingSquared = Math.Min(spacingSquared, DistanceSquared(green[i], green[i + columns]));
        }
        // Require displacement below half the nearest grid spacing, so another grid index is not equally plausible.
        for (int i = 0; i < green.Length; i++)
            if (DistanceSquared(channel[i], green[i]) >= spacingSquared / 4)
                throw new InvalidDataException("通道偏移达到棋盘最小点距的一半，无法可靠确定对应点；请使用更大格距靶标。");
    }

    private static double DistanceSquared(Point2f a, Point2f b) => (a.X - b.X) * (double)(a.X - b.X) + (a.Y - b.Y) * (double)(a.Y - b.Y);
}
