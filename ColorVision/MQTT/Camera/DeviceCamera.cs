using ColorVision.MQTT.Service;
using ColorVision.MySql.DAO;
using Newtonsoft.Json;
using System;

namespace ColorVision.MQTT.Camera
{
    public class DeviceCamera : MQTTDevice<CameraConfig>
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
    }
}
