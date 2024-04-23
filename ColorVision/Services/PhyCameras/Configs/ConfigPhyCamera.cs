using Newtonsoft.Json;
using ColorVision.Services.Devices;
using cvColorVision;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Common.MVVM;
using CVCommCore.CVCamera;

namespace ColorVision.Services.PhyCameras.Configs
{
    /// <summary>
    /// 相机配置
    /// </summary>
    public class ConfigPhyCamera : ViewModelBase
    {
        public string CameraID { get => _CameraID; set { _CameraID = value; NotifyPropertyChanged(); } }
        private string _CameraID;

        public cvColorVision.CameraType CameraType { get => _CameraType; set { _CameraType = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsExpThree)); } }
        private cvColorVision.CameraType _CameraType;

        public cvColorVision.TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; NotifyPropertyChanged(); } }
        private cvColorVision.TakeImageMode _TakeImageMode;

        public ImageBpp ImageBpp { get => _ImageBpp; set { _ImageBpp = value; NotifyPropertyChanged(); } }
        private ImageBpp _ImageBpp;
        public ImageChannel Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsExpThree)); NotifyPropertyChanged(nameof(IsChannelThree)); } }
        private ImageChannel _Channel;

        [JsonIgnore]
        public bool IsExpThree
        {
            get => TakeImageMode != cvColorVision.TakeImageMode.Live && (CameraType == cvColorVision.CameraType.CV_Q || CameraType == cvColorVision.CameraType.CV_MIL_CL);
            set => NotifyPropertyChanged();
        }
        [JsonIgnore]
        public bool IsChannelThree
        {
            get => Channel == ImageChannel.Three;
            set => NotifyPropertyChanged();
        }
        public CameraCfg CameraCfg { get; set; } = new CameraCfg();
        public CFWPORT CFW { get; set; } = new CFWPORT();

        public FileSeviceConfig FileServerCfg { get; set; } = new FileSeviceConfig() { Endpoint = "127.0.0.1", FileBasePath = "D:\\", PortRange = "43210" };
    }
}