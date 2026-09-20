using ColorVision.Core;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

/// <summary>A single property-editor transaction over the existing display and ROI settings.</summary>
public sealed class BmwSfrViewSettings
{
    [Category("显示与指标"), DisplayName("图像标注与指标")]
    public BmwSfrOverlaySettings Display { get; set; } = new();

    [Category("四边测量框"), DisplayName("测量框尺寸与位置"), Description("修改后重新定位并计算四边；各项为 0 时保留自动值。")]
    public BmwSfrRoiSettings MeasurementRoi { get; set; } = new();

    public void Validate()
    {
        if (Display == null || MeasurementRoi == null) throw new ArgumentException("显示与测量框设置不能为空。");
        Display.Validate();
        MeasurementRoi.Validate();
    }
}
