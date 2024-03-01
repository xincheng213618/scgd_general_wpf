#pragma warning disable CS8603,CS0649
using cvColorVision;
using System.ComponentModel;

namespace ColorVision.Services.Devices.Camera.Calibrations
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

    public static class Extensions
    {
        public static ResouceType ToResouceType(this CalibrationType calibrationType)
        {
            return calibrationType switch
            {
                CalibrationType.DarkNoise => ResouceType.DarkNoise,
                CalibrationType.DefectPoint => ResouceType.DefectPoint,
                CalibrationType.DefectWPoint => ResouceType.DefectPoint,
                CalibrationType.DefectBPoint => ResouceType.DefectPoint,
                CalibrationType.DSNU => ResouceType.DSNU,
                CalibrationType.Uniformity => ResouceType.Uniformity,
                CalibrationType.Distortion => ResouceType.Distortion,
                CalibrationType.ColorShift => ResouceType.ColorShift,
                CalibrationType.Luminance => ResouceType.Luminance,
                CalibrationType.LumOneColor => ResouceType.LumOneColor,
                CalibrationType.LumFourColor => ResouceType.LumFourColor,
                CalibrationType.LumMultiColor => ResouceType.LumMultiColor,
                _ => ResouceType.DarkNoise,
            };
        }

    }
}
