using Newtonsoft.Json;
using cvColorVision;
using ColorVision.MVVM;
using ColorVision.Services.Device.Camera.Video;
using ColorVision.Device;

namespace ColorVision.Services.Device.Camera
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class ConfigCamera : BaseDeviceConfig
    {
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
            get
            {
                if (Channel == ImageChannel.Three && CameraType == CameraType.CV_Q)
                    return true;
                return false;
            }
            set => NotifyPropertyChanged();
        }
        [JsonIgnore]
        public bool IsChannelThree
        {
            get
            {
                if (Channel == ImageChannel.Three)
                    return true;
                return false;
            }
            set => NotifyPropertyChanged();
        }


        public double ExpTime { get => _ExpTime; set { _ExpTime = value; NotifyPropertyChanged(); } }
        private double _ExpTime = 10;
        public double ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; NotifyPropertyChanged(); } }
        private double _ExpTimeR = 10;

        public double ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; NotifyPropertyChanged(); } }
        private double _ExpTimeG = 10;

        public double ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; NotifyPropertyChanged(); } }
        private double _ExpTimeB = 10;


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
    }


    public class CFWPORT : ViewModelBase
    {
        public ChannelConfig[] CFW { get; set; } = new ChannelConfig[3]{
            new ChannelConfig() { Port =0,ChannelType =ImageChannelType.Gray_X }, new ChannelConfig(){Port =1,ChannelType =ImageChannelType.Gray_Y }, new ChannelConfig(){ Port =2,ChannelType =ImageChannelType.Gray_Z}
        };

        public bool IsCOM { get => _IsCOM; set { _IsCOM = value; NotifyPropertyChanged(); } }
        private bool _IsCOM;

        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 9600;
    }


    public class MotorConfig : ViewModelBase
    {
        public FOCUS_COMMUN eFOCUSCOMMUN { get => _eFOCUSCOMMUN; set { _eFOCUSCOMMUN = value; NotifyPropertyChanged(); } }
        private FOCUS_COMMUN _eFOCUSCOMMUN;

        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";

        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 9600;

        public AutoFocusConfig AutoFocusConfig { get; set; } = new AutoFocusConfig();

        [JsonIgnore]
        public int Position { get => _Position; set { _Position = value; NotifyPropertyChanged(); } }
        private int _Position;

        public int DwTimeOut { get => _dwTimeOut; set { _dwTimeOut = value; NotifyPropertyChanged(); } }
        private int _dwTimeOut = 5000;
    }

    public class ChannelConfig : ViewModelBase
    {
        public int Port { get => _Port; set { _Port = value; NotifyPropertyChanged(); } }
        private int _Port;

        public ImageChannelType ChannelType { get => _ChannelType; set { if (_ChannelType == value) return; _ChannelType = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ChannelTypeString)); } }
        private ImageChannelType _ChannelType;


        [JsonIgnore]
        public string ChannelTypeString
        {
            get
            {
                return ChannelType switch
                {
                    ImageChannelType.Gray_X => "Channel_R",
                    ImageChannelType.Gray_Y => "Channel_G",
                    ImageChannelType.Gray_Z => "Channel_B",
                    _ => ChannelType.ToString(),
                };
            }
        }
    }


    public class AutoFocusConfig : ViewModelBase
    {
        public double forwardparam { get => _forwardpara; set { _forwardpara = value; NotifyPropertyChanged(); } }
        private double _forwardpara;

        public double curtailparam { get => _curtailparam; set { _curtailparam = value; NotifyPropertyChanged(); } }
        private double _curtailparam;


        public int curStep { get => _curStep; set { _curStep = value; NotifyPropertyChanged(); } }
        private int _curStep;

        public int stopStep { get => _stopStep; set { _stopStep = value; NotifyPropertyChanged(); } }
        private int _stopStep;

        public int minPosition { get => _minPosition; set { _minPosition = value; NotifyPropertyChanged(); } }
        private int _minPosition;

        public int maxPosition { get => _maxPosition; set { _maxPosition = value; NotifyPropertyChanged(); } }
        private int _maxPosition;
        public EvaFunc eEvaFunc { get => _eEvaFunc; set { _eEvaFunc = value; NotifyPropertyChanged(); } }
        private EvaFunc _eEvaFunc;
        public double dMinValue { get => _dMinValue; set { _dMinValue = value; NotifyPropertyChanged(); } }
        private double _dMinValue;


    }
}