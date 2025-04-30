using ColorVision.Engine.Services.Types;
using cvColorVision;

namespace ColorVision.Engine.Services.PhyCameras.Group
{

    public static class Extensions
    {
        public static ServiceTypes ToResouceType(this CalibrationType calibrationType)
        {
            return calibrationType switch
            {
                CalibrationType.DarkNoise => ServiceTypes.DarkNoise,
                CalibrationType.DefectPoint => ServiceTypes.DefectPoint,
                CalibrationType.DefectWPoint => ServiceTypes.DefectPoint,
                CalibrationType.DefectBPoint => ServiceTypes.DefectPoint,
                CalibrationType.DSNU => ServiceTypes.DSNU,
                CalibrationType.Uniformity => ServiceTypes.Uniformity,
                CalibrationType.Distortion => ServiceTypes.Distortion,
                CalibrationType.ColorShift => ServiceTypes.ColorShift,
                CalibrationType.Luminance => ServiceTypes.Luminance,
                CalibrationType.LumOneColor => ServiceTypes.LumOneColor,
                CalibrationType.LumFourColor => ServiceTypes.LumFourColor,
                CalibrationType.LumMultiColor => ServiceTypes.LumMultiColor,
                CalibrationType.ColorDiff => ServiceTypes.ColorDiff,
                CalibrationType.LineArity => ServiceTypes.LineArity,
                _ => ServiceTypes.DarkNoise,
            };
        }
        public static CalibrationType ToCalibrationType(this ServiceTypes serviceType)
        {
            return serviceType switch
            {
                ServiceTypes.DarkNoise => CalibrationType.DarkNoise,
                ServiceTypes.DefectPoint => CalibrationType.DefectPoint,
                ServiceTypes.DSNU => CalibrationType.DSNU,
                ServiceTypes.Uniformity => CalibrationType.Uniformity,
                ServiceTypes.Distortion => CalibrationType.Distortion,
                ServiceTypes.ColorShift => CalibrationType.ColorShift,
                ServiceTypes.Luminance => CalibrationType.Luminance,
                ServiceTypes.LumOneColor => CalibrationType.LumOneColor,
                ServiceTypes.LumFourColor => CalibrationType.LumFourColor,
                ServiceTypes.LumMultiColor => CalibrationType.LumMultiColor,
                ServiceTypes.ColorDiff => CalibrationType.ColorDiff,
                ServiceTypes.LineArity => CalibrationType.LineArity,
                _ => CalibrationType.DarkNoise, // 默认值，视情况而定
            };
        }

    }
}
