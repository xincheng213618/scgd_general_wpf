using ColorVision.SettingUp;
using System.ComponentModel;

namespace ColorVision.Device.Camera
{
    public enum CameraType
    {
        [Description("CV_Q")]
        CVQ,
        [Description("LV_Q")]
        LVQ,
        [Description("BV_Q")]
        BVQ,
        [Description("MIL_CL")]
        MILCL,
        [Description("MIL_CXP")]
        MILCXP,
        [Description("BV_H")]
        BVH,
        [Description("LV_H")]
        LVH,
        [Description("HK_CXP")]
        HKCXP,
        [Description("LV_MIL_CL")]
        LVMILCL,
        [Description("MIL_CXP_VIDEO")]
        MILCXPVIDEO,
        [Description("CameraType_Total")]
        CameraTypeTotal,
    };

    public enum TakeImageMode
    {
        [Description("Measure_Normal")]
        Normal = 0,
        [Description("Live")]
        Live,
        [Description("Measure_Fast")]
        Fast,
        [Description("Measure_FastEx")]
        FastExt
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
        [Description("8Bits")]
        Gray = 8,
        [Description("16Bits")]
        Three = 16,
    }

    /// <summary>
    /// 相机配置
    /// </summary>
    public class CameraConfig : BaseDeviceConfig
    {
        public CameraType CameraType { get => _CameraType; set { _CameraType = value; NotifyPropertyChanged(); } }
        private CameraType _CameraType;

        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; NotifyPropertyChanged(); } }
        private TakeImageMode _TakeImageMode;

        public int ImageBpp { get => _ImageBpp; set { _ImageBpp = value; NotifyPropertyChanged(); } }
        private int _ImageBpp;
        public ImageChannel Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); } }
        private ImageChannel _Channel;

        public double DefalutGain { get => _DefalutGain; set { _DefalutGain = value; NotifyPropertyChanged(); } }
        private double _DefalutGain;

        public int DefalutExpTime { get => _DefalutExpTime; set { _DefalutExpTime = value; NotifyPropertyChanged(); } }
        private int _DefalutExpTime;

        public CameraVideoConfig VideoConfig { get; set; } = new CameraVideoConfig();

    }
}
