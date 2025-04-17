#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix

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

    public enum  FOCUS_COMMUN
    {
        VID_SERIAL = 0,
        CANON_SERIAL,
        NED_SERIAL,
        LONGFOOT_SERIAL,
    };
    public enum ImageChannelType
    {
        CM_Unknown = -1,
        Gray_X = 0,
        Gray_Y = 1,
        Gray_Z = 2,
        Gray_N = 20,
        xCIE = 3,
        yCIE = 4,
        uCIE = 5,
        vCIE = 6,
        CIE_X = 7,
        CIE_Y = 8,
        CIE_Z = 9,
        CIE_LvCxCy = 10,
        LvCIE = 11,
    };

    public enum CalibrationType
    {
        DarkNoise = 0,
        DefectWPoint = 1,
        DefectBPoint = 2,
        DefectPoint = 3,
        DSNU = 4,
        Uniformity = 5,
        Luminance = 6,
        LumOneColor = 7,
        LumFourColor = 8,
        LumMultiColor = 9,
        LumColor = 10,
        Distortion = 11,
        ColorShift = 12,
        LineArity  =13,
        ColorDiff,
        Empty_Num,
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

    public enum FovPattern
    {
        FovCircle = 0,
        FovRectangle,
    };
    public enum FovType
    {
        Horizontal = 0,
        Vertical,
        Leaning,
    };
}
