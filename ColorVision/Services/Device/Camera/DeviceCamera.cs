using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.Services.Device.Camera;
using ColorVision.Themes.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Device.Camera
{
    public class DeviceCamera : BaseDevice<ConfigCamera>
    {
        public ImageSource Icon { get; set; }


        public DeviceServiceCamera DeviceService { get; set; }

        public CameraDisplayControl Control { get; set; }

        public ImageView View { get; set; }

        public ServiceCamera Service { get; set; }


        public DeviceCamera(SysResourceModel sysResourceModel, ServiceCamera cameraService) : base(sysResourceModel)
        {
            Service = cameraService;
            DeviceService = new DeviceServiceCamera(Config, Service);
            View = new ImageView();
            DeviceService.FileHandler += (s, e) =>
            {
                View.OpenImage(e);
            };
            Control = new CameraDisplayControl(this);

            if (Application.Current.TryFindResource("DrawingImageCamera") is DrawingImage  drawingImage)
            {
                Icon = drawingImage;
            }
        }




        public override UserControl GetDeviceControl() => new DeviceCameraControl(this);

        public override UserControl GetDisplayControl() => Control;
    }
}
