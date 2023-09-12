using ColorVision.SettingUp;
using Newtonsoft.Json;
using System.ComponentModel;
using cvColorVision;

namespace ColorVision.Device.Camera
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class CameraConfig : BaseDeviceConfig
    {
        public CameraType CameraType { get => _CameraType; set { _CameraType = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsExpThree)); } }
        private CameraType _CameraType;

        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; NotifyPropertyChanged(); } }
        private TakeImageMode _TakeImageMode;

        public ImageBpp ImageBpp { get => _ImageBpp; set { _ImageBpp = value; NotifyPropertyChanged(); } }
        private ImageBpp _ImageBpp;
        public ImageChannel Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsExpThree)); } }
        private ImageChannel _Channel;

        public CameraVideoConfig VideoConfig { get; set; } = new CameraVideoConfig();
        public int Gain { get => _Gain; set { _Gain = value; NotifyPropertyChanged(); } }
        private int _Gain;

        [JsonIgnore]
        public bool IsExpThree {
            get 
            {
                if (Channel == ImageChannel.Three && CameraType == CameraType.CV_Q)
                    return true;
                return false;
            }
            set => NotifyPropertyChanged();}

        public double ExpTime { get => _ExpTime; set { _ExpTime = value; NotifyPropertyChanged(); } }
        private double _ExpTime;
        public double ExpTimeR { get => _ExpTimeR; set { _ExpTimeR = value; NotifyPropertyChanged(); } }
        private double _ExpTimeR;

        public double ExpTimeG { get => _ExpTimeG; set { _ExpTimeG = value; NotifyPropertyChanged(); } }
        private double _ExpTimeG;

        public double ExpTimeB { get => _ExpTimeB; set { _ExpTimeB = value; NotifyPropertyChanged(); } }
        private double _ExpTimeB;
    }


    public class MotorConfig  
    { 
        public FOCUS_COMMUN eFOCUSCOMMUN { get; set; }



    }
}
