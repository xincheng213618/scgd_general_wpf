using ColorVision.MQTT;
using ColorVision.MySql.DAO;

namespace ColorVision.Device.Spectrum
{
    public class DeviceSpectrum : MQTTDevice<SpectrumConfig>
    {
        public SpectrumService SpectrumService { get; set; }

        public SpectrumView ChartView { get; set; }

        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            SpectrumService = new SpectrumService(Config);
            ChartView = new SpectrumView();
        }
    }
}
