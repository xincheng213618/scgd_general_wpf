using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Device.Camera
{
    public class DeviceCamera : BaseDevice<CameraConfig>
    {
        public CameraService CameraService { get; set; }

        public MQTTCameraControl1 Control { get; set; }

        public ImageView View { get; set; }

        public DeviceCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            CameraService = new CameraService(Config);
            Control = new MQTTCameraControl1(CameraService);
            View = new ImageView();
        }

        public override UserControl GenDeviceControl() => new DeviceCameraControl(this);
    }
}
