using ColorVision.Common.MVVM;
using cvColorVision;
using System;
using System.ComponentModel;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class ConfigPhyCamera : ViewModelBase
    {
        public bool UpdateCameraModeAndIBM(CameraType eCamType)
        {
            switch (eCamType)
            {
                case CameraType.CV_Q:
                    CameraMode = CameraMode.CV_MODE;
                    CameraModel = CameraModel.QHY_USB;
                    break;
                case CameraType.LV_Q:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.QHY_USB;
                    break;
                case CameraType.BV_Q:
                    CameraMode = CameraMode.BV_MODE;
                    CameraModel = CameraModel.QHY_USB;
                    break;
                case CameraType.MIL_CL:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.MIL_CL_CARD;
                    break;
                case CameraType.MIL_CXP:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.MIL_CXP_CARD;
                    break;
                case CameraType.BV_H:
                    CameraMode = CameraMode.BV_MODE;
                    CameraModel = CameraModel.HK_USB;
                    break;
                case CameraType.LV_H:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.HK_USB;
                    break;
                case CameraType.HK_CXP:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.HK_CARD;
                    break;
                case CameraType.LV_MIL_CL:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.MIL_CL_CARD;
                    break;
                case CameraType.CV_MIL_CL:
                    CameraMode = CameraMode.CV_MODE;
                    CameraModel = CameraModel.MIL_CL_CARD;
                    break;
                case CameraType.BV_MIL_CXP:
                    CameraMode = CameraMode.BV_MODE;
                    CameraModel = CameraModel.MIL_CXP_CARD;
                    break;
                case CameraType.BV_HK_CARD:
                    CameraMode = CameraMode.BV_MODE;
                    CameraModel = CameraModel.QHY_USB;
                    break;
                case CameraType.LV_HK_CARD:
                    CameraMode = CameraMode.LV_MODE;
                    CameraModel = CameraModel.HK_CARD;
                    break;
                case CameraType.CV_HK_CARD:
                    CameraMode = CameraMode.CV_MODE;
                    CameraModel = CameraModel.HK_CARD;
                    break;
                case CameraType.CV_HK_USB:
                    CameraMode = CameraMode.CV_MODE;
                    CameraModel = CameraModel.HK_USB;
                    break;
                case CameraType.CameraType_Total:
                    // Process CameraType_Total case if needed
                    break;
                default:
                    // Process default case if needed
                    break;
            }
            return false;
        }
        public string CameraID { get => _CameraID; set { _CameraID = value; OnPropertyChanged(); } }
        private string _CameraID;
        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code;

        public CameraType CameraType { get => _CameraType; set { if (_CameraType == value) return; _CameraType = value; OnPropertyChanged(); } }
        private CameraType _CameraType = CameraType.BV_Q;

        public CameraMode CameraMode { get => _CameraMode; set { if (_CameraMode == value) return; _CameraMode = value; OnPropertyChanged(); } }
        private CameraMode _CameraMode;

        public CameraModel CameraModel { get => _CameraModel; set { if (_CameraModel == value) return; _CameraModel = value; OnPropertyChanged();  } }
        private CameraModel _CameraModel;

        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; OnPropertyChanged(); } }
        private TakeImageMode _TakeImageMode;

        public ImageBpp ImageBpp { get => _ImageBpp; set { _ImageBpp = value; OnPropertyChanged(); } }
        private ImageBpp _ImageBpp;
        public ImageChannel Channel { get => _Channel; set { _Channel = value; OnPropertyChanged(); } }
        private ImageChannel _Channel;

        public MotorConfig MotorConfig { get; set; } = new MotorConfig();
        public PhyCameraCfg CameraCfg { get; set; } = new PhyCameraCfg();
        public CFWPORT CFW { get; set; } = new CFWPORT();
        public FileSeviceConfig FileServerCfg { get => _FileServerCfg; set { _FileServerCfg = value; OnPropertyChanged(); } } 
        private FileSeviceConfig _FileServerCfg = new FileSeviceConfig();

        public CameraParameterLimit CameraParameterLimit { get; set; } = new CameraParameterLimit();

    }

    public class CameraParameterLimit : ViewModelBase
    {
        [DisplayName("增益默认值")]
        public float GainDefault { get => _GainDefault; set { _GainDefault = value; OnPropertyChanged(); } }
        private float _GainDefault = 10;
        [DisplayName("增益最小值")]
        public float GainMin { get => _GainMin; set { _GainMin = value; OnPropertyChanged(); } }
        private float _GainMin;
        [DisplayName("增益最大值")]
        public float GainMax { get => _GainMax; set { _GainMax = value; OnPropertyChanged(); } }
        private float _GainMax = 100;

        [DisplayName("曝光默认值")]
        public float ExpDefalut { get => _ExpDefalut; set { _ExpDefalut = value; OnPropertyChanged(); } }
        private float _ExpDefalut = 100;

        [DisplayName("曝光最小值")]
        public float ExpMin { get => _ExpMin; set { _ExpMin = value; OnPropertyChanged(); } }
        private float _ExpMin = 1;
        [DisplayName("曝光最大值")]
        public float ExpMax { get => _ExpMax; set { _ExpMax = value; OnPropertyChanged(); } }
        private float _ExpMax = 60000;

    }

    public class FileSeviceConfig :ViewModelBase
    {
        [DisplayName("数据存储路径"),PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string FileBasePath { get => _FileBasePath; set { _FileBasePath = value; OnPropertyChanged(); } }
        private string _FileBasePath = "D:\\CVTest";
        /// <summary>
        /// 端口地址
        /// </summary>
        [DisplayName("端口地址")]
        public string Endpoint { get => _Endpoint; set { _Endpoint = value; OnPropertyChanged(); } }
        private string _Endpoint = "127.0.0.1";
        /// <summary>
        /// 端口范围
        /// </summary>
        [DisplayName("端口范围")]
        public string PortRange { get => _PortRange; set { _PortRange = value; OnPropertyChanged(); } }
        private string _PortRange = ((Func<string>)(() => { int fromPort = Math.Abs(new Random().Next()) % 99 + 6600; return string.Format("{0}-{1}", fromPort, fromPort + 5); }))();


    }
}