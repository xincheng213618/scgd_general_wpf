using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.Devices.Camera.Video;
using ColorVision.Engine.Services.PhyCameras.Configs;
using cvColorVision;
using FlowEngineLib.Algorithm;
using Newtonsoft.Json;
using System;

namespace ColorVision.Engine.Services.Devices.Camera.Configs
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class ConfigCamera : DeviceServiceConfig, IFileServerCfg
    {
        public string? CameraCode { get => _CameraCode; set { _CameraCode = value; OnPropertyChanged();  } }
        private string? _CameraCode;

        public string CameraID { get => _CameraID; set { _CameraID = value; OnPropertyChanged();} }
        private string _CameraID;
        public CameraType CameraType { get => _CameraType; set { if (_CameraType == value) return; _CameraType = value; OnPropertyChanged();} }
        private CameraType _CameraType;
        public CameraMode CameraMode { get => _CameraMode; set { if (_CameraMode == value) return; _CameraMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsExpThree)); } }
        private CameraMode _CameraMode = CameraMode.BV_MODE;

        public CameraModel CameraModel { get => _CameraModel; set { if (_CameraModel == value) return; _CameraModel = value; OnPropertyChanged();  } }
        private CameraModel _CameraModel;

        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; OnPropertyChanged(); } }
        private TakeImageMode _TakeImageMode;

        public ImageBpp ImageBpp { get => _ImageBpp; set { _ImageBpp = value; OnPropertyChanged(); } }
        private ImageBpp _ImageBpp;
        public ImageChannel Channel { get => _Channel; set { _Channel = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsExpThree)); OnPropertyChanged(nameof(IsChannelThree)); } }
        private ImageChannel _Channel;

        public CameraVideoConfig VideoConfig { get; set; } = new CameraVideoConfig();

        public int AvgCount { get => _AvgCount; set { _AvgCount = value; OnPropertyChanged(); } }
        private int _AvgCount = 1;

        public CVImageFlipMode FlipMode { get => _FlipMode; set { _FlipMode = value; OnPropertyChanged(); } }
        private CVImageFlipMode _FlipMode = CVImageFlipMode.None;

        public bool UsingFileCaching { get => _UsingFileCaching; set { _UsingFileCaching = value; OnPropertyChanged(); } }
        private bool _UsingFileCaching;

        public bool IsCVCIEFileSave { get => _IsCVCIEFileSave; set { _IsCVCIEFileSave = value; OnPropertyChanged(); } }
        private bool _IsCVCIEFileSave = true;

        public float Gain { get => _Gain; set { _Gain = value; OnPropertyChanged(); } }
        private float _Gain = 10;

        public double ScaleFactor { get => _ScaleFactor;set { _ScaleFactor = value; OnPropertyChanged(); } }
        private double _ScaleFactor = 1.0;

        public string  ScaleFactorUnit { get => _ScaleFactorUnit; set { _ScaleFactorUnit = value; OnPropertyChanged(); } }
        private string _ScaleFactorUnit = "Px";

        [JsonIgnore]
        public bool IsExpThree
        {
            get => TakeImageMode != TakeImageMode.Live && (CameraMode == CameraMode.CV_MODE);
            set => OnPropertyChanged();
        }
        [JsonIgnore]
        public bool IsChannelThree
        {
            get => Channel == ImageChannel.Three;
            set => OnPropertyChanged();
        }

        public bool IsAutoExpose { get => _IsAutoExpose; set { _IsAutoExpose =value; OnPropertyChanged(); } }
        private bool _IsAutoExpose;

        public double ExpTime { get => _ExpTime; set { _ExpTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpTimeLog)); } }
        private double _ExpTime = 100;

        public double ExpTimeLog { get => Math.Log(ExpTime); set { ExpTime = Math.Pow(Math.E, value); } }

        public double ExpTimeMax { get => _ExpTimeMax; set { _ExpTimeMax = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpTimeMaxLog)); } }
        private double _ExpTimeMax = 60000;

        public double ExpTimeMaxLog { get => Math.Log(ExpTimeMax); set { ExpTimeMax = Math.Pow(Math.E, value); } }

        public double ExpTimeMin { get => _ExpTimeMin; set { _ExpTimeMin = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpTimeMinLog)); } }
        private double _ExpTimeMin = 1;

        public double ExpTimeMinLog { get => Math.Log(ExpTimeMin); set { ExpTimeMin = Math.Pow(Math.E, value); } }

        public double ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpTimeRLog)); } }
        private double _ExpTimeR = 100;

        public double ExpTimeRLog { get => Math.Log(ExpTimeR); set { ExpTimeR = Math.Pow(Math.E, value); } }

        public double ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpTimeGLog)); } }
        private double _ExpTimeG = 100;
        public double ExpTimeGLog { get => Math.Log(ExpTimeG); set { ExpTimeG = Math.Pow(Math.E, value); } }

        public double ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExpTimeBLog)); } }
        private double _ExpTimeB = 100;

        public double ExpTimeBLog { get => Math.Log(ExpTimeB); set { ExpTimeB = Math.Pow(Math.E, value); } }


        public double Saturation { get => _Saturation; set { _Saturation = value; OnPropertyChanged(); } }
        private double _Saturation = -1;

        public double SaturationR { get => _SaturationR; set { _SaturationR = value; OnPropertyChanged(); } }
        private double _SaturationR = -1;

        public double SaturationG { get => _SaturationG; set { _SaturationG = value; OnPropertyChanged(); } }
        private double _SaturationG = -1;

        public double SaturationB { get => _SaturationB; set { _SaturationB = value; OnPropertyChanged(); } }
        private double _SaturationB = -1;

        public CFWPORT CFW { get => _CFW; set { _CFW = value; OnPropertyChanged(); } }
        private CFWPORT _CFW = new CFWPORT();

        public MotorConfig MotorConfig { get => _MotorConfig; set { _MotorConfig = value; OnPropertyChanged(); } }
        private MotorConfig _MotorConfig = new MotorConfig();

        public AutoFocusParam AutoFocusConfig { get; set; } = new AutoFocusParam();

        public PhyExpTimeCfg ExpTimeCfg { get; set; } = new PhyExpTimeCfg();

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; OnPropertyChanged(); } }
        private bool _IsAutoOpen = true;


        public ZBDebayer ZBDebayer { get => _ZBDebayer; set { _ZBDebayer = value; OnPropertyChanged(); } }
        private ZBDebayer _ZBDebayer = new ZBDebayer();


    }

}