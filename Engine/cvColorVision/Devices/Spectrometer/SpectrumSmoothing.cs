using System;
using System.ComponentModel;

namespace cvColorVision;

public enum SpectrumSmoothingMethod
{
    [Description("均值滤波")]
    Mean = 0,
    [Description("SG（二阶，试验）")]
    SavitzkyGolay = 1,
}

public static class SpectrumSmoothing
{
    public static void Validate(SpectrumSmoothingMethod method, int filterWidth)
    {
        if (method is not (SpectrumSmoothingMethod.Mean or SpectrumSmoothingMethod.SavitzkyGolay))
            throw new InvalidOperationException("不支持的光谱平滑算法。");
        if (method == SpectrumSmoothingMethod.SavitzkyGolay && (filterWidth < 3 || filterWidth > 2047 || filterWidth % 2 == 0))
            throw new InvalidOperationException("SG 滤波宽度必须是 3～2047 的奇数，且不能超过原始采样点数。");
    }

    public static int Configure(IntPtr handle, SpectrumSmoothingMethod method, int filterWidth)
    {
        Validate(method, filterWidth);
        return Spectrometer.CM_Emission_SetSmoothingMethod(handle, (int)method);
    }
}
