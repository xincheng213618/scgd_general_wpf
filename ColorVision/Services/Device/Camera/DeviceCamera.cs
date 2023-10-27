using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.Services.Device.Camera;
using System.Windows.Controls;

namespace ColorVision.Device.Camera
{
    public class DeviceCamera : BaseDevice<ConfigCamera>
    {
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
        }




        public override UserControl GetDeviceControl() => new DeviceCameraControl(this);

        public override UserControl GetDisplayControl() => Control;
    }
}
