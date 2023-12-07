using ColorVision.MySql.DAO;
using ColorVision.Services.Device;
using ColorVision.Services.Device.Camera;
using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Device.Camera
{
    public class DeviceCamera : BaseDevice<ConfigCamera>
    {
        public DeviceServiceCamera DeviceService { get; set; }


        readonly Lazy<CameraDisplayControl> ControlLazy;

        public CameraView View { get; set; }

        public ServiceCamera Service { get; set; }

        public cvColorVision.TakeImageMode cameraMode { get => _cameraMode; set { _cameraMode = value; IsVideo = value == cvColorVision.TakeImageMode.Live; } }
        private cvColorVision.TakeImageMode _cameraMode;
        public bool IsVideo
        {
            get => _isVideo;
            set
            {
                _isVideo = value; NotifyPropertyChanged();
            }
        }
        private bool _isVideo;


        public DeviceCamera(SysResourceModel sysResourceModel, ServiceCamera cameraService) : base(sysResourceModel)
        {
            Service = cameraService;
            DeviceService = new DeviceServiceCamera(Config, Service);
            View = new CameraView();
            DeviceService.FileHandler += (s, e) =>
            {
                //View.View.(e);
            };
            if (Application.Current.TryFindResource("DrawingImageCamera") is DrawingImage  drawingImage)
                Icon = drawingImage;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource("DrawingImageCamera") is DrawingImage drawingImage)
                    Icon = drawingImage;
                View.View.Icon = Icon;
            };
            View.View.Title = "相机视图";
            View.View.Icon = Icon;
            ControlLazy = new Lazy<CameraDisplayControl>(() => new CameraDisplayControl(this));
        }

        public override void Dispose()
        {
            Service.Dispose();
            DeviceService.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
        }


        public override UserControl GetDeviceControl() => new DeviceCameraControl(this);
        public override UserControl GetDeviceInfo() => new DeviceCameraControl(this, false);

        public override UserControl GetDisplayControl() => ControlLazy.Value;
    }
}
