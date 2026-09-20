using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CameraTest.Models;

public sealed record SearchRegion(string Id, int X, int Y, int Width, int Height)
{
    [JsonIgnore] public RoiRect Roi => new(X, Y, Width, Height);
    [JsonIgnore] public string Bounds => $"{X}, {Y} · {Width} × {Height}";
}

public sealed class AnalysisSettings
{
    [Category("指标"), DisplayName("目标频率 (cycles/pixel)"), Description("范围 0～0.5；0.5 为 Nyquist 频率。")]
    public double TargetFrequency { get; set; } = 0.25;
    [Category("实时"), DisplayName("分析间隔 (ms)"), Description("只处理最新帧，上一轮未完成时不排队。实际刷新率取决于相机和算法耗时。")]
    public int LiveIntervalMilliseconds { get; set; } = 300;

    public void Validate()
    {
        if (!double.IsFinite(TargetFrequency) || TargetFrequency < 0 || TargetFrequency > 0.5
            || LiveIntervalMilliseconds < 100 || LiveIntervalMilliseconds > 10000)
            throw new ArgumentException("目标频率应为 0～0.5，实时分析间隔应为 100～10000 ms。");
    }
}

public sealed class TestProfile
{
    public int SchemaVersion { get; set; } = 1;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public StandaloneCameraOptions Camera { get; set; } = new();
    public AnalysisSettings Analysis { get; set; } = new();
    public SfrAnalysisOptions Sfr { get; set; } = new();
    public JudgmentRules Judgment { get; set; } = new();
    public List<SearchRegion> Regions { get; set; } = new();

    public void Validate()
    {
        if (SchemaVersion != 1 || Camera == null || Analysis == null || Sfr == null || Judgment == null || Regions == null || Regions.Count > 64)
            throw new ArgumentException("检测配置版本或内容无效；最多支持 64 个搜索区域。");
        Analysis.Validate();
        Sfr.Validate();
        Judgment.Validate();
        if (ImageWidth < 0 || ImageHeight < 0 || Regions.Count != 0 && (ImageWidth == 0 || ImageHeight == 0))
            throw new ArgumentException("搜索区域缺少参考图像尺寸。");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in Regions)
            if (r == null || string.IsNullOrWhiteSpace(r.Id) || !ids.Add(r.Id) || r.Width < 40 || r.Height < 40
                || r.X < 0 || r.Y < 0 || (long)r.X + r.Width > ImageWidth || (long)r.Y + r.Height > ImageHeight)
                throw new ArgumentException("搜索区域名称须唯一，矩形须完整位于参考图像内且至少为 40×40 像素。");
    }
}

public static class ProfileStore
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static TestProfile Load(string path)
    {
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("配置文件超过 1 MiB。");
        var profile = JsonSerializer.Deserialize<TestProfile>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("配置为空。");
        profile.Validate();
        return profile;
    }

    public static void Save(string path, TestProfile profile)
    {
        profile.Validate();
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
    }
}
