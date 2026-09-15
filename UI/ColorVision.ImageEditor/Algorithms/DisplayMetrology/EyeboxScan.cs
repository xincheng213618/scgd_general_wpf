using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms;

public sealed partial class DisplayMetrologyProvider
{
    private static void MeasureEyebox(AlgorithmExecutionContext context, EyeboxScanParameters p, List<AlgorithmArtifact> artifacts, CancellationToken token)
    {
        if (context.Inputs.Count != p.Columns * p.Rows)
            throw new MeasurementException("scan_count_mismatch", "输入数量必须等于扫描行数 × 列数，不能填补缺失位置。");
        for (int i = 0; i < context.Inputs.Count; i++)
            if (context.Inputs[i].Name != $"sample-{i}") throw new MeasurementException("scan_order_mismatch", "扫描帧须按 sample-0、sample-1… 行优先命名与排序。");
        var reference = context.Inputs[p.ReferenceIndex].Image;
        float[] baseline = ReadSignal(reference, p.Channel, p.DecodeExponent, token);
        int validPixels = baseline.Count(v => v >= p.ReferenceSignalFloor);
        if (validPixels == 0) throw new MeasurementException("invalid_scan_reference", "参考帧没有高于有效信号下限的像素。");
        double baselineSum = baseline.Where(v => v >= p.ReferenceSignalFloor).Sum(v => (double)v);
        var rows = new List<IReadOnlyDictionary<string, JsonElement>>();
        var accepted = new bool[context.Inputs.Count];
        var map = new byte[context.Inputs.Count];
        for (int index = 0; index < context.Inputs.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            context.Progress?.Report(new AlgorithmProgress((double)index / context.Inputs.Count, "eyebox.sample", $"扫描点 {index + 1}/{context.Inputs.Count}"));
            float[] signal = ReadSignal(context.Inputs[index].Image, p.Channel, p.DecodeExponent, token);
            double sum = 0;
            int covered = 0;
            for (int i = 0; i < signal.Length; i++)
            {
                if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
                if (baseline[i] < p.ReferenceSignalFloor) continue;
                sum += signal[i];
                if (signal[i] >= baseline[i] * p.MinimumPixelRatio) covered++;
            }
            double meanRatio = sum / baselineSum, coverage = (double)covered / validPixels;
            accepted[index] = meanRatio >= p.MinimumMeanRatio && coverage >= p.MinimumCoverage;
            map[index] = accepted[index] ? (byte)255 : (byte)0;
            rows.Add(Row(("sample", index), ("x_mm", (index % p.Columns - p.ReferenceIndex % p.Columns) * p.StepXMillimeters),
                ("y_mm", (index / p.Columns - p.ReferenceIndex / p.Columns) * p.StepYMillimeters),
                ("meanOverReference", meanRatio), ("coverage", coverage), ("accepted", accepted[index])));
        }
        int acceptedCells = 0;
        for (int y = 0; y + 1 < p.Rows; y++)
            for (int x = 0; x + 1 < p.Columns; x++)
            {
                int i = y * p.Columns + x;
                if (accepted[i] && accepted[i + 1] && accepted[i + p.Columns] && accepted[i + p.Columns + 1]) acceptedCells++;
            }
        Table(artifacts, "eyebox-scan", ["sample", "x_mm", "y_mm", "meanOverReference", "coverage", "accepted"], rows);
        Metrics(artifacts, ("accepted_sample_count", accepted.Count(v => v), "count"), ("reference_valid_pixels", validPixels, "px²"),
            ("four_corner_accepted_mesh_area", acceptedCells * p.StepXMillimeters * p.StepYMillimeters, "mm²"),
            ("sampled_span_x", (p.Columns - 1) * p.StepXMillimeters, "mm"), ("sampled_span_y", (p.Rows - 1) * p.StepYMillimeters, "mm"));
        artifacts.Add(new AlgorithmImageArtifact("eyebox-sample-grid", "visualization", new AlgorithmImageBuffer(p.Columns, p.Rows, p.Columns, AlgorithmImageFormat.Gray8, map),
            new Dictionary<string, string> { ["coordinates"] = "scan-grid, not image pixels", ["area"] = "mesh cells with four accepted sampled corners; unsampled interior is not verified" }));
    }
}
