#pragma warning disable CA1707
using ColorVision.Common.MVVM;
using cvColorVision;
using System;
using System.ComponentModel;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    /// <summary>
    /// 相机模式
    /// </summary>
    public enum CameraMode
    {
        [Description("CV_MODE")]
        CV_MODE,
        [Description("BV_MODE")]
        BV_MODE,
        [Description("LV_MODE")]
        LV_MODE,
        [Description("LVTOBV_MODE")]
        LVTOBV_MODE,
    };
    /// <summary>
    /// 相机型号
    /// </summary>
    public enum CameraModel
    {
        [Description("QHY_USB")]
        QHY_USB,
        [Description("HK_USB")]
        HK_USB,
        [Description("HK_CARD")]
        HK_CARD,
        [Description("MIL_CL_CARD")]
        MIL_CL_CARD,
        [Description("MIL_CXP_CARD")]
        MIL_CXP_CARD,
        [Description("NN_USB")]
        NN_USB,
        [Description("TOUP_USB")]
        TOUP_USB,
        [Description("HK_FG_CARD")]
        HK_FG_CARD,
        [Description("CameraModel_Total")]
        CameraModel_Total,
    };

    /// <summary>
    /// 相机配置
    /// </summary>
    public class ConfigPhyCamera : ViewModelBase
    {

        public string CameraID { get => _CameraID; set { _CameraID = value; NotifyPropertyChanged(); } }
        private string _CameraID;
        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code;

        public CameraMode CameraMode { get => _CameraMode; set { if (_CameraMode == value) return; _CameraMode = value; NotifyPropertyChanged();  } }
        private CameraMode _CameraMode;

        public CameraModel CameraModel { get => _CameraModel; set { if (_CameraModel == value) return; _CameraModel = value; NotifyPropertyChanged(); } }
        private CameraModel _CameraModel;

        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; NotifyPropertyChanged(); } }
        private TakeImageMode _TakeImageMode;

        public ImageBpp ImageBpp { get => _ImageBpp; set { _ImageBpp = value; NotifyPropertyChanged(); } }
        private ImageBpp _ImageBpp;
        public ImageChannel Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); } }
        private ImageChannel _Channel;

        public MotorConfig MotorConfig { get; set; } = new MotorConfig();
        public PhyCameraCfg CameraCfg { get; set; } = new PhyCameraCfg();
        public CFWPORT CFW { get; set; } = new CFWPORT();
        public FileSeviceConfig FileServerCfg { get => _FileServerCfg; set { _FileServerCfg = value; NotifyPropertyChanged(); } } 
        private FileSeviceConfig _FileServerCfg = new FileSeviceConfig();
    }

    public class FileSeviceConfig :ViewModelBase
    {
        public string FileBasePath { get => _FileBasePath; set { _FileBasePath = value; NotifyPropertyChanged(); } }
        private string _FileBasePath = "D:\\CVTest";
        /// <summary>
        /// 端口地址
        /// </summary>
        public string Endpoint { get => _Endpoint; set { _Endpoint = value; NotifyPropertyChanged(); } }
        private string _Endpoint = "127.0.0.1";
        /// <summary>
        /// 端口范围
        /// </summary>
        public string PortRange { get => _PortRange; set { _PortRange = value; NotifyPropertyChanged(); } }
        private string _PortRange = ((Func<string>)(() => { int fromPort = Math.Abs(new Random().Next()) % 99 + 6600; return string.Format("{0}-{1}", fromPort, fromPort + 5); }))();


    }
}