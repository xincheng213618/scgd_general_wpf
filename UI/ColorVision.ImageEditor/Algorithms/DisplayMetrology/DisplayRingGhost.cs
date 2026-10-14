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
    private const int RingAngles = 720;
    private sealed record RingBand(int Peak, int Left, int Right, int LeftBase, int RightBase, double Prominence);
    private sealed record RingReading(RingBand Band, double Mean, double Background, double Coverage);

    // Dedicated complete, approximately concentric annulus measurement. Generic blob Ghost remains unchanged.
    private static void MeasureRingGhost(float[] signal, int width, int height, RingGhostMeasurementParameters p,
        List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        RotatedRect ellipse = LocatePrimaryRing(signal, width, height, token);
        double radius = (ellipse.Size.Width + ellipse.Size.Height) / 4.0;
        double step = Math.Max(1, radius / 1600);
        int length = (int)Math.Ceiling(radius * 1.3 / step) + 1;
        double[] polar = SampleRing(signal, width, height, ellipse, step, length, token);
        var median = new double[length];
        var mean = new double[length];
        var scratch = new double[RingAngles];
        for (int r = 0; r < length; r++)
        {
            for (int a = 0; a < RingAngles; a++) scratch[a] = polar[a * length + r];
            mean[r] = scratch.Average();
            median[r] = RingMedian(scratch);
        }
        double[] profile = SmoothRingProfile(median, Math.Max(1, radius * 0.0025 / step));
        double[] coarseProfile = SmoothRingProfile(median, Math.Max(2, radius * .01 / step));
        int main = Enumerable.Range((int)(radius * .85 / step), (int)(radius * .3 / step))
            .MaxBy(i => profile[i]);
        int window = Math.Max(6, (int)(radius * .09 / step));
        RingBand mainBand = FindRingBand(profile, main, Math.Max(window, (int)(radius * .2 / step)));
        var primary = ReadRingBand(polar, length, mainBand, 0);
        if (primary.Mean <= 1e-8 || mainBand.Prominence <= 1e-8 || primary.Coverage < .9)
            throw new MeasurementException("primary_ring_incomplete", "主亮环不完整或对比不足，不能建立可靠亮度参考。");
        // Check original pixels, including isolated saturated pixels missed by polar resampling.
        RejectSaturatedRing(signal, width, height, ellipse, mainBand, step, token);

        var differences = Enumerable.Range(1, Math.Max(2, (int)(main * .75)))
            .Select(i => Math.Abs(profile[i] - profile[i - 1])).ToArray();
        double noise = RingMedian(differences) / .67448975 / Math.Sqrt(2);
        double requiredProminence = Math.Max(primary.Mean * .0001, 6 * noise);
        int start = Math.Max(3, (int)(main * .15));
        int end = Math.Min((int)(main * .9), mainBand.Left - Math.Max(window, mainBand.Right - mainBand.Left));
        var found = new List<RingReading>();
        for (int r = start; r < end; r++)
        {
            token.ThrowIfCancellationRequested();
            if (profile[r] <= profile[r - 1] || profile[r] < profile[r + 1]) continue;
            RingBand band = FindRingBand(profile, r, window);
            if (band.Prominence < requiredProminence || band.Right - band.Left < 2
                || band.Left <= start || band.Right >= end) continue;
            // Quantized row noise can create tiny radial peaks. Require a matching peak after
            // averaging over 1% of the primary radius as well; this is a resolution boundary.
            int nearby = Math.Max(2, (int)(main * .03));
            int coarsePeak = Enumerable.Range(Math.Max(start, r - nearby), Math.Min(end, r + nearby) - Math.Max(start, r - nearby) + 1)
                .MaxBy(i => coarseProfile[i]);
            if (coarsePeak <= start || coarsePeak >= end || coarseProfile[coarsePeak] <= coarseProfile[coarsePeak - 1]
                || coarseProfile[coarsePeak] < coarseProfile[coarsePeak + 1]
                || FindRingBand(coarseProfile, coarsePeak, window).Prominence < requiredProminence) continue;
            var reading = ReadRingBand(polar, length, band, requiredProminence * .2);
            if (reading.Coverage >= 2.0 / 3 && reading.Mean > reading.Background) found.Add(reading);
        }
        // Broad peaks may contain several local maxima. Keep one measurement for overlapping bands.
        var accepted = new List<RingReading>();
        foreach (var reading in found.OrderByDescending(v => v.Band.Prominence))
        {
            if (accepted.Any(v => Math.Abs(v.Band.Peak - reading.Band.Peak) < main * .06
                || v.Band.Left <= reading.Band.Right && reading.Band.Left <= v.Band.Right)) continue;
            accepted.Add(reading);
        }
        if (accepted.Count > 8) throw new MeasurementException("ring_candidates_ambiguous", "径向峰过多，图案或背景不符合圆环鬼影测量条件。");
        accepted = accepted.OrderByDescending(v => v.Band.Peak).ToList();
        bool mapped = accepted.Count == 2 && p.OrderMapping != RingGhostOrderMapping.Unconfirmed;
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var shapes = new List<AlgorithmGeometry>();
        AddRingBoundary(shapes, "主像内边界", ellipse, mainBand.Left * step);
        AddRingBoundary(shapes, "主像外边界", ellipse, mainBand.Right * step);
        var warnings = new List<string>
        {
            "亮度比为原图区域平均信号比；只有输入为经校正的线性亮度信号时，才可解释为亮度比。",
            "局部背景扣除值用于观察弱环对比，不替代客户 GMR 测量口径；未作瑞淀数值等效或产品合格判定。",
            "只搜索主像内侧约 0.15～0.90 倍半径的近同心完整环；外侧、偏心或重叠鬼影需要另行评价。"
        };
        if (!mapped) warnings.Add("物理阶次未确认或未恰好分离出两个候选，GMR1/GMR2 留空；候选编号按半径从外到内排列，不代表阶次。");
        if (accepted.Count < 2) warnings.Add("未可靠分离出两个鬼影环，弱环可能与背景或光晕混合。未检出不等于 0，也不能判定通过。");
        for (int i = 0; i < accepted.Count; i++)
        {
            var v = accepted[i];
            int? order = mapped ? (p.OrderMapping == RingGhostOrderMapping.OuterIsFirst ? i + 1 : 2 - i) : null;
            double ratio = v.Mean / primary.Mean * 100;
            double contrast = (v.Mean - v.Background) / Math.Max(1e-12, primary.Mean - primary.Background) * 100;
            string label = order.HasValue ? $"{order}阶候选" : $"候选{(char)('A' + i)}";
            rows.Add(Row(("候选", label), ("阶次", order), ("平均信号比_%", ratio), ("扣局部背景对比_%", contrast),
                ("环半径_px", v.Band.Peak * step), ("内边界_px", v.Band.Left * step), ("外边界_px", v.Band.Right * step),
                ("环区平均信号", v.Mean), ("局部背景信号", v.Background), ("圆周支持率", v.Coverage)));
            AddRingBoundary(shapes, label + "内边界", ellipse, v.Band.Left * step);
            AddRingBoundary(shapes, label + "外边界", ellipse, v.Band.Right * step);
            if (order.HasValue) Metrics(artifacts, ($"GMR{order}_percent", ratio, "%"));
            Metrics(artifacts, ($"ring_candidate_{(char)('A' + i)}_percent", ratio, "%"));
        }
        Table(artifacts, "ring-ghost-candidates", ["候选", "阶次", "平均信号比_%", "扣局部背景对比_%", "环半径_px", "内边界_px", "外边界_px", "环区平均信号", "局部背景信号", "圆周支持率"], rows);
        Table(artifacts, "ring-ghost-quality", ["说明"], warnings.Select(w => (IReadOnlyDictionary<string, JsonElement>)Row(("说明", w))).ToList());
        Metrics(artifacts, ("ring_candidate_count", accepted.Count, "count"), ("ring_primary_radius", main * step, "px"),
            ("ring_primary_mean", primary.Mean, "relative-signal"), ("ring_detection_prominence", requiredProminence / primary.Mean * 100, "%"));
        var profiles = Enumerable.Range(0, length).Select(i => (IReadOnlyDictionary<string, JsonElement>)Row(
            ("半径_px", i * step), ("圆周中位信号", profile[i]), ("圆周平均信号", mean[i]))).ToList();
        Table(artifacts, "ring-radial-profile", ["半径_px", "圆周中位信号", "圆周平均信号"], profiles);
        artifacts.Add(new AlgorithmStructuredDataArtifact("ring-ghost-measurement", "colorvision.ring-ghost/v1", AlgorithmJson.ToElement(new
        {
            status = accepted.Count == 2 ? "two_candidates_resolved" : "unresolved_ring_count",
            physicalOrderConfirmed = mapped,
            gmr1Percent = mapped ? accepted[p.OrderMapping == RingGhostOrderMapping.OuterIsFirst ? 0 : 1].Mean / primary.Mean * 100 : (double?)null,
            gmr2Percent = mapped ? accepted[p.OrderMapping == RingGhostOrderMapping.OuterIsFirst ? 1 : 0].Mean / primary.Mean * 100 : (double?)null,
            productPass = (bool?)null,
            centerX = ellipse.Center.X, centerY = ellipse.Center.Y,
            axisWidth = ellipse.Size.Width, axisHeight = ellipse.Size.Height, angleDegrees = ellipse.Angle,
            primaryMean = primary.Mean, primaryBackground = primary.Background,
            primaryInnerRadius = mainBand.Left * step, primaryOuterRadius = mainBand.Right * step,
            radialStep = step, angularSamples = RingAngles,
            measurementRegion = "full-width-at-half-local-prominence annulus; area-weighted polar mean of original decoded signal",
            formula = "100 * ghostRegionMean / primaryRegionMean",
            warnings,
        })));
        artifacts.Add(new AlgorithmGeometryArtifact("display-regions", AlgorithmCoordinateSpace.Pixel, shapes));
        artifacts.Add(new AlgorithmOverlayArtifact("display-measurements", AlgorithmOverlayLifetime.Transient,
            shapes.Select((shape, index) => new AlgorithmOverlayItem(shape.Id, new AlgorithmOverlayStyle(
                Stroke: index < 2 ? "#FF7BF15C" : (index - 2) / 2 % 2 == 0 ? "#FFFFBA38" : "#FF4DE3FF", Label: shape.Id))).ToArray()));
    }

    private static RotatedRect LocatePrimaryRing(float[] signal, int width, int height, CancellationToken token)
    {
        double scale = Math.Min(1, 1600.0 / Math.Max(width, height));
        using var full = Mat.FromPixelData(height, width, MatType.CV_32FC1, signal);
        using var small = new Mat();
        Cv2.Resize(full, small, new Size(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale))), 0, 0, InterpolationFlags.Area);
        token.ThrowIfCancellationRequested();
        Cv2.MinMaxLoc(small, out double min, out double max);
        if (max - min < 1e-7) throw new MeasurementException("primary_ring_missing", "图像没有足够的亮环对比。");
        using var gray = new Mat();
        small.ConvertTo(gray, MatType.CV_8UC1, 255 / (max - min), -min * 255 / (max - min));
        Cv2.Threshold(gray, gray, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        Cv2.FindContours(gray, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalModes.Tree, ContourApproximationModes.ApproxNone);
        var candidates = new List<(double Area, RotatedRect Ellipse)>();
        for (int i = 0; i < contours.Length; i++)
        {
            int child = hierarchy[i].Child;
            if (hierarchy[i].Parent >= 0 || child < 0 || contours[i].Length < 20) continue;
            // Select the largest actual hole; isolated dust holes cannot become the inner ring boundary.
            int hole = child;
            for (int c = hierarchy[child].Next; c >= 0; c = hierarchy[c].Next)
                if (Cv2.ContourArea(contours[c]) > Cv2.ContourArea(contours[hole])) hole = c;
            double area = Cv2.ContourArea(contours[i]), holeArea = Cv2.ContourArea(contours[hole]);
            if (area < 100 || holeArea / area < .55 || holeArea / area > .98 || contours[hole].Length < 20) continue;
            Rect box = Cv2.BoundingRect(contours[i]);
            if (box.X <= 0 || box.Y <= 0 || box.Right >= small.Width - 1 || box.Bottom >= small.Height - 1) continue;
            RotatedRect outer = Cv2.FitEllipse(contours[i]);
            RotatedRect inner = Cv2.FitEllipse(contours[hole]);
            if (outer.Size.Width / outer.Size.Height < .6
                || Math.Abs(area / (Math.PI * outer.Size.Width * outer.Size.Height / 4) - 1) > .12
                || Math.Abs(holeArea / (Math.PI * inner.Size.Width * inner.Size.Height / 4) - 1) > .12
                || Math.Sqrt(Math.Pow(outer.Center.X - inner.Center.X, 2) + Math.Pow(outer.Center.Y - inner.Center.Y, 2))
                    > Math.Min(outer.Size.Width, outer.Size.Height) * .03) continue;
            // One fit to both boundaries avoids averaging ambiguous near-circular ellipse angles.
            RotatedRect fit = Cv2.FitEllipse(contours[i].Concat(contours[hole]).ToArray());
            candidates.Add((area, fit));
        }
        if (candidates.Count == 0) throw new MeasurementException("primary_ring_missing", "未找到完整的亮圆环。请保留完整主环及背景，检查图案是否为圆环。");
        candidates.Sort((a, b) => b.Area.CompareTo(a.Area));
        if (candidates.Count > 1 && candidates[1].Area > candidates[0].Area * .2)
            throw new MeasurementException("multiple_primary_rings", "存在多个独立亮环，无法自动确定主像。");
        var chosen = candidates[0].Ellipse;
        double sx = (double)width / small.Width, sy = (double)height / small.Height;
        return new RotatedRect(new Point2f((float)((chosen.Center.X + .5) * sx - .5), (float)((chosen.Center.Y + .5) * sy - .5)),
            new Size2f((float)(chosen.Size.Width * sx), (float)(chosen.Size.Height * sy)), chosen.Angle);
    }

    private static double[] SampleRing(float[] signal, int width, int height, RotatedRect ellipse, double step, int length, CancellationToken token)
    {
        var samples = new double[RingAngles * length];
        double radius = (ellipse.Size.Width + ellipse.Size.Height) / 4;
        for (int a = 0; a < RingAngles; a++)
        {
            token.ThrowIfCancellationRequested();
            var direction = RingPoint(ellipse, radius, a * 2 * Math.PI / RingAngles);
            double dx = (direction.X - ellipse.Center.X) / radius, dy = (direction.Y - ellipse.Center.Y) / radius;
            for (int r = 0; r < length; r++)
            {
                double x = ellipse.Center.X + r * step * dx, y = ellipse.Center.Y + r * step * dy;
                int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
                if (ix < 0 || iy < 0 || ix + 1 >= width || iy + 1 >= height)
                    throw new MeasurementException("ring_margin_missing", "主亮环外围需保留约 30% 半径的背景边距，不能用图外零值测量。");
                double fx = x - ix, fy = y - iy;
                samples[a * length + r] = (1 - fy) * ((1 - fx) * signal[iy * width + ix] + fx * signal[iy * width + ix + 1])
                    + fy * ((1 - fx) * signal[(iy + 1) * width + ix] + fx * signal[(iy + 1) * width + ix + 1]);
            }
        }
        return samples;
    }

    private static RingBand FindRingBand(double[] profile, int peak, int window)
    {
        int leftBase = peak, rightBase = peak;
        for (int i = Math.Max(1, peak - window); i < peak; i++) if (profile[i] < profile[leftBase]) leftBase = i;
        for (int i = peak + 1; i <= Math.Min(profile.Length - 2, peak + window); i++) if (profile[i] < profile[rightBase]) rightBase = i;
        double baseline = Math.Max(profile[leftBase], profile[rightBase]);
        double prominence = profile[peak] - baseline, half = baseline + prominence / 2;
        int left = peak, right = peak;
        while (left > leftBase && profile[left - 1] >= half) left--;
        while (right < rightBase && profile[right + 1] >= half) right++;
        return new RingBand(peak, left, right, leftBase, rightBase, prominence);
    }

    private static RingReading ReadRingBand(double[] polar, int length, RingBand band, double minimumContrast)
    {
        double total = 0, totalBackground = 0, weights = 0;
        var sectors = new double[24];
        for (int a = 0; a < RingAngles; a++)
        {
            double sum = 0, background = 0, weight = 0;
            for (int r = band.Left; r <= band.Right; r++)
            {
                double t = (double)(r - band.LeftBase) / Math.Max(1, band.RightBase - band.LeftBase);
                // Integrate radius-weighted samples: equal-width annuli have different areas.
                double b = polar[a * length + band.LeftBase] * (1 - t) + polar[a * length + band.RightBase] * t;
                sum += polar[a * length + r] * r; background += b * r; weight += r;
            }
            total += sum; totalBackground += background; weights += weight;
            sectors[a * sectors.Length / RingAngles] += (sum - background) / Math.Max(1, weight) / (RingAngles / sectors.Length);
        }
        return new RingReading(band, total / Math.Max(1, weights), totalBackground / Math.Max(1, weights),
            (double)sectors.Count(v => v > minimumContrast) / sectors.Length);
    }

    private static double[] SmoothRingProfile(double[] profile, double sigma)
    {
        int half = (int)Math.Ceiling(sigma * 3);
        var result = new double[profile.Length];
        for (int i = 0; i < result.Length; i++)
        {
            double sum = 0, weight = 0;
            for (int d = -half; d <= half; d++)
            {
                double w = Math.Exp(-d * d / (2 * sigma * sigma));
                sum += profile[Math.Clamp(i + d, 0, profile.Length - 1)] * w; weight += w;
            }
            result[i] = sum / weight;
        }
        return result;
    }

    private static double RingMedian(double[] values)
    {
        Array.Sort(values);
        int middle = values.Length / 2;
        return values.Length % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle];
    }

    private static AlgorithmPoint RingPoint(RotatedRect ellipse, double radial, double angle)
    {
        double scale = radial * 4 / (ellipse.Size.Width + ellipse.Size.Height), rotation = ellipse.Angle * Math.PI / 180;
        double u = Math.Cos(angle) * ellipse.Size.Width / 2 * scale, v = Math.Sin(angle) * ellipse.Size.Height / 2 * scale;
        return new AlgorithmPoint(ellipse.Center.X + u * Math.Cos(rotation) - v * Math.Sin(rotation),
            ellipse.Center.Y + u * Math.Sin(rotation) + v * Math.Cos(rotation));
    }

    private static void AddRingBoundary(List<AlgorithmGeometry> shapes, string id, RotatedRect ellipse, double radius)
        => shapes.Add(new AlgorithmGeometry(id, AlgorithmGeometryKind.Polyline,
            Enumerable.Range(0, 181).Select(i => RingPoint(ellipse, radius, i * 2 * Math.PI / 180)).ToArray()));

    private static void RejectSaturatedRing(float[] signal, int width, int height, RotatedRect ellipse, RingBand band, double step, CancellationToken token)
    {
        double angle = ellipse.Angle * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle);
        double reference = (ellipse.Size.Width + ellipse.Size.Height) / 4;
        double halfWidth = ellipse.Size.Width / 2, halfHeight = ellipse.Size.Height / 2;
        double extent = Math.Max(halfWidth, halfHeight) * band.Right * step / reference + 2;
        for (int y = Math.Max(0, (int)(ellipse.Center.Y - extent)); y < Math.Min(height, ellipse.Center.Y + extent); y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = Math.Max(0, (int)(ellipse.Center.X - extent)); x < Math.Min(width, ellipse.Center.X + extent); x++)
            {
                if (signal[y * width + x] < 1) continue;
                double dx = x - ellipse.Center.X, dy = y - ellipse.Center.Y;
                double u = (dx * cos + dy * sin) / halfWidth, v = (-dx * sin + dy * cos) / halfHeight;
                double radial = Math.Sqrt(u * u + v * v) * reference;
                if (radial >= band.Left * step && radial <= band.Right * step)
                    throw new MeasurementException("saturated_primary_ring", "主像测量环存在满量程像素，亮度比无效，请降低曝光后重拍。");
            }
        }
    }
}
