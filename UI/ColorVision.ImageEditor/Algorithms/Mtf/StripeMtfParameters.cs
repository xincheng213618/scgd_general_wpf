using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms.Mtf;

public enum StripeMtfPattern
{
    [Description("单独 H 条纹")] Horizontal = 1,
    [Description("单独 V 条纹")] Vertical = 2,
    [Description("四部横竖条纹")] FourPart = 5
}

public enum StripeMtfMethod
{
    [Description("明暗两端均值")] TailMean = 0,
    [Description("去噪后最亮 / 最暗")] TrimmedExtrema = 1
}

/// <summary>Shared editable settings for ImageView and Flow. No database template or vendor DLL is required.</summary>
public sealed class StripeMtfParameters : ViewModelBase
{
    private StripeMtfPattern pattern = StripeMtfPattern.FourPart;
    [Category("条纹 MTF"), DisplayName("图案")]
    public StripeMtfPattern Pattern
    {
        get => pattern;
        set
        {
            if (!SetProperty(ref pattern, value)) return;
            OnPropertyChanged(nameof(IsFourPart));
            OnPropertyChanged(nameof(ShowFourPartSettings));
        }
    }
    [Browsable(false), JsonIgnore] public bool IsFourPart => Pattern == StripeMtfPattern.FourPart;
    private bool showAdvanced;
    [Category("条纹 MTF"), DisplayName("高级设置"), JsonIgnore, Description("展开计算、去噪及当前图案的定位参数。关闭仅收起参数，计算仍使用已配置的值。")]
    public bool ShowAdvanced
    {
        get => showAdvanced;
        set
        {
            if (!SetProperty(ref showAdvanced, value)) return;
            OnPropertyChanged(nameof(ShowFourPartSettings));
            OnPropertyChanged(nameof(ShowTailRatio));
        }
    }
    [Browsable(false), JsonIgnore] public bool ShowFourPartSettings => ShowAdvanced && IsFourPart;
    [Browsable(false), JsonIgnore] public bool ShowTailRatio => ShowAdvanced && IsTailMean;
    private StripeMtfMethod method;
    [Category("条纹 MTF"), DisplayName("计算方式"), PropertyVisibility(nameof(ShowAdvanced), false), Description("均值法对明暗两端取样；极值法取去噪后的端点。两者均计算 (亮−暗)/(亮+暗)。")]
    public StripeMtfMethod Method
    {
        get => method;
        set
        {
            if (!SetProperty(ref method, value)) return;
            OnPropertyChanged(nameof(IsTailMean));
            OnPropertyChanged(nameof(ShowTailRatio));
        }
    }
    [Browsable(false), JsonIgnore] public bool IsTailMean => Method == StripeMtfMethod.TailMean;
    [Category("条纹 MTF"), DisplayName("两端取样比例"), PropertyVisibility(nameof(ShowTailRatio), false), Description("均值法每端使用的像素比例；0 < 比例 ≤ 0.5。")]
    public double TailRatio { get; set; } = 0.1;
    [Category("条纹 MTF"), DisplayName("舍弃最亮比例"), PropertyVisibility(nameof(ShowAdvanced), false)]
    public double WhiteNoiseRatio { get; set; } = 0.01;
    [Category("条纹 MTF"), DisplayName("舍弃最暗比例"), PropertyVisibility(nameof(ShowAdvanced), false)]
    public double BlackNoiseRatio { get; set; } = 0.01;
    [Category("条纹 MTF"), DisplayName("输出百分数"), PropertyVisibility(nameof(ShowAdvanced), false), Description("关闭输出比例，开启输出百分数；下游判定限必须使用相同单位。")]
    public bool PercentageDisplay { get; set; }
    [Category("四部定位"), DisplayName("定位阈值"), PropertyVisibility(nameof(ShowFourPartSettings), false)]
    public int Threshold { get; set; } = 5000;
    [Category("四部定位"), DisplayName("最小目标面积"), PropertyVisibility(nameof(ShowFourPartSettings), false)]
    public int MinimumArea { get; set; } = 100;
    [Category("四部定位"), DisplayName("中心偏移 X"), PropertyVisibility(nameof(ShowFourPartSettings), false)]
    public int OffsetX { get; set; }
    [Category("四部定位"), DisplayName("中心偏移 Y"), PropertyVisibility(nameof(ShowFourPartSettings), false)]
    public int OffsetY { get; set; }
    [Category("四部定位"), DisplayName("小框中心到目标中心距离"), PropertyVisibility(nameof(ShowFourPartSettings), false)]
    public int DistanceToRect { get; set; } = 70;
    [Category("四部定位"), DisplayName("小框宽度"), PropertyVisibility(nameof(ShowFourPartSettings), false)]
    public int RectWidth { get; set; } = 60;
    [Category("四部定位"), DisplayName("小框高度"), PropertyVisibility(nameof(ShowFourPartSettings), false)]
    public int RectHeight { get; set; } = 60;
    [Category("四部定位"), DisplayName("左上框归入 H"), PropertyVisibility(nameof(ShowFourPartSettings), false), Description("保持原 firstIsHor 分组约定；编号按左上、右上、右下、左下。")]
    public bool FirstIsHorizontal { get; set; }

    public void Validate()
    {
        if (!Enum.IsDefined(Pattern) || !Enum.IsDefined(Method)) throw new ArgumentException("MTF 图案或计算方式无效。");
        if (!double.IsFinite(WhiteNoiseRatio) || WhiteNoiseRatio < 0 || WhiteNoiseRatio >= .5
            || !double.IsFinite(BlackNoiseRatio) || BlackNoiseRatio < 0 || BlackNoiseRatio >= .5
            || (Method == StripeMtfMethod.TailMean && (!double.IsFinite(TailRatio) || TailRatio <= 0 || TailRatio > .5
                || 2 * TailRatio + WhiteNoiseRatio + BlackNoiseRatio > 1)))
            throw new ArgumentException("MTF 取样比例无效：去噪及明暗取样区域不得重叠。");
        if (IsFourPart && (Threshold < 0 || Threshold > 65535 || MinimumArea <= 0 || DistanceToRect <= 0
            || RectWidth <= 0 || RectHeight <= 0)) throw new ArgumentException("MTF 四部定位参数无效。");
    }

    public JObject ToJson()
    {
        Validate();
        return JObject.FromObject(new
        {
            pattern = (int)Pattern, CalcMethod = (int)Method, dRatio = TailRatio, PercentageDisplay,
            NoiseRatio = new { white = WhiteNoiseRatio, black = BlackNoiseRatio }, threshold = Threshold,
            nV1 = new { AAminSize = MinimumArea, offsetX = OffsetX, offsetY = OffsetY, distanceToRect = DistanceToRect,
                rectWidth = RectWidth, rectHeight = RectHeight, firstIsHor = FirstIsHorizontal }
        });
    }

    public static StripeMtfParameters FromJson(string json)
    {
        JObject value = JObject.Parse(json);
        // A selected historical unsupported correction must not be silently ignored.
        if (value["mathMaskRect"]?.Value<bool>("enable") == true || value.Value<double?>("sensorRatio") is double ratio && ratio != 1)
            throw new ArgumentException("本地 MTF 不支持旧模板的额外 Mask 或 sensorRatio 校正，请使用明确的测量矩形。");
        StripeMtfParameters result = new();
        result.Pattern = (StripeMtfPattern)(value.Value<int?>("pattern") ?? (int)result.Pattern);
        result.Method = (StripeMtfMethod)(value.Value<int?>("CalcMethod") ?? (int)result.Method);
        result.TailRatio = value.Value<double?>("dRatio") ?? result.TailRatio;
        result.PercentageDisplay = value.Value<bool?>("PercentageDisplay") ?? result.PercentageDisplay;
        result.WhiteNoiseRatio = value["NoiseRatio"]?.Value<double?>("white") ?? result.WhiteNoiseRatio;
        result.BlackNoiseRatio = value["NoiseRatio"]?.Value<double?>("black") ?? result.BlackNoiseRatio;
        result.Threshold = value.Value<int?>("threshold") ?? result.Threshold;
        if (value["nV1"] is JObject n)
        {
            result.MinimumArea = n.Value<int?>("AAminSize") ?? result.MinimumArea;
            result.OffsetX = n.Value<int?>("offsetX") ?? result.OffsetX; result.OffsetY = n.Value<int?>("offsetY") ?? result.OffsetY;
            result.DistanceToRect = n.Value<int?>("distanceToRect") ?? result.DistanceToRect;
            result.RectWidth = n.Value<int?>("rectWidth") ?? result.RectWidth; result.RectHeight = n.Value<int?>("rectHeight") ?? result.RectHeight;
            result.FirstIsHorizontal = n.Value<bool?>("firstIsHor") ?? result.FirstIsHorizontal;
        }
        result.Validate();
        return result;
    }
}

public sealed record MtfRoi(string name, int x, int y, int w, int h);
