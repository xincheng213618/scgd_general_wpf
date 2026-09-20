using CameraTest.Models;
using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CameraTest.Application;

public sealed record FrameAnalysis(Guid FrameId, string Source, DateTimeOffset CapturedAt, int Width, int Height, int BitDepth, int Channels,
    double Frequency, SfrAnalysisOptions Options, double ElapsedMilliseconds, IReadOnlyList<BmwTargetAnalysis> Targets)
{
    public FrameSourceKind SourceKind { get; init; }
    public StandaloneCameraOptions? AcquisitionSettings { get; init; }
    public IReadOnlyList<EdgeColorAnalysis> ColorShifts { get; init; } = [];
    public JudgmentRules Rules { get; init; } = new();
    public FrameJudgment Judgment { get; init; } = new(JudgmentState.Unconfigured, []);

    public static FrameAnalysis Run(TestFrame frame, TestProfile profile)
    {
        profile.Validate();
        if (profile.Regions.Count == 0) throw new InvalidOperationException("请先框选至少一个 BMW 搜索区域。");
        if (profile.ImageWidth != frame.Data.Width || profile.ImageHeight != frame.Data.Height)
            throw new InvalidOperationException("当前图像尺寸与搜索区域参考尺寸不一致，请重新框选或加载匹配配置。");
        var regions = profile.Regions.Select(r => new BmwSearchRegion(r.Id, r.Roi)).ToArray();
        var options = profile.Sfr with { };
        var timer = Stopwatch.StartNew();
        var targets = frame.Read(image => BmwSfrAnalyzer.Analyze(image, regions, options));
        var colors = targets.SelectMany(target => target.Edges.Select(edge => new EdgeColorAnalysis(target.Id, edge.Id.ToString(),
            SfrChromaticAberration.Analyze(edge.Analysis, edge.Roi)))).ToArray();
        timer.Stop();
        var result = new FrameAnalysis(frame.Id, frame.Source, frame.Data.CapturedAt, frame.Data.Width, frame.Data.Height, frame.Data.BitDepth, frame.Data.Channels,
            profile.Analysis.TargetFrequency, options, timer.Elapsed.TotalMilliseconds, targets)
        {
            SourceKind = frame.SourceKind, AcquisitionSettings = frame.AcquisitionSettings?.Copy(), ColorShifts = colors, Rules = profile.Judgment with { }
        };
        return result with { Judgment = FrameJudgment.Evaluate(result, result.Rules) };
    }

    public IReadOnlyList<MetricRow> Rows() => Targets.SelectMany(target => target.Edges.SelectMany(edge =>
        edge.Analysis is { } result
        ? result.Channels.Select(channel => new MetricRow(target.Id, edge.Id.ToString(), channel.Channel == "L" ? "Y (L)" : channel.Channel,
            channel.Valid ? (channel.Warnings.Length == 0 ? "有效" : string.Join("; ", channel.Warnings)) : channel.Reason,
            channel.Valid ? channel.Mtf50 : null, channel.Valid ? channel.Mtf10 : null,
            channel.Valid ? SfrCurveQueries.AtFrequency(channel.Frequencies, channel.Mtf, Frequency) : null, result))
        : new[] { new MetricRow(target.Id, edge.Id.ToString(), "—", edge.Reason, null, null, null, null) })).ToArray();

    public void Export(string path)
    {
        if (string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { schemaVersion = 2, measurement = this }, ProfileStore.JsonOptions));
            return;
        }
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("FrameId,Source,Target,Edge,Channel,Status,MTF50_cyclesPerPixel,MTF10_cyclesPerPixel,MTF_at_frequency,Frequency_cyclesPerPixel");
        foreach (var row in Rows()) writer.WriteLine(string.Join(",", Quote(FrameId.ToString()), Quote(Source), Quote(row.Target), Quote(row.Edge), Quote(row.Channel), Quote(row.Status), Number(row.Mtf50), Number(row.Mtf10), Number(row.Response), Number(Frequency)));
        writer.WriteLine();
        writer.WriteLine("Target,Edge,Pair,Axis,Axis_shift_pixel,Common_normal_shift_pixel,Valid,Status");
        foreach (var edge in ColorShifts)
            foreach (var pair in edge.Analysis.Pairs)
                writer.WriteLine(string.Join(",", Quote(edge.Target), Quote(edge.Edge), Quote(pair.Pair), edge.Analysis.Axis, Number(pair.AxisShiftPixels), Number(pair.NormalShiftPixels), pair.Valid, Quote(pair.Valid ? string.Join("; ", pair.Warnings) : pair.Reason)));
        writer.WriteLine();
        writer.WriteLine("Target,Edge,Channel,Metric,Value,Limit,Unit,Judgment,Reason");
        foreach (var item in Judgment.Items)
            writer.WriteLine(string.Join(",", Quote(item.Target), Quote(item.Edge), Quote(item.Channel), Quote(item.Metric), Number(item.Value), Number(item.Limit), Quote(item.Unit), item.State, Quote(item.Reason)));
        writer.WriteLine();
        writer.WriteLine("Target,Edge,Channel,Frequency_cyclesPerPixel,MTF");
        foreach (var target in Targets)
            foreach (var edge in target.Edges)
                if (edge.Analysis is { } analysis)
                    foreach (var channel in analysis.Channels.Where(c => c.Valid))
                        for (int i = 0; i < channel.Frequencies.Length; i++)
                            writer.WriteLine(string.Join(",", Quote(target.Id), Quote(edge.Id.ToString()), Quote(channel.Channel), Number(channel.Frequencies[i]), Number(channel.Mtf[i])));
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    private static string Number(double? value) => value?.ToString("G17", CultureInfo.InvariantCulture) ?? string.Empty;
}

public sealed record MetricRow(string Target, string Edge, string Channel, string Status, double? Mtf50, double? Mtf10, double? Response, SfrAnalysisResult? Analysis);
public sealed record EdgeColorAnalysis(string Target, string Edge, SfrChromaticAberrationResult Analysis);
public sealed record ColorShiftRow(string Target, string Edge, string Pair, string Axis, double? Shift, string Status, SfrChromaticAberrationResult Analysis);
