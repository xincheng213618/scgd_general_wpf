using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Device.Camera
{
    public class DeviceCamera : BaseDevice<CameraConfig>
    {
        public CameraDeviceService Service { get; set; }

        public CameraDisplayControl Control { get; set; }

        public ImageView View { get; set; }

        public DeviceCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            Service = new CameraDeviceService(Config);
            View = new ImageView();
            Service.FileHandler += (s, e) =>
            {
                View.OpenImage(e);
            };
            Control = new CameraDisplayControl(this);
        }

        public override UserControl GetDeviceControl() => new DeviceCameraControl(this);

        public override UserControl GetDisplayControl() => Control;
    }
}
