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
    public static readonly AlgorithmId Ghost = new("colorvision.display.ghost-measurement");
    public static readonly AlgorithmId Defects = new("colorvision.display.defects");
    public static readonly AlgorithmId Binocular = new("colorvision.display.binocular-quality");
    public static readonly AlgorithmId Eyebox = new("colorvision.display.eyebox-scan");
    public static readonly AlgorithmId FieldSfr = new("colorvision.display.field-sfr");
    public static IReadOnlySet<AlgorithmId> All { get; } = new HashSet<AlgorithmId>
        { RgbRegistration, Ghost, Defects, Binocular, Eyebox, FieldSfr };
}

internal static class DisplayMetrologyCatalog
{
    internal const AlgorithmHostCapabilities Capabilities = AlgorithmHostCapabilities.Interactive
        | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic
        | AlgorithmHostCapabilities.Flow;

    internal static void Register(AlgorithmCatalog catalog)
    {
        Add(catalog, DisplayMetrologyIds.RgbRegistration, "RGB 图案套色", new RgbRegistrationParameters(), 1, 1, 1);
        Add(catalog, DisplayMetrologyIds.Ghost, "鬼影与杂散光评价", new GhostMeasurementParameters(), 1, 1, 2);
        Add(catalog, DisplayMetrologyIds.Defects, "亮暗点 / 线缺陷 / Mura", new DisplayDefectParameters(), 1, 1, 3);
        Add(catalog, DisplayMetrologyIds.Binocular, "左右眼对准与信号一致性", new BinocularQualityParameters(), 2, 2, 4);
        Add(catalog, DisplayMetrologyIds.Eyebox, "Eyebox 扫描评价", new EyeboxScanParameters(), 4, 81, 5);
        Add(catalog, DisplayMetrologyIds.FieldSfr, "全视场斜边 SFR", new FieldSfrParameters(), 1, 1, 6);
    }

    private static void Add(AlgorithmCatalog catalog, AlgorithmId id, string name, IAlgorithmParameters parameters, int minimum, int maximum, int order)
    {
        var fields = parameters.GetType().GetProperties().Where(p => p.CanWrite).Select(p => new AlgorithmParameterField(
            p.Name, p.PropertyType.Name, AlgorithmJson.ToElement(p.GetValue(parameters)),
            Description: p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName)).ToArray();
        var formats = Enum.GetValues<AlgorithmImageFormat>().Where(f => id != DisplayMetrologyIds.RgbRegistration || f.Channels() >= 3).ToHashSet();
        catalog.Register(new AlgorithmDescriptor(id, new AlgorithmVersion(1, 0, 0), name, "显示计量",
            "离线图案评价；输出像素坐标与相对信号，不附带产品合格判定或绝对光度标定。", parameters.GetType(),
            new AlgorithmParameterSchema(1, fields, AlgorithmJson.ToElement(parameters)), formats,
            Capabilities | (maximum > 1 ? AlgorithmHostCapabilities.MultiInput : 0), minimum, maximum,
            OutputFormats: new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 }, OutputFormatPolicy: "analysis-only; masks=gray8")
        {
            ResultSemantics = AlgorithmResultSemantics.Analysis,
            Presentation = new AlgorithmPresentationMetadata(InteractiveEntries:
            [new AlgorithmInteractivePresentation(id.Value, order, name)
            { Group = new AlgorithmInteractiveGroupPresentation("DisplayMetrology", 45, "显示计量") }]),
        });
    }
}
