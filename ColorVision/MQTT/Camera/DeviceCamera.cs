using ColorVision.MQTT.Service;
using ColorVision.MySql.DAO;
using Newtonsoft.Json;

namespace ColorVision.MQTT.Camera
{
    public class DeviceCamera : MQTTDevice<CameraConfig>
    {

        public DeviceCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {

        }




        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            ServiceControl.GetInstance().ResourceService.Save(SysResourceModel);
        }

    }
}
