#pragma warning disable CA1707

using System.ComponentModel;

namespace cvColorVision
{
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
        [Description("BV_MIL_CXP")]
        BV_MIL_CXP,
        [Description("BV_HK_CARD")]
        BV_HK_CARD,
        [Description("LV_HK_CARD")]
        LV_HK_CARD,
        [Description("CV_HK_CARD")]
        CV_HK_CARD,
        [Description("CV_HK_USB")]
        CV_HK_USB,
        [Description("MIL_CXP_VIDEO")]
        MIL_CXP_VIDEO,
        [Description("CameraType_Total")]
        CameraType_Total,
    };

}
