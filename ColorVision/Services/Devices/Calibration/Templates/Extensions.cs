﻿using ColorVision.Services.Type;
using cvColorVision;
using System.ComponentModel;

namespace ColorVision.Services.Devices.Calibration.Templates
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
                _ => ServiceTypes.DarkNoise,
            };
        }

    }
}