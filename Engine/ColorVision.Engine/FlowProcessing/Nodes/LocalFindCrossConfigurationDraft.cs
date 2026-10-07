using ColorVision.Common.MVVM;
using ColorVision.Core;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace ColorVision.Engine.FlowProcessing.Nodes;

/// <summary>Independent, transactional editor draft for the local production contract.</summary>
internal sealed class LocalFindCrossConfigurationDraft : ViewModelBase
{
    private string originalJson = "";
    private string initialState = "";
    private bool hadDistortion;
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Category("定位"), DisplayName("结果名称"), Description("标识本次十字结果；修改名称不会改变测量类型或增加其他测量结果。"), PropertyEditorType]
    public string Name { get; set; } = "Point_1";

    [Category("定位"), DisplayName("预期角度 (°)"), Description("产品名义方向，范围 -180° 到 180°。"), PropertyEditorType]
    public string ExpectedAngle { get; set; } = "0";

    [Category("定位"), DisplayName("最大允许旋转偏差 (±°)"), Description("相对预期角度的搜索范围，大于 0° 且不超过 45°。"), PropertyEditorType]
    public string AngleTolerance { get; set; } = "10";

    [Category("光学"), DisplayName("焦距 (mm)"), Description("填写实际使用的光学焦距，必须大于 0。"), PropertyEditorType]
    public string FocusLength { get; set; } = "25.4";

    [Category("光学"), DisplayName("像元尺寸 (μm)"), Description("传感器像元尺寸，必须大于 0。"), PropertyEditorType]
    public string PixelSize { get; set; } = "3.76";

    [Category("标准中心"), DisplayName("标准中心 X (px)"), Description("整幅图像坐标；请填写实际标定值。"), PropertyEditorType]
    [PropertyVisibility(nameof(UseImageCenter), true)]
    public string CenterX { get; set; } = "0";

    [Category("标准中心"), DisplayName("标准中心 Y (px)"), Description("整幅图像坐标，不是搜索区域内坐标。"), PropertyEditorType]
    [PropertyVisibility(nameof(UseImageCenter), true)]
    public string CenterY { get; set; } = "0";

    [Category("高级校准"), DisplayName("中心补偿 X (px)"), Description("叠加到检测中心后计算倾角。"), PropertyEditorType]
    [PropertyVisibility(nameof(UseOffset))]
    public string OffsetX { get; set; } = "0";

    [Category("高级校准"), DisplayName("中心补偿 Y (px)"), Description("叠加到检测中心后计算倾角。"), PropertyEditorType]
    [PropertyVisibility(nameof(UseOffset))]
    public string OffsetY { get; set; } = "0";

    [Category("镜头畸变"), DisplayName("畸变 K1"), Description("填写相机标定得到的 Brown 畸变系数。"), PropertyEditorType]
    [PropertyVisibility(nameof(HasDistortionParameters))]
    public string K1 { get; set; } = "0";

    [Category("镜头畸变"), DisplayName("畸变 K2"), Description("填写相机标定得到的 Brown 畸变系数。"), PropertyEditorType]
    [PropertyVisibility(nameof(HasDistortionParameters))]
    public string K2 { get; set; } = "0";

    [Category("镜头畸变"), DisplayName("畸变 P1"), Description("填写相机标定得到的 Brown 畸变系数。"), PropertyEditorType]
    [PropertyVisibility(nameof(HasDistortionParameters))]
    public string P1 { get; set; } = "0";

    [Category("镜头畸变"), DisplayName("畸变 P2"), Description("填写相机标定得到的 Brown 畸变系数。"), PropertyEditorType]
    [PropertyVisibility(nameof(HasDistortionParameters))]
    public string P2 { get; set; } = "0";

    [Category("镜头畸变"), DisplayName("畸变 K3"), Description("填写相机标定得到的 Brown 畸变系数。"), PropertyEditorType]
    [PropertyVisibility(nameof(HasDistortionParameters))]
    public string K3 { get; set; } = "0";

    [Category("镜头畸变"), DisplayName("内参 Fx (px)"), Description("须使用完整的相机标定内参；主点与倾角标准中心不同。"), PropertyEditorType]
    [PropertyVisibility(nameof(HasDistortionParameters))]
    public string Fx { get; set; } = "";

    [Category("镜头畸变"), DisplayName("内参 Fy (px)"), Description("须使用完整的相机标定内参；主点与倾角标准中心不同。"), PropertyEditorType]
    [PropertyVisibility(nameof(HasDistortionParameters))]
    public string Fy { get; set; } = "";

    [Category("镜头畸变"), DisplayName("镜头主点 Cx (px)"), Description("须使用完整的相机标定内参；主点与倾角标准中心不同。"), PropertyEditorType]
    [PropertyVisibility(nameof(HasDistortionParameters))]
    public string Cx { get; set; } = "";

    [Category("镜头畸变"), DisplayName("镜头主点 Cy (px)"), Description("须使用完整的相机标定内参；主点与倾角标准中心不同。"), PropertyEditorType]
    [PropertyVisibility(nameof(HasDistortionParameters))]
    public string Cy { get; set; } = "";

    [Category("标准中心"), DisplayName("使用图像中心"), Description("自动使用整幅输入图像的中心，与搜索区域无关。")]
    public bool UseImageCenter { get => useImageCenter; set { useImageCenter = value; OnPropertyChanged(); } }
    private bool useImageCenter = true;

    [Category("高级校准"), DisplayName("启用中心补偿"), Description("关闭时不输出 CalibrationOffset。")]
    public bool UseOffset { get => useOffset; set { useOffset = value; OnPropertyChanged(); } }
    private bool useOffset = false;

    [Category("镜头畸变"), DisplayName("启用镜头畸变校正"), Description("启用前必须填写完整的 Fx、Fy、Cx、Cy。")]
    public bool EnableDistortion { get => enableDistortion; set { enableDistortion = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasDistortionParameters)); } }
    private bool enableDistortion = false;

    [Browsable(false)] public bool HasDistortionParameters => EnableDistortion || hadDistortion;

    [Browsable(false)] public string ErrorProperty { get; private set; } = "";

    internal static bool TryCreate(string json, out LocalFindCrossConfigurationDraft? draft, out string error)
    {
        draft = null;
        if (!LocalFindCrossNodeServices.TryParseProductionOptions(json, out var options, out error)) return false;
        if (options.Optics == null) { error = "opticsParams 必须是光学参数对象。"; return false; }
        var optics = options.Optics;
        var distortion = optics.Distortion;
        draft = new()
        {
            originalJson = json,
            Name = options.Name,
            ExpectedAngle = Format(options.ExpectedAngleDegrees),
            AngleTolerance = Format(options.AngleToleranceDegrees),
            FocusLength = Format(optics.FocusLengthMillimeters),
            PixelSize = Format(optics.SensorPixelSizeMicrometers),
            UseImageCenter = !optics.StandardCenter.HasValue,
            CenterX = Format(optics.StandardCenter?.X ?? 0),
            CenterY = Format(optics.StandardCenter?.Y ?? 0),
            UseOffset = options.CalibrationOffset.HasValue,
            OffsetX = Format(options.CalibrationOffset?.X ?? 0),
            OffsetY = Format(options.CalibrationOffset?.Y ?? 0),
            hadDistortion = distortion != null,
            EnableDistortion = distortion?.Enabled ?? false,
            K1 = Format(distortion?.K1 ?? 0), K2 = Format(distortion?.K2 ?? 0),
            P1 = Format(distortion?.P1 ?? 0), P2 = Format(distortion?.P2 ?? 0), K3 = Format(distortion?.K3 ?? 0),
            Fx = Format(distortion?.FxPixels), Fy = Format(distortion?.FyPixels),
            Cx = Format(distortion?.PrincipalPointX), Cy = Format(distortion?.PrincipalPointY)
        };
        draft.initialState = draft.State();
        return true;
    }

    internal bool TryGetJson(out string json, out string error)
    {
        json = originalJson;
        ErrorProperty = "";
        try
        {
            if (string.IsNullOrWhiteSpace(Name)) throw Invalid(nameof(Name), "结果名称不能为空。");
            var options = new FindCrossLocalOptions
            {
                Name = Name,
                ExpectedAngleDegrees = Number(ExpectedAngle, nameof(ExpectedAngle), "预期角度", -180, 180),
                AngleToleranceDegrees = Number(AngleTolerance, nameof(AngleTolerance), "最大允许旋转偏差", 0, 45, positive: true),
                CalibrationOffset = UseOffset ? new FindCrossLocalPoint(Number(OffsetX, nameof(OffsetX), "中心补偿 X"), Number(OffsetY, nameof(OffsetY), "中心补偿 Y")) : null,
                Optics = new()
                {
                    FocusLengthMillimeters = Number(FocusLength, nameof(FocusLength), "焦距", 0, positive: true),
                    SensorPixelSizeMicrometers = Number(PixelSize, nameof(PixelSize), "像元尺寸", 0, positive: true),
                    StandardCenter = UseImageCenter ? null : new FindCrossLocalPoint(Number(CenterX, nameof(CenterX), "标准中心 X"), Number(CenterY, nameof(CenterY), "标准中心 Y"))
                }
            };
            if (hadDistortion || EnableDistortion)
            {
                options.Optics.Distortion = new()
                {
                    Enabled = EnableDistortion,
                    K1 = Number(K1, nameof(K1), "K1"), K2 = Number(K2, nameof(K2), "K2"),
                    P1 = Number(P1, nameof(P1), "P1"), P2 = Number(P2, nameof(P2), "P2"), K3 = Number(K3, nameof(K3), "K3"),
                    FxPixels = Optional(Fx, nameof(Fx), positive: true), FyPixels = Optional(Fy, nameof(Fy), positive: true),
                    PrincipalPointX = Optional(Cx, nameof(Cx)), PrincipalPointY = Optional(Cy, nameof(Cy))
                };
                if (EnableDistortion || !string.IsNullOrWhiteSpace(Fx + Fy + Cx + Cy))
                {
                    foreach (var item in new[] { (nameof(Fx), Fx), (nameof(Fy), Fy), (nameof(Cx), Cx), (nameof(Cy), Cy) })
                        if (string.IsNullOrWhiteSpace(item.Item2)) throw Invalid(item.Item1, "镜头畸变需要完整的 Fx、Fy、Cx、Cy；当前缺少 " + item.Item1 + "。");
                }
            }
            if (!options.TryValidate(out error)) return false;
            json = State() == initialState ? originalJson : JsonSerializer.Serialize(options, Indented);
            error = "";
            return true;
        }
        catch (ArgumentException ex) { error = ex.Message; return false; }
    }

    private string State() => JsonSerializer.Serialize(this);
    private static string Format(double? value) => value?.ToString("R", CultureInfo.InvariantCulture) ?? "";
    private double? Optional(string text, string property, bool positive = false) => string.IsNullOrWhiteSpace(text) ? null : Number(text, property, property, positive: positive);
    private double Number(string text, string property, string label, double min = double.NegativeInfinity, double max = double.PositiveInfinity, bool positive = false)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            throw Invalid(property, label + "必须为有限数字，小数点使用 .。");
        if (value < min || value > max || (positive && value <= 0))
            throw Invalid(property, label + (positive && double.IsPositiveInfinity(max) ? "必须大于 0。" : $"超出允许范围：{(positive ? "(" : "[")}{min}, {max}]。"));
        return value;
    }
    private ArgumentException Invalid(string property, string message) { ErrorProperty = property; return new ArgumentException(message); }
}
