using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.Spectrum
{
    public class DeviceSpectrum : MQTTDevice<SpectrumConfig>
    {

        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {

        }
    }
}
