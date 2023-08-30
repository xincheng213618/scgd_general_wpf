using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Device.Camera
{
    public class DeviceCamera : BaseDevice<CameraConfig>
    {
        public CameraService Service { get; set; }

        public MQTTCameraControl1 Control { get; set; }

        public ImageView View { get; set; }

        public DeviceCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            Service = new CameraService(Config);
            View = new ImageView();
            Service.FileHandler += (s, e) =>
            {
                View.OpenImage(e);
            };
            Control = new MQTTCameraControl1(this);
        }

        public override UserControl GenDeviceControl() => new DeviceCameraControl(this);
    }
}
