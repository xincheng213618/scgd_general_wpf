#pragma warning disable CS8603,CS0649
using System.ComponentModel;

namespace ColorVision.Services.Device.Camera.Calibrations
{
    public enum ResouceType
    {
        [Description("暗噪声")]
        DarkNoise = 31,
        [Description("缺陷点")]
        DefectPoint = 32,
        [Description("DSNU")]
        DSNU = 33,
        [Description("均匀场")]
        Uniformity = 34,
        [Description("畸变")]
        Distortion = 35,
        [Description("色偏")]
        ColorShift = 36,
        [Description("亮度")]
        Luminance = 37,
        [Description("单色")]
        LumOneColor = 38,
        [Description("四色")]
        LumFourColor = 39,
        [Description("多色")]
        LumMultiColor = 40,
    }


}
