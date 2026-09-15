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
    private sealed record EdgeSfr(bool Valid, string Reason, double Slope = 0, double Residual = 0, double? Mtf50 = null, double[]? Mtf = null);

    private static void MeasureFieldSfr(float[] signal, int width, int height, FieldSfrParameters p, List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var curves = new List<IReadOnlyDictionary<string, JsonElement>>();
        var shapes = new List<AlgorithmGeometry>();
        var frequencies = new List<double>();
        var fieldMap = new byte[p.Rows * p.Columns];
        int valid = 0;
        foreach (var (index, bounds) in Grid(width, height, p))
        {
            token.ThrowIfCancellationRequested();
            EdgeSfr result = SlantedEdge(signal, width, bounds, p, token);
            rows.Add(Row(("cell", index), ("centerX_px", bounds.X + (bounds.Width - 1) / 2.0), ("centerY_px", bounds.Y + (bounds.Height - 1) / 2.0),
                ("valid", result.Valid), ("reason", result.Reason), ("slope", result.Valid ? result.Slope : null),
                ("edgeFitRms_px", result.Valid ? result.Residual : null), ("mtf50_cyclesPerPixel", result.Mtf50)));
            if (!result.Valid) continue;
            valid++;
            if (result.Mtf50.HasValue)
            {
                frequencies.Add(result.Mtf50.Value);
                fieldMap[index] = (byte)Math.Clamp((int)Math.Round(result.Mtf50.Value * 510), 1, 255);
            }
            for (int i = 0; i < result.Mtf!.Length; i++) curves.Add(Row(("cell", index), ("cyclesPerPixel", i / 128.0), ("mtf", result.Mtf[i])));
            shapes.Add(Box($"SFR-{index}", bounds));
        }
        if (valid == 0) throw new MeasurementException("no_valid_slanted_edges", "没有有效斜边。每格须包含一条完整、接近竖直（或启用水平）的斜边，且边缘两侧均有稳定平台。");
        Table(artifacts, "field-sfr", ["cell", "centerX_px", "centerY_px", "valid", "reason", "slope", "edgeFitRms_px", "mtf50_cyclesPerPixel"], rows);
        Table(artifacts, "sfr-curves", ["cell", "cyclesPerPixel", "mtf"], curves);
        Metrics(artifacts, ("valid_edge_cells", valid, "count"), ("invalid_edge_cells", rows.Count - valid, "count"), ("mtf50_crossing_cells", frequencies.Count, "count"));
        if (frequencies.Count > 0) Metrics(artifacts, ("minimum_field_mtf50", frequencies.Min(), "cycles/pixel"), ("maximum_field_mtf50", frequencies.Max(), "cycles/pixel"));
        artifacts.Add(new AlgorithmImageArtifact("mtf50-field-map", "visualization", new AlgorithmImageBuffer(p.Columns, p.Rows, p.Columns, AlgorithmImageFormat.Gray8, fieldMap),
            new Dictionary<string, string> { ["scale"] = "1..255 represents mtf50 * 510; zero=unavailable; consult per-cell validity", ["coordinates"] = "test-pattern-grid" }));
        Overlay(artifacts, shapes);
    }

    // Independent bounded slanted-edge estimator, based on ESF projection -> derivative -> windowed DFT.
    // It is not an ISO-conformance implementation. Fixed 4x sampling and +/-12 px support are explicit.
    private static EdgeSfr SlantedEdge(float[] source, int stride, Rect bounds, FieldSfrParameters p, CancellationToken token)
    {
        int width = p.HorizontalEdge ? bounds.Height : bounds.Width;
        int height = p.HorizontalEdge ? bounds.Width : bounds.Height;
        if (width < 40 || height < 32) return new(false, "edge_roi_too_small");
        double Pixel(int x, int y) => p.HorizontalEdge ? source[(bounds.Y + x) * stride + bounds.X + y] : source[(bounds.Y + y) * stride + bounds.X + x];
        var centers = new List<(double Y, double X)>();
        double direction = 0;
        for (int y = 2; y < height - 2; y++)
        {
            token.ThrowIfCancellationRequested();
            double left = 0, right = 0;
            for (int x = 0; x < 4; x++) { left += Pixel(x, y) / 4; right += Pixel(width - 1 - x, y) / 4; }
            double contrast = right - left;
            if (Math.Abs(contrast) < p.MinimumContrast) return new(false, "low_edge_contrast");
            if (direction == 0) direction = Math.Sign(contrast);
            if (Math.Sign(contrast) != direction) return new(false, "mixed_edge_polarity");
            double sum = 0, weighted = 0, variation = 0;
            for (int x = 2; x < width - 2; x++)
            {
                double gradient = direction * (Pixel(x + 1, y) - Pixel(x - 1, y)) / 2;
                variation += Math.Abs(gradient);
                sum += gradient; weighted += x * gradient;
            }
            if (sum < p.MinimumContrast || variation > Math.Abs(contrast) * 1.5) return new(false, "multiple_or_noisy_edges");
            double center = weighted / sum;
            if (center < 15 || center > width - 16) return new(false, "insufficient_edge_support");
            centers.Add((y, center));
        }
        double my = centers.Average(v => v.Y), mx = centers.Average(v => v.X);
        double slope = centers.Sum(v => (v.Y - my) * (v.X - mx)) / centers.Sum(v => (v.Y - my) * (v.Y - my));
        double intercept = mx - slope * my;
        double rms = Math.Sqrt(centers.Average(v => Math.Pow(v.X - slope * v.Y - intercept, 2)));
        if (Math.Abs(slope) < 0.02 || Math.Abs(slope) > 0.35) return new(false, "edge_angle_out_of_range");
        if (rms > 0.35) return new(false, "edge_not_straight");
        const int bins = 97;
        var esf = new double[bins]; var counts = new int[bins];
        for (int y = 2; y < height - 2; y++)
            for (int x = 0; x < width; x++)
            {
                double distance = (x - slope * y - intercept) / Math.Sqrt(1 + slope * slope);
                int bin = (int)Math.Floor(distance * 4 + 48.5);
                if (bin < 0 || bin >= bins) continue;
                esf[bin] += Pixel(x, y); counts[bin]++;
            }
        if (counts.Count(v => v > 0) < bins * 0.8 || counts[0] == 0 || counts[^1] == 0) return new(false, "insufficient_subpixel_coverage");
        for (int i = 0; i < bins; i++) if (counts[i] > 0) esf[i] /= counts[i];
        for (int i = 1; i < bins - 1; i++)
        {
            if (counts[i] != 0) continue;
            int a = i - 1, b = i + 1;
            while (counts[b] == 0) b++;
            esf[i] = esf[a] + (esf[b] - esf[a]) / (b - a);
        }
        var lsf = new double[bins - 2];
        for (int i = 0; i < lsf.Length; i++)
        {
            double distance = (i + 1 - 48) / 4.0;
            double window = 0.54 + 0.46 * Math.Cos(Math.PI * distance / 12);
            lsf[i] = direction * (esf[i + 2] - esf[i]) * 2 * window;
        }
        double dc = lsf.Sum();
        if (dc <= 1e-8) return new(false, "invalid_lsf_dc");
        var mtf = new double[65];
        for (int i = 0; i < mtf.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            double f = i / 128.0, real = 0, imaginary = 0;
            for (int j = 0; j < lsf.Length; j++)
            {
                double angle = 2 * Math.PI * f * j / 4;
                real += lsf[j] * Math.Cos(angle); imaginary -= lsf[j] * Math.Sin(angle);
            }
            double derivativeCorrection = i == 0 ? 1 : (2 * Math.PI * f / 4) / Math.Sin(2 * Math.PI * f / 4);
            mtf[i] = Math.Sqrt(real * real + imaginary * imaginary) / dc * derivativeCorrection;
        }
        double? crossing = null;
        for (int i = 1; i < mtf.Length; i++)
            if (mtf[i - 1] >= 0.5 && mtf[i] < 0.5)
            { crossing = (i - 1 + (mtf[i - 1] - 0.5) / (mtf[i - 1] - mtf[i])) / 128; break; }
        return new(true, crossing.HasValue ? "" : "mtf50_not_reached_below_nyquist", slope, rms, crossing, mtf);
    }
}
