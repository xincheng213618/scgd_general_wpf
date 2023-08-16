using ColorVision.MQTT.Camera;
using ColorVision.MySql.DAO;

namespace ColorVision.MQTT.Spectrum
{
    public class DeviceSpectrum : MQTTDevice<SpectrumConfig>
    {
        public SpectrumService SpectrumService { get; set; }

        public ChartView ChartView { get; set; }

        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            SpectrumService = new SpectrumService(Config);
            ChartView = new ChartView();
        }
    }
}
