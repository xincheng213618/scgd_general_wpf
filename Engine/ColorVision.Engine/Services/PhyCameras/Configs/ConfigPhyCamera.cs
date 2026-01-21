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

        public string CameraID { get => _CameraID; set { _CameraID = value; OnPropertyChanged(); } }
        private string _CameraID;
        public string Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string _Code;

        public CameraType CameraType { get => _CameraType; set { if (_CameraType == value) return; _CameraType = value; OnPropertyChanged(); } }
        private CameraType _CameraType = CameraType.BV_Q;

        public CameraMode CameraMode { get => _CameraMode; set { if (_CameraMode == value) return; _CameraMode = value; OnPropertyChanged(); } }
        private CameraMode _CameraMode = CameraMode.BV_MODE;

        public CameraModel CameraModel { get => _CameraModel; set { if (_CameraModel == value) return; _CameraModel = value; OnPropertyChanged();  } }
        private CameraModel _CameraModel = CameraModel.QHY_USB;

        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; OnPropertyChanged(); } }
        private TakeImageMode _TakeImageMode = TakeImageMode.Measure_Normal;

        public ImageBpp ImageBpp { get => _ImageBpp; set { _ImageBpp = value; OnPropertyChanged(); } }
        private ImageBpp _ImageBpp = ImageBpp.bpp16;
        public ImageChannel Channel { get => _Channel; set { _Channel = value; OnPropertyChanged(); } }
        private ImageChannel _Channel = ImageChannel.Three;

        public MotorConfig MotorConfig { get; set; } = new MotorConfig();
        public PhyCameraCfg CameraCfg { get; set; } = new PhyCameraCfg();
        public CFWPORT CFW { get; set; } = new CFWPORT();
        public FileSeviceConfig FileServerCfg { get => _FileServerCfg; set { _FileServerCfg = value; OnPropertyChanged(); } } 
        private FileSeviceConfig _FileServerCfg = new FileSeviceConfig();

        public CameraParameterLimit CameraParameterLimit { get; set; } = new CameraParameterLimit();

        public FilterWheelConfig FilterWheelConfig { get; set; } = new FilterWheelConfig();

        public int Fileversion { get => _Fileversion; set { _Fileversion = value; OnPropertyChanged(); } }
        private int _Fileversion = 2;

    }

    public class CameraParameterLimit : ViewModelBase
    {
        [DisplayName("DefaultGain")]
        public float GainDefault { get => _GainDefault; set { _GainDefault = value; OnPropertyChanged(); } }
        private float _GainDefault = 10;
        [DisplayName("MinGain")]
        public float GainMin { get => _GainMin; set { _GainMin = value; OnPropertyChanged(); } }
        private float _GainMin;
        [DisplayName("MaxGain")]
        public float GainMax { get => _GainMax; set { _GainMax = value; OnPropertyChanged(); } }
        private float _GainMax = 100;

        [DisplayName("DefaultExpTime")]
        public float ExpDefalut { get => _ExpDefalut; set { _ExpDefalut = value; OnPropertyChanged(); } }
        private float _ExpDefalut = 100;

        [DisplayName("MinExpTime")]
        public float ExpMin { get => _ExpMin; set { _ExpMin = value; OnPropertyChanged(); } }
        private float _ExpMin = 1;
        [DisplayName("MaxExpTime")]
        public float ExpMax { get => _ExpMax; set { _ExpMax = value; OnPropertyChanged(); } }
        private float _ExpMax = 60000;

    }

    public class FileSeviceConfig :ViewModelBase
    {
        [DisplayName("DataSavePath"),PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string FileBasePath { get => _FileBasePath; set { _FileBasePath = value; OnPropertyChanged(); } }
        private string _FileBasePath = "D:\\CVTest";
        /// <summary>
        /// 端口地址
        /// </summary>
        [DisplayName("PortAddress")]
        public string Endpoint { get => _Endpoint; set { _Endpoint = value; OnPropertyChanged(); } }
        private string _Endpoint = "127.0.0.1";
        /// <summary>
        /// 端口范围
        /// </summary>
        [DisplayName("PortRange")]
        public string PortRange { get => _PortRange; set { _PortRange = value; OnPropertyChanged(); } }
        private string _PortRange = ((Func<string>)(() => { int fromPort = Math.Abs(new Random().Next()) % 99 + 6600; return string.Format("{0}-{1}", fromPort, fromPort + 5); }))();


    }
}