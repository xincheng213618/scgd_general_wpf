using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace ColorVision.ImageEditor.Algorithms;

public static class DisplayMetrologyIds
{
    public static readonly AlgorithmId RgbRegistration = new("colorvision.display.rgb-registration");
    public static readonly AlgorithmId RgbCrossRegistration = new("colorvision.display.rgb-cross-registration");
    public static readonly AlgorithmId Ghost = new("colorvision.display.ghost-measurement");
    public static readonly AlgorithmId Defects = new("colorvision.display.defects");
    public static readonly AlgorithmId Dust = new("colorvision.display.dust-detection");
    public static readonly AlgorithmId Binocular = new("colorvision.display.binocular-quality");
    public static readonly AlgorithmId Eyebox = new("colorvision.display.eyebox-scan");
    public static readonly AlgorithmId FieldSfr = new("colorvision.display.field-sfr");
    public static IReadOnlySet<AlgorithmId> All { get; } = new HashSet<AlgorithmId>
        { RgbRegistration, RgbCrossRegistration, Ghost, Defects, Dust, Binocular, Eyebox, FieldSfr };
}

internal static class DisplayMetrologyCatalog
{
    internal const AlgorithmHostCapabilities Capabilities = AlgorithmHostCapabilities.Interactive
        | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic
        | AlgorithmHostCapabilities.Flow;

    internal static void Register(AlgorithmCatalog catalog)
    {
        Add(catalog, DisplayMetrologyIds.RgbRegistration, "RGB 图案套色", new RgbRegistrationParameters(), 1, 1, 2, AlgorithmMenuGroups.ColorRegistration, "Algorithm_RgbRegistration");
        Add(catalog, DisplayMetrologyIds.RgbCrossRegistration, "十字 RGB 分离", new RgbCrossRegistrationParameters(), 1, 1, 1, AlgorithmMenuGroups.ColorRegistration, "Algorithm_RgbCross");
        Add(catalog, DisplayMetrologyIds.Ghost, "鬼影与杂散光评价", new GhostMeasurementParameters(), 1, 1, 4, AlgorithmMenuGroups.Defects, "Algorithm_GhostMetrology");
        Add(catalog, DisplayMetrologyIds.Defects, "亮暗点 / 线缺陷 / Mura", new DisplayDefectParameters(), 1, 1, 2, AlgorithmMenuGroups.Defects, "Algorithm_DisplayDefects");
        Add(catalog, DisplayMetrologyIds.Dust, "灰尘 / 脏污检测", new DustDetectionParameters(), 1, 1, 3, AlgorithmMenuGroups.Defects, "Algorithm_DustDetection");
        Add(catalog, DisplayMetrologyIds.Binocular, "左右眼对准与信号一致性", new BinocularQualityParameters(), 2, 2, 2, AlgorithmMenuGroups.Stereo, "Algorithm_BinocularQuality");
        Add(catalog, DisplayMetrologyIds.Eyebox, "Eyebox 扫描评价", new EyeboxScanParameters(), 4, 81, 3, AlgorithmMenuGroups.Stereo, "Algorithm_Eyebox");
        Add(catalog, DisplayMetrologyIds.FieldSfr, "全视场斜边 SFR", new FieldSfrParameters(), 1, 1, 3, AlgorithmMenuGroups.ImageQuality, "Algorithm_FieldSfr");
    }

    private static void Add(AlgorithmCatalog catalog, AlgorithmId id, string name, IAlgorithmParameters parameters, int minimum, int maximum, int order, AlgorithmInteractiveGroupPresentation group, string resourceKey)
    {
        var fields = parameters.GetType().GetProperties().Where(p => p.CanWrite).Select(p => new AlgorithmParameterField(
            p.Name, p.PropertyType.Name, AlgorithmJson.ToElement(p.GetValue(parameters)),
            Description: p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName)).ToArray();
        bool requiresColor = id == DisplayMetrologyIds.RgbRegistration || id == DisplayMetrologyIds.RgbCrossRegistration;
        var formats = Enum.GetValues<AlgorithmImageFormat>().Where(f => (!requiresColor || f.Channels() >= 3)
            && (id != DisplayMetrologyIds.Dust || f.Channels() <= 3)).ToHashSet();
        catalog.Register(new AlgorithmDescriptor(id, id == DisplayMetrologyIds.RgbCrossRegistration ? new AlgorithmVersion(1, 5, 0) : new AlgorithmVersion(1, 0, 0), name, "显示计量",
            "离线图案评价；输出像素坐标与相对信号，不附带客户 Recipe、量产合格判定或绝对光度标定。", parameters.GetType(),
            new AlgorithmParameterSchema(1, fields, AlgorithmJson.ToElement(parameters)), formats,
            Capabilities | (id == DisplayMetrologyIds.RgbCrossRegistration ? AlgorithmHostCapabilities.Roi : AlgorithmHostCapabilities.None) | (maximum > 1 ? AlgorithmHostCapabilities.MultiInput : 0), minimum, maximum,
            SupportsRectangleRoi: id == DisplayMetrologyIds.RgbCrossRegistration,
            OutputFormats: new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 }, OutputFormatPolicy: "analysis-only; masks=gray8")
        {
            ResultSemantics = AlgorithmResultSemantics.Analysis,
            Presentation = new AlgorithmPresentationMetadata(InteractiveEntries:
                [new AlgorithmInteractivePresentation(id.Value, order, name + "...", resourceKey) { Group = group }]),
        });
    }
}
