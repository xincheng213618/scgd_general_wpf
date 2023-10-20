using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.Services;
using System.Windows.Controls;

namespace ColorVision.Device.Camera
{
    public class DeviceCamera : BaseDevice<CameraConfig>
    {
        public CameraDeviceService DeviceService { get; set; }

        public CameraDisplayControl Control { get; set; }

        public ImageView View { get; set; }

        public CameraService Service { get; set; }


        public DeviceCamera(SysResourceModel sysResourceModel, CameraService cameraService) : base(sysResourceModel)
        {
            Service = cameraService;
            DeviceService = new CameraDeviceService(Config, Service);
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
