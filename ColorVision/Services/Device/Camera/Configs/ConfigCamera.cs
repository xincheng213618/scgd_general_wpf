using Newtonsoft.Json;
using cvColorVision;
using ColorVision.Services.Device.Camera.Video;
using System;
using ColorVision.Services.Device.Camera.Calibrations;
using ColorVision.Templates;
using System.Collections.ObjectModel;
using Xceed.Wpf.AvalonDock.Themes;
using System.Collections.Generic;
using ColorVision.Device.Camera;

namespace ColorVision.Services.Device.Camera.Configs
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class ConfigCamera : DeviceServiceConfig
    {
        public string CameraID { get => _CameraID; set { _CameraID = value; NotifyPropertyChanged(); } }
        private string _CameraID;

        public CameraType CameraType { get => _CameraType; set { _CameraType = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsExpThree)); } }
        private CameraType _CameraType;

        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; NotifyPropertyChanged(); } }
        private TakeImageMode _TakeImageMode;

        public ImageBpp ImageBpp { get => _ImageBpp; set { _ImageBpp = value; NotifyPropertyChanged(); } }
        private ImageBpp _ImageBpp;
        public ImageChannel Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsExpThree)); NotifyPropertyChanged(nameof(IsChannelThree)); } }
        private ImageChannel _Channel;

        public CameraVideoConfig VideoConfig { get; set; } = new CameraVideoConfig();
        public int Gain { get => _Gain; set { _Gain = value; NotifyPropertyChanged(); } }
        private int _Gain = 10;

        [JsonIgnore]
        public bool IsExpThree
        {
            get => TakeImageMode != TakeImageMode.Live && (CameraType == CameraType.CV_Q || CameraType == CameraType.CV_MIL_CL);
            set => NotifyPropertyChanged();
        }
        [JsonIgnore]
        public bool IsChannelThree
        {
            get => Channel == ImageChannel.Three;
            set => NotifyPropertyChanged();
        }

        public int ExpTime { get => _ExpTime; set { _ExpTime = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ExpTimeLog)); } }
        private int _ExpTime = 10;

        public double ExpTimeLog { get => Math.Log(ExpTime); set { ExpTime = (int)Math.Pow(Math.E, value); } }


        public int ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; NotifyPropertyChanged(); } }
        private int _ExpTimeR = 10;
        public double ExpTimeRLog { get => Math.Log(ExpTimeR); set { ExpTimeR = (int)Math.Pow(Math.E, value); } }


        public int ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; NotifyPropertyChanged(); } }
        private int _ExpTimeG = 10;
        public double ExpTimeGLog { get => Math.Log(ExpTimeG); set { ExpTimeG = (int)Math.Pow(Math.E, value); } }

        public int ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; NotifyPropertyChanged(); } }
        private int _ExpTimeB = 10;
        public double ExpTimeBLog { get => Math.Log(ExpTimeB); set { ExpTimeB = (int)Math.Pow(Math.E, value); } }


        public double Saturation { get => _Saturation; set { _Saturation = value; NotifyPropertyChanged(); } }
        private double _Saturation = -1;

        public double SaturationR { get => _SaturationR; set { _SaturationR = value; NotifyPropertyChanged(); } }
        private double _SaturationR = -1;

        public double SaturationG { get => _SaturationG; set { _SaturationG = value; NotifyPropertyChanged(); } }
        private double _SaturationG = -1;

        public double SaturationB { get => _SaturationB; set { _SaturationB = value; NotifyPropertyChanged(); } }
        private double _SaturationB = -1;

        public CFWPORT CFW { get; set; } = new CFWPORT();

        public bool IsHaveMotor { get => _IsHaveMotor; set { _IsHaveMotor = value; NotifyPropertyChanged(); } }
        private bool _IsHaveMotor;

        public MotorConfig MotorConfig { get; set; } = new MotorConfig();

        public ExpTimeCfg ExpTimeCfg { get; set; } = new ExpTimeCfg();

        public CameraCfg CameraCfg { get; set; } = new CameraCfg();

        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();

        public bool IsSaveSrcPic { get; set; }

        public Dictionary<string,CalibrationRsourcesGroup> CalibrationRsourcesGroups { get; set; } = new Dictionary<string,CalibrationRsourcesGroup>();
    }
}