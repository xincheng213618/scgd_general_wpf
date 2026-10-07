using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms;

public sealed partial class DisplayMetrologyProvider
{
    private static void MeasureDust(AlgorithmExecutionContext context, DustDetectionParameters p, List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        AlgorithmImageBuffer image = context.Inputs[0].Image;
        using AlgorithmImageMatLease source = AlgorithmImageInterop.BorrowReadOnly(image);
        if (image.Format.BytesPerPixel() / image.Format.Channels() == 4
            && !Cv2.CheckRange(source.Mat, true, out _, 0, Math.BitIncrement(1.0)))
            throw new MeasurementException("invalid_signal", "浮点输入必须为有限的 [0,1] 相对信号。");
        double reduction = Math.Min(1, (double)p.MaximumAnalysisDimension / Math.Max(image.Width, image.Height));
        int width = Math.Max(1, (int)Math.Round(image.Width * reduction));
        int height = Math.Max(1, (int)Math.Round(image.Height * reduction));
        if (Math.Min(width, height) <= p.BorderMargin * 2 + 8)
            throw new MeasurementException("analysis_region_too_small", "内缩后的分析区域太小，请减小边界内缩或增加分析分辨率。");
        using Mat resized = new();
        Cv2.Resize(source.Mat, resized, new Size(width, height), interpolation: InterpolationFlags.Area);
        token.ThrowIfCancellationRequested();
        using Mat channel = new();
        Cv2.ExtractChannel(resized, channel, image.Format.Channels() == 1 ? 0 : p.Channel);
        using Mat signal = new();
        int channelBytes = image.Format.BytesPerPixel() / image.Format.Channels();
        channel.ConvertTo(signal, MatType.CV_32FC1, channelBytes == 1 ? 1.0 / 255 : channelBytes == 2 ? 1.0 / 65535 : 1);
        if (p.DecodeExponent != 1) Cv2.Pow(signal, p.DecodeExponent, signal);
        using Mat smoothed = new();
        Cv2.GaussianBlur(signal, smoothed, new Size(0, 0), 0.8, borderType: BorderTypes.Reflect101);
        using Mat mask = BuildDustAnalysisMask(signal, p, token, out double foregroundThreshold);
        int analyzedPixels = Cv2.CountNonZero(mask);
        if (analyzedPixels < 64)
            throw new MeasurementException("no_analysis_region", "没有足够的有效成像区域，请检查成像阈值与边界内缩。");
        context.Progress?.Report(new AlgorithmProgress(0.2, "dust.background", "分析成像区域内的暗斑"));
        using Mat score = new(height, width, MatType.CV_32FC1, Scalar.All(0));
        foreach (int kernel in new[] { 31, 121, p.BackgroundKernel }.Distinct())
        {
            token.ThrowIfCancellationRequested();
            using Mat background = new();
            using Mat delta = new();
            using Mat denominator = new();
            Cv2.GaussianBlur(smoothed, background, new Size(kernel, kernel), 0, borderType: BorderTypes.Reflect101);
            Cv2.Subtract(background, smoothed, delta);
            Cv2.Max(background, 1e-6, denominator);
            Cv2.Divide(delta, denominator, delta);
            Cv2.Max(score, delta, score);
        }
        using Mat candidateMask = new();
        Cv2.Threshold(score, candidateMask, p.ContrastPercent / 100, 255, ThresholdTypes.Binary);
        candidateMask.ConvertTo(candidateMask, MatType.CV_8UC1);
        // Mask BEFORE components: a dark optical rim must not merge with/suppress interior dust.
        Cv2.BitwiseAnd(candidateMask, mask, candidateMask);
        using Mat opening = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
        Cv2.MorphologyEx(candidateMask, candidateMask, MorphTypes.Open, opening);
        using Mat labels = new();
        using Mat stats = new();
        using Mat centers = new();
        int count = Cv2.ConnectedComponentsWithStats(candidateMask, labels, stats, centers);
        token.ThrowIfCancellationRequested();
        if (count > 100_001) throw new MeasurementException("component_budget_exceeded", "噪声候选超过 100000，请提高对比度阈值或检查图像。");
        double scaleX = (double)image.Width / width, scaleY = (double)image.Height / height;
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var geometry = new List<AlgorithmGeometry>();
        var overlays = new List<AlgorithmOverlayItem>();
        using Mat acceptedMask = new(height, width, MatType.CV_8UC1, Scalar.All(0));
        int rejectedBoundary = 0;
        for (int label = 1; label < count; label++)
        {
            token.ThrowIfCancellationRequested();
            int area = stats.At<int>(label, 4);
            if (area < p.MinimumArea || area > p.MaximumArea) continue;
            var box = new Rect(stats.At<int>(label, 0), stats.At<int>(label, 1), stats.At<int>(label, 2), stats.At<int>(label, 3));
            var padded = new Rect(Math.Max(0, box.X - 1), Math.Max(0, box.Y - 1), 0, 0);
            padded.Width = Math.Min(width, box.Right + 1) - padded.X;
            padded.Height = Math.Min(height, box.Bottom + 1) - padded.Y;
            using Mat boundary = new(mask, padded);
            if (Cv2.CountNonZero(boundary) != padded.Width * padded.Height) { rejectedBoundary++; continue; }
            if (rows.Count == MaximumComponents)
                throw new MeasurementException("defect_budget_exceeded", "候选超过 2048，请提高阈值；未将截断结果报告成成功。");
            using Mat localLabels = new(labels, box);
            using Mat localScore = new(score, box);
            using Mat component = new();
            Cv2.Compare(localLabels, label, component, CmpTypes.EQ);
            Cv2.MinMaxLoc(localScore, out _, out double peak, out _, out _, component);
            using Mat acceptedRoi = new(acceptedMask, box);
            Cv2.BitwiseOr(acceptedRoi, component, acceptedRoi);
            string id = $"dust-{rows.Count + 1}";
            double x = box.X * scaleX, y = box.Y * scaleY, w = box.Width * scaleX, h = box.Height * scaleY;
            rows.Add(Row(("编号", rows.Count + 1), ("原图X_px", x), ("原图Y_px", y), ("原图宽_px", w), ("原图高_px", h),
                ("分析面积_px2", area), ("原图等效面积_px2", area * scaleX * scaleY), ("最大暗差_%", peak * 100)));
            geometry.Add(new AlgorithmGeometry(id, AlgorithmGeometryKind.Rectangle, [new(x, y), new(x + w, y + h)]));
            overlays.Add(new AlgorithmOverlayItem(id, new AlgorithmOverlayStyle("#FFFF4545", Label: $"{rows.Count}")));
        }
        Cv2.FindContours(mask, out Point[][] borders, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        foreach (Point[] border in borders)
        {
            string id = $"dust-boundary-{geometry.Count}";
            Point[] simplified = Cv2.ApproxPolyDP(border, 1.5, true);
            geometry.Add(new AlgorithmGeometry(id, AlgorithmGeometryKind.Polygon, simplified.Select(v => new AlgorithmPoint(v.X * scaleX, v.Y * scaleY)).ToArray()));
            overlays.Add(new AlgorithmOverlayItem(id, new AlgorithmOverlayStyle("#FF20C8E6")));
        }
        Table(artifacts, "灰尘候选", ["编号", "原图X_px", "原图Y_px", "原图宽_px", "原图高_px", "分析面积_px2", "原图等效面积_px2", "最大暗差_%"], rows);
        Metrics(artifacts, ("灰尘候选数量", rows.Count, "count"), ("有效检测范围", 100.0 * analyzedPixels / (width * height), "%"),
            ("边界截断候选数", rejectedBoundary, "count"), ("分析图宽", width, "px"), ("分析图高", height, "px"));
        var metadata = new Dictionary<string, string> { ["sourcePixelsPerAnalysisPixelX"] = scaleX.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            ["sourcePixelsPerAnalysisPixelY"] = scaleY.ToString("R", System.Globalization.CultureInfo.InvariantCulture) };
        artifacts.Add(new AlgorithmImageArtifact("有效成像区域", "visualization", AlgorithmImageInterop.FromMat(mask), metadata));
        artifacts.Add(new AlgorithmImageArtifact("灰尘候选掩膜", "visualization", AlgorithmImageInterop.FromMat(acceptedMask), metadata));
        artifacts.Add(new AlgorithmGeometryArtifact("dust-regions", AlgorithmCoordinateSpace.Pixel, geometry));
        artifacts.Add(new AlgorithmOverlayArtifact("dust-candidates", AlgorithmOverlayLifetime.Transient, overlays));
        artifacts.Add(new AlgorithmStructuredDataArtifact("dust-analysis", "colorvision.dust-detection/v1", AlgorithmJson.ToElement(new
        {
            sourceWidth = image.Width, sourceHeight = image.Height, analysisWidth = width, analysisHeight = height,
            sourcePixelsPerAnalysisPixelX = scaleX, sourcePixelsPerAnalysisPixelY = scaleY, foregroundThreshold,
            parameters = p, candidateCount = rows.Count, rejectedBoundary,
            note = "仅检测有效成像区内的暗斑候选；小于分析分辨率、过宽或过浅的污斑及排除边缘可能漏检；不判定灰尘成因或产品合格。",
        })));
    }

    private static Mat BuildDustAnalysisMask(Mat signal, DustDetectionParameters p, CancellationToken token, out double threshold)
    {
        using Mat blurred = new();
        Cv2.GaussianBlur(signal, blurred, new Size(0, 0), 3, borderType: BorderTypes.Reflect101);
        var samples = new List<float>();
        int height = blurred.Rows, width = blurred.Cols;
        for (int y = 0; y < height; y += 8)
        {
            token.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x += 8) samples.Add(blurred.At<float>(y, x));
        }
        samples.Sort();
        double reference = samples[(int)((samples.Count - 1) * 0.95)];
        if (reference < 1e-6) throw new MeasurementException("no_illuminated_region", "图像没有足够的成像信号。");
        threshold = reference * p.ForegroundPercent / 100;
        using Mat foreground = new();
        Cv2.Threshold(blurred, foreground, threshold, 255, ThresholdTypes.Binary);
        foreground.ConvertTo(foreground, MatType.CV_8UC1);
        Cv2.FindContours(foreground, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (contours.Length == 0) throw new MeasurementException("no_illuminated_region", "未找到成像区域。");
        Point[] largest = contours.MaxBy(c => Cv2.ContourArea(c))!;
        using Mat filled = new(signal.Rows, signal.Cols, MatType.CV_8UC1, Scalar.All(0));
        Cv2.DrawContours(filled, new[] { largest }, -1, Scalar.All(255), -1);
        using Mat padded = new();
        Cv2.CopyMakeBorder(filled, padded, 1, 1, 1, 1, BorderTypes.Constant, Scalar.All(0));
        using Mat distance = new();
        Cv2.DistanceTransform(padded, distance, DistanceTypes.L2, DistanceTransformMasks.Mask5);
        using Mat interior = new(distance, new Rect(1, 1, signal.Cols, signal.Rows));
        Mat mask = new();
        try
        {
            Cv2.Threshold(interior, mask, p.BorderMargin, 255, ThresholdTypes.Binary);
            mask.ConvertTo(mask, MatType.CV_8UC1);
            return mask;
        }
        catch { mask.Dispose(); throw; }
    }
}
