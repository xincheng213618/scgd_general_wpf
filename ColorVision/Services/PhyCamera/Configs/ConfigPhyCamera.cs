using Newtonsoft.Json;
using cvColorVision;
using ColorVision.Services.Devices;

namespace ColorVision.Services.PhyCameras.Configs
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class ConfigPhyCamera : DeviceServiceConfig
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

        public CameraCfg CameraCfg { get; set; } = new CameraCfg();
    }
}