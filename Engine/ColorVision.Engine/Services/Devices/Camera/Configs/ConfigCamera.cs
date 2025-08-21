using ColorVision.Engine.Abstractions;
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
        public string? CameraCode { get => _CameraCode; set { _CameraCode = value; NotifyPropertyChanged();  } }
        private string? _CameraCode;

        public string CameraID { get => _CameraID; set { _CameraID = value; NotifyPropertyChanged();} }
        private string _CameraID;
        public CameraType CameraType { get => _CameraType; set { if (_CameraType == value) return; _CameraType = value; NotifyPropertyChanged();} }
        private CameraType _CameraType;
        public CameraMode CameraMode { get => _CameraMode; set { if (_CameraMode == value) return; _CameraMode = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsExpThree)); } }
        private CameraMode _CameraMode = CameraMode.BV_MODE;

        public CameraModel CameraModel { get => _CameraModel; set { if (_CameraModel == value) return; _CameraModel = value; NotifyPropertyChanged();  } }
        private CameraModel _CameraModel;

        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; NotifyPropertyChanged(); } }
        private TakeImageMode _TakeImageMode;

        public ImageBpp ImageBpp { get => _ImageBpp; set { _ImageBpp = value; NotifyPropertyChanged(); } }
        private ImageBpp _ImageBpp;
        public ImageChannel Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsExpThree)); NotifyPropertyChanged(nameof(IsChannelThree)); } }
        private ImageChannel _Channel;

        public CameraVideoConfig VideoConfig { get; set; } = new CameraVideoConfig();

        public int AvgCount { get => _AvgCount; set { _AvgCount = value; NotifyPropertyChanged(); } }
        private int _AvgCount = 1;

        public CVImageFlipMode FlipMode { get => _FlipMode; set { _FlipMode = value; NotifyPropertyChanged(); } }
        private CVImageFlipMode _FlipMode = CVImageFlipMode.None;

        public bool UsingFileCaching { get => _UsingFileCaching; set { _UsingFileCaching = value; NotifyPropertyChanged(); } }
        private bool _UsingFileCaching;

        public bool IsCVCIEFileSave { get => _IsCVCIEFileSave; set { _IsCVCIEFileSave = value; NotifyPropertyChanged(); } }
        private bool _IsCVCIEFileSave = true;

        public float Gain { get => _Gain; set { _Gain = value; NotifyPropertyChanged(); } }
        private float _Gain = 10;

        public double ScaleFactor { get => _ScaleFactor;set { _ScaleFactor = value; NotifyPropertyChanged(); } }
        private double _ScaleFactor = 1.0;

        public string  ScaleFactorUnit { get => _ScaleFactorUnit; set { _ScaleFactorUnit = value; NotifyPropertyChanged(); } }
        private string _ScaleFactorUnit = "Px";

        [JsonIgnore]
        public bool IsExpThree
        {
            get => TakeImageMode != TakeImageMode.Live && (CameraMode == CameraMode.CV_MODE);
            set => NotifyPropertyChanged();
        }
        [JsonIgnore]
        public bool IsChannelThree
        {
            get => Channel == ImageChannel.Three;
            set => NotifyPropertyChanged();
        }

        public bool IsAutoExpose { get => _IsAutoExpose; set { _IsAutoExpose =value; NotifyPropertyChanged(); } }
        private bool _IsAutoExpose;

        public double ExpTime { get => _ExpTime; set { _ExpTime = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeLog)); } }
        private double _ExpTime = 100;

        public double ExpTimeLog { get => Math.Log(ExpTime); set { ExpTime = Math.Pow(Math.E, value); } }

        public double ExpTimeMax { get => _ExpTimeMax; set { _ExpTimeMax = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeMaxLog)); } }
        private double _ExpTimeMax = 60000;

        public double ExpTimeMaxLog { get => Math.Log(ExpTimeMax); set { ExpTimeMax = Math.Pow(Math.E, value); } }

        public double ExpTimeMin { get => _ExpTimeMin; set { _ExpTimeMin = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeMinLog)); } }
        private double _ExpTimeMin = 1;

        public double ExpTimeMinLog { get => Math.Log(ExpTimeMin); set { ExpTimeMin = Math.Pow(Math.E, value); } }

        public double ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeRLog)); } }
        private double _ExpTimeR = 100;

        public double ExpTimeRLog { get => Math.Log(ExpTimeR); set { ExpTimeR = Math.Pow(Math.E, value); } }

        public double ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeGLog)); } }
        private double _ExpTimeG = 100;
        public double ExpTimeGLog { get => Math.Log(ExpTimeG); set { ExpTimeG = Math.Pow(Math.E, value); } }

        public double ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeBLog)); } }
        private double _ExpTimeB = 100;

        public double ExpTimeBLog { get => Math.Log(ExpTimeB); set { ExpTimeB = Math.Pow(Math.E, value); } }


        public double Saturation { get => _Saturation; set { _Saturation = value; NotifyPropertyChanged(); } }
        private double _Saturation = -1;

        public double SaturationR { get => _SaturationR; set { _SaturationR = value; NotifyPropertyChanged(); } }
        private double _SaturationR = -1;

        public double SaturationG { get => _SaturationG; set { _SaturationG = value; NotifyPropertyChanged(); } }
        private double _SaturationG = -1;

        public double SaturationB { get => _SaturationB; set { _SaturationB = value; NotifyPropertyChanged(); } }
        private double _SaturationB = -1;

        public CFWPORT CFW { get => _CFW; set { _CFW = value; NotifyPropertyChanged(); } }
        private CFWPORT _CFW = new CFWPORT();

        public MotorConfig MotorConfig { get => _MotorConfig; set { _MotorConfig = value; NotifyPropertyChanged(); } }
        private MotorConfig _MotorConfig = new MotorConfig();

        public AutoFocusParam AutoFocusConfig { get; set; } = new AutoFocusParam();

        public PhyExpTimeCfg ExpTimeCfg { get; set; } = new PhyExpTimeCfg();

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

        public bool IsAutoOpen { get => _IsAutoOpen; set { _IsAutoOpen = value; NotifyPropertyChanged(); } }
        private bool _IsAutoOpen = true;


        public ZBDebayer ZBDebayer { get => _ZBDebayer; set { _ZBDebayer = value; NotifyPropertyChanged(); } }
        private ZBDebayer _ZBDebayer = new ZBDebayer();


    }

}