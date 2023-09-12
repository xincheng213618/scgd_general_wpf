#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix

using System.ComponentModel;

namespace cvColorVision
{
    public enum  FOCUS_COMMUN
    {
        VID_SERIAL = 0,
        CANON_SERIAL,
        NED_SERIAL,
        LONGFOOT_SERIAL,
    };


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
        [Description("MIL_CXP_VIDEO")]
        MIL_CXP_VIDEO,
        [Description("CameraType_Total")]
        CameraType_Total,
    };


    public enum TakeImageMode
    {
        [Description("Measure_Normal")]
        Measure_Normal = 0,
        [Description("Live")]
        Live,
        [Description("Measure_Fast")]
        Measure_Fast,
        [Description("Measure_FastEx")]
        Measure_FastEx
    };


    public enum ImageChannel
    {
        [Description("单通道")]
        One = 1,
        [Description("三通道")]
        Three = 3
    }

    public enum ImageBpp
    {
        [Description("8位")]
        bpp8 = 8,
        [Description("16位")]
        bpp16 = 16,
    }


}
