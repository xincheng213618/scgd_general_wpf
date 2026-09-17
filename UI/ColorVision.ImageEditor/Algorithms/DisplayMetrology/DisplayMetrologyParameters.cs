using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms;

/// <summary>Pixel-domain evaluation parameters; no implicit photometric or angular calibration.</summary>
public abstract class DisplayMetrologyParameters : StandardAlgorithmParameters
{
    [Category("输入"), DisplayName("输入解码指数"), Description("线性图填 1；仅在已知编码为幂函数时填写对应指数。不能自动推断亮度标定。")]
    public double DecodeExponent { get; set; } = 1;

    [Category("输入"), DisplayName("测量通道 (B=0/G=1/R=2)"), Description("灰度图固定取唯一通道；彩色图按此通道取相对信号，不输出 CIE 亮度或色度。")]
    public int Channel { get; set; } = 1;

    public override AlgorithmValidationResult Validate()
    {
        var result = AlgorithmValidationResult.Valid();
        Range(result, nameof(DecodeExponent), DecodeExponent, 0.1, 5);
        Range(result, nameof(Channel), Channel, 0, 2);
        return result;
    }
}

public class DisplayGridParameters : DisplayMetrologyParameters
{
    [Category("图案网格"), DisplayName("列数")]
    public int Columns { get; set; } = 3;
    [Category("图案网格"), DisplayName("行数")]
    public int Rows { get; set; } = 3;
    [Category("图案网格"), DisplayName("最小信号跨度 (满量程比例)")]
    public double MinimumContrast { get; set; } = 0.02;

    public override AlgorithmValidationResult Validate()
    {
        var result = base.Validate();
        Range(result, nameof(Columns), Columns, 1, 16);
        Range(result, nameof(Rows), Rows, 1, 16);
        Range(result, nameof(MinimumContrast), MinimumContrast, 0.000001, 1);
        return result;
    }
}

public sealed class RgbRegistrationParameters : DisplayGridParameters
{
    [Category("图案网格"), DisplayName("目标阈值比例"), Description("每格必须只有一个独立亮目标，阈值相对于本格背景到峰值。")]
    public double TargetThreshold { get; set; } = 0.5;
    public override AlgorithmValidationResult Validate()
    {
        var result = base.Validate();
        Range(result, nameof(TargetThreshold), TargetThreshold, 0.1, 0.9);
        return result;
    }
}

public sealed class RgbCrossRegistrationParameters : DisplayGridParameters
{
    [Category("十字检测"), DisplayName("前景阈值比例"), Description("全图候选及各独立臂截面相对背景到峰值的阈值。")]
    public double TargetThreshold { get; set; } = 0.5;

    [Category("十字检测"), DisplayName("最小十字跨度比例"), Description("十字候选宽高在自动目标 ROI 中至少覆盖的比例。")]
    public double MinimumArmSpanFraction { get; set; } = 0.35;

    [Category("十字检测"), DisplayName("轴带支持阈值"), Description("相对最强行/列强度投影检查唯一窄轴带；最终边缘由独立臂截面测量。")]
    public double AxisBandThreshold { get; set; } = 0.5;

    [Category("十字检测"), DisplayName("最小臂截面覆盖率"), Description("每条半臂有效截面至少占采样截面的比例；属于检测参数，不是产品合格阈值。")]
    public double MinimumArmCoverage { get; set; } = 0.5;

    [Category("分离判定"), DisplayName("允许的最大边缘分离 (px)"), Description("三通道对应水平/垂直边缘的最大极差；留空只测量，指定产品规格后才给出 OK/NG。")]
    public double? MaximumEdgeSeparationPixels { get; set; }

    public RgbCrossRegistrationParameters()
    {
        Columns = 3;
        Rows = 3;
    }

    public override AlgorithmValidationResult Validate()
    {
        var result = base.Validate();
        if (Columns != 3 || Rows != 3)
            result.Add("Grid", "nine_point_grid_required", "九点十字 RGB 分离固定使用 3×3 网格。");
        Range(result, nameof(TargetThreshold), TargetThreshold, 0.1, 0.9);
        Range(result, nameof(MinimumArmSpanFraction), MinimumArmSpanFraction, 0.1, 0.9);
        Range(result, nameof(AxisBandThreshold), AxisBandThreshold, 0.1, 0.9);
        Range(result, nameof(MinimumArmCoverage), MinimumArmCoverage, 0.1, 1);
        if (MaximumEdgeSeparationPixels is double limit) Range(result, nameof(MaximumEdgeSeparationPixels), limit, 0, 1000);
        return result;
    }
}

public sealed class BinocularQualityParameters : DisplayGridParameters
{
    [Category("测量"), DisplayName("测量目标位置"), Description("启用时每格须有一个亮目标；关闭后可比较左右均匀场的分区相对信号。两图须使用相同采集条件及坐标方向。")]
    public bool MeasureAlignment { get; set; } = true;
}

public sealed class GhostMeasurementParameters : DisplayMetrologyParameters
{
    [Category("主像区域"), DisplayName("左边界 (0..1)")]
    public double PrimaryX { get; set; } = 0.35;
    [Category("主像区域"), DisplayName("上边界 (0..1)")]
    public double PrimaryY { get; set; } = 0.35;
    [Category("主像区域"), DisplayName("宽度 (0..1)")]
    public double PrimaryWidth { get; set; } = 0.3;
    [Category("主像区域"), DisplayName("高度 (0..1)")]
    public double PrimaryHeight { get; set; } = 0.3;
    [Category("检测"), DisplayName("候选阈值 / 主像峰值")]
    public double RelativeThreshold { get; set; } = 0.03;
    [Category("检测"), DisplayName("最小候选面积 (px²)")]
    public int MinimumArea { get; set; } = 3;
    [Category("检测"), DisplayName("背景信号 (满量程比例)"), Description("使用独立暗场或已知空白区确定背景；从整幅图估计会把宽域泛光一起扣除。")]
    public double Background { get; set; }
    public override AlgorithmValidationResult Validate()
    {
        var result = base.Validate();
        Range(result, nameof(PrimaryX), PrimaryX, 0, 1);
        Range(result, nameof(PrimaryY), PrimaryY, 0, 1);
        Range(result, nameof(PrimaryWidth), PrimaryWidth, 0.001, 1);
        Range(result, nameof(PrimaryHeight), PrimaryHeight, 0.001, 1);
        if (PrimaryX + PrimaryWidth > 1 || PrimaryY + PrimaryHeight > 1)
            result.Add("primary", "outside_image", "主像区域必须完整位于图内。");
        Range(result, nameof(RelativeThreshold), RelativeThreshold, 0.0001, 1);
        Range(result, nameof(MinimumArea), MinimumArea, 1, 100_000);
        Range(result, nameof(Background), Background, 0, 0.99);
        return result;
    }
}

public sealed class DisplayDefectParameters : DisplayMetrologyParameters
{
    [Category("背景"), DisplayName("背景平滑尺度 (px)"), Description("必须大于待检 Mura 的特征尺度；输入须裁到有效均匀发光区。")]
    public int BackgroundRadius { get; set; } = 31;
    [Category("阈值"), DisplayName("局部偏差比例")]
    public double RelativeThreshold { get; set; } = 0.08;
    [Category("阈值"), DisplayName("绝对偏差下限 (满量程比例)"), Description("低灰阶仍保留绝对噪声下限；默认约为 8-bit 的 1 个灰阶。")]
    public double AbsoluteThreshold { get; set; } = 1.0 / 255;
    [Category("阈值"), DisplayName("亮暗点最大面积 (px²)")]
    public int MaximumPointArea { get; set; } = 25;
    [Category("阈值"), DisplayName("Mura 最小面积 (px²)")]
    public int MinimumMuraArea { get; set; } = 100;
    [Category("阈值"), DisplayName("线缺陷最小长度 (px)")]
    public int MinimumLineLength { get; set; } = 32;
    [Category("阈值"), DisplayName("线缺陷最小长宽比")]
    public double MinimumLineAspect { get; set; } = 8;
    public override AlgorithmValidationResult Validate()
    {
        var result = base.Validate();
        Range(result, nameof(BackgroundRadius), BackgroundRadius, 3, 128);
        Range(result, nameof(RelativeThreshold), RelativeThreshold, 0.001, 1);
        Range(result, nameof(AbsoluteThreshold), AbsoluteThreshold, 0.000001, 1);
        Range(result, nameof(MaximumPointArea), MaximumPointArea, 1, 10_000);
        Range(result, nameof(MinimumMuraArea), MinimumMuraArea, 2, 1_000_000);
        Range(result, nameof(MinimumLineLength), MinimumLineLength, 3, 100_000);
        Range(result, nameof(MinimumLineAspect), MinimumLineAspect, 2, 1000);
        if (MinimumMuraArea <= MaximumPointArea)
            result.Add(nameof(MinimumMuraArea), "overlapping_classes", "Mura 最小面积必须大于亮暗点最大面积。");
        return result;
    }
}

public sealed class EyeboxScanParameters : DisplayMetrologyParameters
{
    [Category("扫描网格"), DisplayName("列数")]
    public int Columns { get; set; } = 3;
    [Category("扫描网格"), DisplayName("行数")]
    public int Rows { get; set; } = 3;
    [Category("扫描网格"), DisplayName("X 步距 (mm)")]
    public double StepXMillimeters { get; set; } = 1;
    [Category("扫描网格"), DisplayName("Y 步距 (mm)")]
    public double StepYMillimeters { get; set; } = 1;
    [Category("扫描网格"), DisplayName("参考帧序号 (从 0 开始)")]
    public int ReferenceIndex { get; set; } = 4;
    [Category("判定"), DisplayName("最低平均信号 / 参考帧")]
    public double MinimumMeanRatio { get; set; } = 0.5;
    [Category("判定"), DisplayName("像素最低信号 / 参考帧对应像素")]
    public double MinimumPixelRatio { get; set; } = 0.5;
    [Category("判定"), DisplayName("最低有效视场覆盖比例")]
    public double MinimumCoverage { get; set; } = 0.9;
    [Category("判定"), DisplayName("参考帧最低有效信号")]
    public double ReferenceSignalFloor { get; set; } = 0.01;
    public override AlgorithmValidationResult Validate()
    {
        var result = base.Validate();
        Range(result, nameof(Columns), Columns, 2, 9);
        Range(result, nameof(Rows), Rows, 2, 9);
        Range(result, nameof(StepXMillimeters), StepXMillimeters, 0.001, 100);
        Range(result, nameof(StepYMillimeters), StepYMillimeters, 0.001, 100);
        Range(result, nameof(ReferenceIndex), ReferenceIndex, 0, (double)Columns * Rows - 1);
        Range(result, nameof(MinimumMeanRatio), MinimumMeanRatio, 0.001, 1);
        Range(result, nameof(MinimumPixelRatio), MinimumPixelRatio, 0.001, 1);
        Range(result, nameof(MinimumCoverage), MinimumCoverage, 0.001, 1);
        Range(result, nameof(ReferenceSignalFloor), ReferenceSignalFloor, 0.000001, 0.99);
        return result;
    }
}

public sealed class FieldSfrParameters : DisplayGridParameters
{
    [Category("斜边"), DisplayName("水平边缘"), Description("默认每格为接近竖直的单条斜边；启用后先转置每格再测量。斜率须为 0.02..0.35。")]
    public bool HorizontalEdge { get; set; }
}
