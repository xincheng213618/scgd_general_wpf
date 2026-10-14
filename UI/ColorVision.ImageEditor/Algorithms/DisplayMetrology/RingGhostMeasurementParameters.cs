using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms;

public enum RingGhostOrderMapping
{
    [Description("未确认（只显示候选亮度比）")]
    Unconfirmed,
    [Description("已确认：外侧候选为一阶，内侧为二阶")]
    OuterIsFirst,
    [Description("已确认：内侧候选为一阶，外侧为二阶")]
    InnerIsFirst,
}

/// <summary>Detection scales and thresholds are estimated from the image, not operator tuning.</summary>
public sealed class RingGhostMeasurementParameters : DisplayMetrologyParameters
{
    [Category("测量口径"), DisplayName("鬼影阶次对应"), Description("半径顺序不能确定物理反射阶次。仅在客户已确认两个环的对应关系后选择；仅检出一个环时仍不自动赋阶。默认无需设置即可查看候选亮度比。")]
    public RingGhostOrderMapping OrderMapping { get; set; }

    public override AlgorithmValidationResult Validate()
    {
        var result = base.Validate();
        if (!Enum.IsDefined(OrderMapping)) result.Add(nameof(OrderMapping), "invalid_order_mapping", "鬼影阶次对应无效。");
        return result;
    }
}
