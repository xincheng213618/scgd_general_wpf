using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms;

public sealed partial class DisplayMetrologyProvider
{
    private const int EdgeSpreadBins = 97;
    private const int DftFrequencyCount = 65;
    private static readonly SemaphoreSlim FieldSfrWorker = new(1, 1);

    private static class DftWeights
    {
        public static readonly (double Cosine, double Sine)[] Values = Create();

        private static (double Cosine, double Sine)[] Create()
        {
            const int lineSpreadBins = EdgeSpreadBins - 2;
            var values = new (double, double)[DftFrequencyCount * lineSpreadBins];
            for (int i = 0; i < DftFrequencyCount; i++)
            {
                double f = i / 128.0;
                for (int j = 0; j < lineSpreadBins; j++)
                {
                    double angle = 2 * Math.PI * f * j / 4;
                    values[i * lineSpreadBins + j] = (Math.Cos(angle), Math.Sin(angle));
                }
            }
            return values;
        }
    }

    private sealed record EdgeSfr(bool Valid, string Reason, double Slope = 0, double Residual = 0, double? Mtf50 = null, double[]? Mtf = null);

    private static void MeasureFieldSfr(float[] signal, int width, int height, FieldSfrParameters p, List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var curves = new List<IReadOnlyDictionary<string, JsonElement>>();
        var shapes = new List<AlgorithmGeometry>();
        var frequencies = new List<double>();
        var fieldMap = new byte[p.Rows * p.Columns];
        IEnumerable<(int Index, Rect Bounds)> cells = Grid(width, height, p);
        EdgeSfr[]? parallelResults = null;
        // Share one additional worker across Field SFR calls because the outer CPU scheduler
        // can already run many invocations. Contention falls back to serial processing.
        const int minimumParallelPixels = 2 * 1024 * 1024;
        if (fieldMap.Length > 1 && width * height >= minimumParallelPixels && Environment.ProcessorCount > 1 && FieldSfrWorker.Wait(0))
        {
            try
            {
                var work = cells.ToArray();
                var results = new EdgeSfr[work.Length];
                Parallel.For(0, work.Length, new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = token },
                    index => results[index] = SlantedEdge(signal, width, work[index].Bounds, p, token));
                parallelResults = results;
                cells = work;
            }
            finally
            {
                FieldSfrWorker.Release();
            }
        }
        int valid = 0;
        foreach (var (index, bounds) in cells)
        {
            token.ThrowIfCancellationRequested();
            EdgeSfr result = parallelResults?[index] ?? SlantedEdge(signal, width, bounds, p, token);
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

        int offset = bounds.Y * stride + bounds.X;
        int rowStride = p.HorizontalEdge ? 1 : stride;
        int columnStride = p.HorizontalEdge ? stride : 1;
        double Pixel(int x, int y) => source[offset + y * rowStride + x * columnStride];
        // Reorder horizontal samples in bands of at most 512 KiB.
        const int maxBandSamples = 128 * 1024;
        int bandRows = p.HorizontalEdge && width * height >= maxBandSamples ? Math.Min(32, maxBandSamples / width) : 0;
        float[]? band = null;
        int bandStart = 0, bandEnd = 0;
        var centers = new List<(double Y, double X)>();
        double direction = 0;
        try
        {
            for (int y = 2; y < height - 2; y++)
            {
                token.ThrowIfCancellationRequested();
                // Let the first measured row reject the tile before renting or transposing.
                if (y == 3 && bandRows >= 4) band = ArrayPool<float>.Shared.Rent(bandRows * width);
                int start = offset + y * rowStride;
                int step = columnStride;
                if (band != null)
                {
                    if (y >= bandEnd)
                    {
                        bandStart = y;
                        bandEnd = Math.Min(height - 2, y + bandRows);
                        TransposeEdgeBand(source, stride, offset + y, width, bandEnd - bandStart, band);
                    }
                    start = (y - bandStart) * width;
                    step = 1;
                }
                string? failure = FindEdgeRowCenter(band ?? source, start, step, width, p.MinimumContrast, ref direction, out double center);
                if (failure != null) return new(false, failure);
                centers.Add((y, center));
            }
        }
        finally
        {
            if (band != null) ArrayPool<float>.Shared.Return(band);
        }
        double my = centers.Average(v => v.Y), mx = centers.Average(v => v.X);
        double slope = centers.Sum(v => (v.Y - my) * (v.X - mx)) / centers.Sum(v => (v.Y - my) * (v.Y - my));
        double intercept = mx - slope * my;
        double rms = Math.Sqrt(centers.Average(v => Math.Pow(v.X - slope * v.Y - intercept, 2)));
        if (Math.Abs(slope) < 0.02 || Math.Abs(slope) > 0.35) return new(false, "edge_angle_out_of_range");
        if (rms > 0.35) return new(false, "edge_not_straight");
        const int bins = EdgeSpreadBins;
        const int samplesPerPixel = 4;
        const double binCenter = bins / 2.0;
        var esf = new double[bins]; var counts = new int[bins];
        double projectionScale = Math.Sqrt(1 + slope * slope);
        // Include one extra pixel for truncating the projected center to an integer.
        int projectionRadius = (int)Math.Ceiling(binCenter / samplesPerPixel * projectionScale) + 1;
        for (int y = 2; y < height - 2; y++)
        {
            double rowShift = slope * y;
            int center = (int)(rowShift + intercept);
            int start = Math.Max(0, center - projectionRadius);
            int end = Math.Min(width, center + projectionRadius + 1);
            for (int x = start; x < end; x++)
            {
                double distance = (x - rowShift - intercept) / projectionScale;
                int bin = (int)Math.Floor(distance * samplesPerPixel + binCenter);
                if (bin < 0 || bin >= bins) continue;
                esf[bin] += Pixel(x, y); counts[bin]++;
            }
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
        var mtf = new double[DftFrequencyCount];
        for (int i = 0; i < mtf.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            double f = i / 128.0, real = 0, imaginary = 0;
            ReadOnlySpan<(double Cosine, double Sine)> weights = DftWeights.Values.AsSpan(i * lsf.Length, lsf.Length);
            for (int j = 0; j < lsf.Length; j++)
            {
                real += lsf[j] * weights[j].Cosine;
                imaginary -= lsf[j] * weights[j].Sine;
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

    private static void TransposeEdgeBand(float[] source, int stride, int offset, int width, int rows, float[] destination)
    {
        const int block = 32;
        for (int start = 0; start < width; start += block)
        {
            int end = Math.Min(start + block, width);
            for (int row = 0; row < rows; row++)
            {
                int sourceStart = offset + row;
                int destinationStart = row * width;
                for (int x = start; x < end; x++)
                    destination[destinationStart + x] = source[sourceStart + x * stride];
            }
        }
    }

    private static string? FindEdgeRowCenter(float[] samples, int start, int step, int width, double minimumContrast, ref double direction, out double center)
    {
        center = 0;
        double Pixel(int x) => samples[start + x * step];
        double left = 0, right = 0;
        for (int x = 0; x < 4; x++) { left += Pixel(x) / 4; right += Pixel(width - 1 - x) / 4; }
        double contrast = right - left;
        if (Math.Abs(contrast) < minimumContrast) return "low_edge_contrast";
        if (direction == 0) direction = Math.Sign(contrast);
        if (Math.Sign(contrast) != direction) return "mixed_edge_polarity";
        double sum = 0, weighted = 0, variation = 0;
        double previous = Pixel(1), current = Pixel(2);
        for (int x = 2, nextIndex = start + 3 * step; x < width - 2; x++, nextIndex += step)
        {
            double next = samples[nextIndex];
            double gradient = direction * (next - previous) / 2;
            variation += Math.Abs(gradient);
            sum += gradient; weighted += x * gradient;
            previous = current;
            current = next;
        }
        if (sum < minimumContrast || variation > Math.Abs(contrast) * 1.5) return "multiple_or_noisy_edges";
        center = weighted / sum;
        return center < 15 || center > width - 16 ? "insufficient_edge_support" : null;
    }
}
