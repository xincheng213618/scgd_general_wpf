using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.Engine.Templates.Jsons.RingGhost;

/// <summary>Persisted local waveguide measurement; diagnostics stay out of the customer metric model.</summary>
public sealed class LocalRingGhostResult
{
    public const string ResultVersion = "ring-ghost/1.0";
    public const string SchemaId = "colorvision.local-ring-ghost/v1";
    public const int MaximumJsonBytes = 16 * 1024 * 1024;
    [JsonRequired] public string Schema { get; set; } = SchemaId;
    [JsonRequired] public double GMR1 { get; set; }
    [JsonRequired] public double GMR2 { get; set; }
    [JsonRequired] public int ImageWidth { get; set; }
    [JsonRequired] public int ImageHeight { get; set; }
    [JsonRequired] public int CandidateCount { get; set; }
    [JsonRequired] public RingGhostOrderMapping OrderMapping { get; set; }
    [JsonRequired] public bool SingleCandidateAssignedToFirst { get; set; }
    [JsonRequired] public string MeasurementStatus { get; set; } = "MEASURED";
    public string DiagnosticJson { get; set; } = "";
    [JsonRequired] public AlgorithmGeometry[] Regions { get; set; } = [];

    internal static LocalRingGhostResult FromAlgorithm(AlgorithmResult result, int width, int height, RingGhostOrderMapping mapping)
    {
        if (result.AlgorithmId != DisplayMetrologyIds.RingGhost || result.Status != AlgorithmResultStatus.Succeeded)
            throw new InvalidDataException("圆环鬼影算法未成功完成，不能生成零值结果。");
        if (mapping is not RingGhostOrderMapping.OuterIsFirst and not RingGhostOrderMapping.InnerIsFirst)
            throw new InvalidDataException("必须确定两个候选内外位置与鬼影阶次的对应关系。");
        var rows = result.GetArtifact<AlgorithmTableArtifact>("ring-ghost-candidates")?.Rows
            ?? throw new InvalidDataException("圆环鬼影结果缺少候选记录。");
        if (rows.Count > 2) throw new InvalidDataException("存在两个以上鬼影候选，阶次不明确。");
        double Ratio(int index) => rows[index]["平均信号比_%"].GetDouble();
        // This Flow profile is for the customer's first-order-always-present waveguide test.
        // A sole detected ring is first order; a normally undetected second order is reported as zero.
        var measurement = new LocalRingGhostResult
        {
            GMR1 = rows.Count == 0 ? 0 : Ratio(rows.Count == 1 || mapping == RingGhostOrderMapping.OuterIsFirst ? 0 : 1),
            GMR2 = rows.Count < 2 ? 0 : Ratio(mapping == RingGhostOrderMapping.OuterIsFirst ? 1 : 0),
            ImageWidth = width, ImageHeight = height, CandidateCount = rows.Count, OrderMapping = mapping,
            SingleCandidateAssignedToFirst = rows.Count == 1,
            MeasurementStatus = rows.Count == 0 ? "NOT_DETECTED" : rows.Count == 1 ? "SECOND_NOT_DETECTED" : "MEASURED",
            DiagnosticJson = JsonSerializer.Serialize(new
            {
                algorithmId = result.AlgorithmId.Value, algorithmVersion = result.AlgorithmVersion.ToString(),
                context = result.GetArtifact<AlgorithmStructuredDataArtifact>("ring-ghost-measurement")?.Data,
                candidates = rows, policy = "first-order-always-present; normal non-detection reported as zero"
            }),
            Regions = result.GetArtifact<AlgorithmGeometryArtifact>("display-regions")?.Geometries.ToArray()
                ?? throw new InvalidDataException("圆环鬼影结果缺少测量区域。")
        };
        measurement.Validate();
        return measurement;
    }

    public void Validate()
    {
        if (Schema != SchemaId || !double.IsFinite(GMR1) || !double.IsFinite(GMR2) || GMR1 < 0 || GMR2 < 0
            || ImageWidth <= 0 || ImageHeight <= 0 || (long)ImageWidth * ImageHeight > 33_554_432
            || CandidateCount is < 0 or > 2 || OrderMapping is not RingGhostOrderMapping.OuterIsFirst and not RingGhostOrderMapping.InnerIsFirst)
            throw new InvalidDataException("圆环鬼影结果格式、阶次或数值无效。");
        string expectedStatus = CandidateCount == 0 ? "NOT_DETECTED" : CandidateCount == 1 ? "SECOND_NOT_DETECTED" : "MEASURED";
        if (MeasurementStatus != expectedStatus || SingleCandidateAssignedToFirst != (CandidateCount == 1)
            || CandidateCount < 2 && GMR2 != 0 || CandidateCount == 0 && GMR1 != 0)
            throw new InvalidDataException("圆环鬼影未检出状态与输出值不一致。");
        if (Regions == null || Regions.Length != 2 + CandidateCount * 2 || Regions.Any(g => g == null
            || g.Kind is not AlgorithmGeometryKind.Polyline and not AlgorithmGeometryKind.Polygon
            || g.Points == null || g.Points.Count < 3 || g.Points.Count > 4096 || g.Points.Any(p =>
                !double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.X < 0 || p.Y < 0 || p.X >= ImageWidth || p.Y >= ImageHeight)))
            throw new InvalidDataException("圆环鬼影测量区域缺失或越出原图。");
    }

    public static LocalRingGhostResult Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        if (stream.Length > MaximumJsonBytes) throw new InvalidDataException("圆环鬼影结果文件超过 16 MiB。");
        var result = JsonSerializer.Deserialize<LocalRingGhostResult>(stream)
            ?? throw new InvalidDataException("圆环鬼影结果为空。");
        result.Validate();
        return result;
    }
}
