#pragma warning disable CA1707

using System.ComponentModel;

namespace cvColorVision
{
    public enum CameraModeType
    {
        LV,
        BV,
        CV,
        Other
    }
    public enum CameraType
    {
        [Description("CV_Q")]
        CV_Q,
        [Description("LV_Q")]
        LV_Q,
        [Description("BV_Q")]
        BV_Q,
        [Description("MIL_CL")]
        MIL_CL,
        [Description("MIL_CXP")]
        MIL_CXP,
        [Description("BV_H")]
        BV_H,
        [Description("LV_H")]
        LV_H,
        [Description("HK_CXP")]
        HK_CXP,
        [Description("LV_MIL_CL")]
        LV_MIL_CL,
        [Description("CV_MIL_CL")]
        CV_MIL_CL,
        [Description("MIL_CXP_VIDEO")]
        MIL_CXP_VIDEO,
        [Description("CameraType_Total")]
        CameraType_Total,
    };

    public static class CameraExtensions
    {
        public static CameraModeType GetCameraModeType(this CameraType cameraType)
        {
            return cameraType switch
            {
                CameraType.CV_Q => CameraModeType.CV,
                CameraType.LV_Q or CameraType.MIL_CXP or CameraType.LV_H or CameraType.HK_CXP or CameraType.LV_MIL_CL or CameraType.MIL_CXP_VIDEO => CameraModeType.LV,
                CameraType.BV_Q or CameraType.MIL_CL or CameraType.BV_H => CameraModeType.BV,
                _ => CameraModeType.Other,
            };
        }
    }


}
