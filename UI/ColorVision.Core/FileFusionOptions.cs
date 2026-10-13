using System;
using System.ComponentModel;

namespace ColorVision.Core;

[DisplayName("景深融合参数")]
public sealed record FileFusionOptions
{
    [Category("常用")]
    [DisplayName("清晰度窗口（像素）")]
    [Description("默认 5，范围 3–31 的奇数。小窗口保留细小纹理，大窗口使清晰度判断更平稳，但可能混合物体边缘两侧的焦点。建议先比较 3、5、7、9。")]
    public int FocusWindow { get; set; } = 5;

    [Category("常用")]
    [DisplayName("选焦可信度阈值（dB）")]
    [Description("默认 13，范围 0–60。调低更偏向最清晰帧、保留细节；调高让更多区域混合多帧、抑制噪声，但可能变软。建议先比较 10、13、16。不是曝光或亮度阈值。")]
    public double Threshold { get; set; } = 13;

    [Category("高级")]
    [DisplayName("拟合取样间隔（帧）")]
    [Description("默认 2，范围 1–1024。在清晰度峰值附近间隔多少帧取样；不是采集电机步距，也不会跳过输入图。需要至少 2×间隔+1 张；不足时改用普通清晰度加权。焦点应按顺序、等间隔采集。")]
    public int FocusStep { get; set; } = 2;

    [Category("高级")]
    [DisplayName("可信度平滑窗口（像素）")]
    [Description("默认 3，范围 3–31 的奇数。平滑拟合误差和区域可信度；调大可减少零散的选焦变化，但会扩大不同区域的过渡。与清晰度窗口分别设置。")]
    public int ConsistencyWindow { get; set; } = 3;

    [Category("高级")]
    [DisplayName("选择性系数 α")]
    [Description("默认 0.2，范围 0.01–1。通常调小更偏向最清晰帧，调大增加多帧混合；同时影响阈值过渡与权重强度，不是线性的锐化强度。建议保持默认，必要时比较 0.1、0.2、0.3。")]
    public double Alpha { get; set; } = 0.2;

    public void Validate()
    {
        if (FocusWindow < 3 || FocusWindow > 31 || FocusWindow % 2 == 0)
            throw new ArgumentOutOfRangeException(nameof(FocusWindow), "清晰度窗口必须是 3–31 的奇数。");
        if (ConsistencyWindow < 3 || ConsistencyWindow > 31 || ConsistencyWindow % 2 == 0)
            throw new ArgumentOutOfRangeException(nameof(ConsistencyWindow), "可信度平滑窗口必须是 3–31 的奇数。");
        if (FocusStep < 1 || FocusStep > 1024)
            throw new ArgumentOutOfRangeException(nameof(FocusStep), "拟合取样间隔必须在 1–1024 帧之间。");
        if (!double.IsFinite(Alpha) || Alpha < 0.01 || Alpha > 1)
            throw new ArgumentOutOfRangeException(nameof(Alpha), "选择性系数 α 必须在 0.01–1 之间。");
        if (!double.IsFinite(Threshold) || Threshold < 0 || Threshold > 60)
            throw new ArgumentOutOfRangeException(nameof(Threshold), "选焦可信度阈值必须在 0–60 dB 之间。");
    }
}
