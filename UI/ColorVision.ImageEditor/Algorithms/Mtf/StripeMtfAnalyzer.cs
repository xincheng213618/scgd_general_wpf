using ColorVision.Core;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor.Algorithms.Mtf;

/// <summary>ROI-only stripe contrast analysis. Borrows input pixels; never normalizes, resizes or modifies measurement pixels.</summary>
public static class StripeMtfAnalyzer
{
    public const string FormulaVersion = "StripeMtf.TrimmedHistogram/1";

    public static JObject Calculate(HImage image, StripeMtfParameters options, IReadOnlyList<MtfRoi> regions)
    {
        options = StripeMtfParameters.FromJson(options.ToJson().ToString());
        ValidateRegions(regions, image.cols, image.rows);
        if (image.pData == IntPtr.Zero || image.depth is not (8 or 16) || image.channels is not (1 or 3)
            || image.stride < checked(image.cols * image.channels * (image.depth / 8)))
            throw new ArgumentException("MTF 需要 8/16 位、1/3 通道原始图像，不接受伪彩色或浮点显示图。");
        using Mat source = Mat.FromPixelData(image.rows, image.cols, MatType.MakeType(image.depth == 8 ? 0 : 2, image.channels), image.pData, image.stride);
        JArray rectangles = new(), groups = new();
        int[] histogram = ArrayPool<int>.Shared.Rent(image.depth == 8 ? 256 : 65536);
        try
        {
            using CLAHE? clahe = options.IsFourPart ? Cv2.CreateCLAHE(2, new Size(8, 8)) : null;
            using Mat? kernel = options.IsFourPart ? Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(15, 15)) : null;
            foreach (MtfRoi region in regions)
            {
                using Mat input = new(source, new Rect(region.x, region.y, region.w, region.h));
                using Mat gray = new();
                if (image.channels == 3) Cv2.CvtColor(input, gray, ColorConversionCodes.BGR2GRAY);
                // A one-channel ROI remains a borrowed view; only color ROIs need a gray buffer.
                Mat measurement = image.channels == 1 ? input : gray;
                Rect[] localRects = options.IsFourPart ? Locate(measurement, options, clahe!, kernel!) : [new(0, 0, region.w, region.h)];
                JArray children = new();
                double[] values = new double[localRects.Length];
                for (int id = 0; id < localRects.Length; id++)
                {
                    Rect rect = localRects[id];
                    if (rect.X < 0 || rect.Y < 0 || (long)rect.X + rect.Width > region.w || (long)rect.Y + rect.Height > region.h)
                        throw new InvalidOperationException($"MTF 小框超出搜索区域：{region.name}。请调整搜索框或四部参数。");
                    using Mat sample = new(measurement, rect);
                    values[id] = Contrast(sample, options, histogram) * (options.PercentageDisplay ? 100 : 1);
                    JObject item = new() { ["name"] = region.name, ["x"] = region.x + rect.X, ["y"] = region.y + rect.Y,
                        ["w"] = rect.Width, ["h"] = rect.Height, ["mtfValue"] = values[id] };
                    if (options.IsFourPart) item["id"] = id;
                    rectangles.Add(item);
                    JObject child = (JObject)item.DeepClone(); child.Remove("name"); children.Add(child);
                }
                if (options.IsFourPart)
                {
                    int horizontal = options.FirstIsHorizontal ? 0 : 1, vertical = 1 - horizontal;
                    groups.Add(new JObject { ["name"] = region.name, ["childRects"] = children,
                        ["horizontalAverage"] = (values[horizontal] + values[horizontal + 2]) / 2,
                        ["verticalAverage"] = (values[vertical] + values[vertical + 2]) / 2, ["Average"] = values.Average() });
                }
            }
        }
        finally { ArrayPool<int>.Shared.Return(histogram); }
        JObject result = new() { ["resultType"] = (int)options.Pattern, ["result"] = rectangles };
        if (options.IsFourPart) result["resultChild"] = groups;
        return result;
    }

    public static void ValidateRegions(IReadOnlyList<MtfRoi> regions, int width, int height)
    {
        if (width <= 0 || height <= 0 || regions.Count is < 1 or > 256) throw new ArgumentException("MTF 需要 1～256 个测量区域及有效图像尺寸。");
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (MtfRoi r in regions)
            if (r == null || string.IsNullOrWhiteSpace(r.name) || !names.Add(r.name) || r.x < 0 || r.y < 0 || r.w <= 0 || r.h <= 0
                || (long)r.x + r.w > width || (long)r.y + r.h > height || (long)r.w * r.h > int.MaxValue)
                throw new ArgumentException("MTF 区域名称须非空且唯一，矩形须完全在图内，像素数不得超过 Int32 范围。");
    }

    private static Rect[] Locate(Mat gray, StripeMtfParameters options, CLAHE clahe, Mat kernel)
    {
        // Smooth only the locator. Contrast always uses the original gray pixels.
        using Mat locator = new(), enhanced = new(), mask = new(), binary = new(), closed = new();
        Cv2.GaussianBlur(gray, locator, new Size(5, 5), 0, 0, BorderTypes.Reflect101 | BorderTypes.Isolated);
        Size imageSize = gray.Size();
        Point[]? FindTarget()
        {
            mask.ConvertTo(binary, MatType.CV_8U);
            Cv2.MorphologyEx(binary, closed, MorphTypes.Close, kernel);
            Cv2.FindContours(closed, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            return contours.Where(c => Cv2.ContourArea(c) >= options.MinimumArea && CanSampleCompletePattern(c, imageSize, options))
                .OrderByDescending(c => Cv2.ContourArea(c)).FirstOrDefault();
        }

        Point[]? target = null;
        if (gray.Depth() != MatType.CV_8U || options.Threshold <= 255)
        {
            clahe.Apply(locator, enhanced);
            Cv2.Threshold(enhanced, mask, options.Threshold, 255, ThresholdTypes.Binary);
            target = FindTarget();
        }
        if (target == null)
        {
            // A fixed threshold may miss dim signals, while CLAHE can amplify background into a border-connected halo.
            // Estimate a threshold on unenhanced pixels; this also handles an 8-bit input with the 16-bit default settings.
            Cv2.Threshold(locator, mask, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            target = FindTarget();
        }
        if (target == null) throw new InvalidOperationException("未定位到完整四部条纹图案。请框住一整组图案，并在四周留出少量边距。");
        Moments moments = Cv2.Moments(target);
        if (moments.M00 <= 0) throw new InvalidOperationException("四部条纹定位区域退化。");
        double cx = moments.M10 / moments.M00 + options.OffsetX, cy = moments.M01 / moments.M00 + options.OffsetY;
        double distance = options.DistanceToRect / Math.Sqrt(2);
        return new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }.Select(sign =>
            new Rect(checked((int)(cx + sign.Item1 * distance - options.RectWidth / 2.0)),
                checked((int)(cy + sign.Item2 * distance - options.RectHeight / 2.0)), options.RectWidth, options.RectHeight)).ToArray();
    }

    private static bool CanSampleCompletePattern(Point[] contour, Size imageSize, StripeMtfParameters options)
    {
        Rect bounds = Cv2.BoundingRect(contour);
        if (bounds.X == 0 || bounds.Y == 0 || bounds.Right >= imageSize.Width || bounds.Bottom >= imageSize.Height) return false;
        Moments moments = Cv2.Moments(contour);
        if (moments.M00 <= 0) return false;
        double cx = moments.M10 / moments.M00 + options.OffsetX, cy = moments.M01 / moments.M00 + options.OffsetY;
        double distance = options.DistanceToRect / Math.Sqrt(2);
        // A single detached quadrant or a small bright fragment must not become a four-part target.
        return cx - distance - options.RectWidth / 2.0 >= bounds.Left && cx + distance + options.RectWidth / 2.0 <= bounds.Right
            && cy - distance - options.RectHeight / 2.0 >= bounds.Top && cy + distance + options.RectHeight / 2.0 <= bounds.Bottom;
    }

    private static unsafe double Contrast(Mat image, StripeMtfParameters options, int[] histogram)
    {
        int bins = image.Depth() == MatType.CV_8U ? 256 : 65536;
        Array.Clear(histogram, 0, bins);
        int min = bins - 1, max = 0;
        int rows = image.Rows, cols = image.Cols;
        byte* data = (byte*)image.Data;
        long stride = image.Step();
        for (int y = 0; y < rows; y++)
        {
            byte* row = data + y * stride;
            for (int x = 0; x < cols; x++)
            {
                int value = bins == 256 ? row[x] : ((ushort*)row)[x];
                histogram[value]++; min = Math.Min(min, value); max = Math.Max(max, value);
            }
        }
        int count = checked(rows * cols);
        int take = options.Method == StripeMtfMethod.TailMean ? Math.Max(1, (int)(count * options.TailRatio)) : 1;
        int skipLow = (int)(count * options.BlackNoiseRatio), skipHigh = (int)(count * options.WhiteNoiseRatio);
        if (2L * take + skipLow + skipHigh > count) throw new InvalidOperationException("MTF 区域太小，无法取得互不重叠的明暗样本。");
        double low = TailMean(histogram, min, max, 1, skipLow, take), high = TailMean(histogram, max, min, -1, skipHigh, take);
        if (high + low <= 0) throw new InvalidOperationException("MTF 区域没有有效亮度信号。");
        return (high - low) / (high + low);
    }

    private static double TailMean(int[] histogram, int start, int end, int step, int skip, int take)
    {
        long sum = 0; int remaining = take;
        for (int value = start; step > 0 ? value <= end : value >= end; value += step)
        {
            int available = histogram[value];
            int discarded = Math.Min(skip, available); skip -= discarded; available -= discarded;
            int used = Math.Min(remaining, available); sum += (long)value * used; remaining -= used;
            if (remaining == 0) return (double)sum / take;
        }
        throw new InvalidOperationException("MTF 明暗取样不足。");
    }
}
