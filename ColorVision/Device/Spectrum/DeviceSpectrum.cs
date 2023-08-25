using ColorVision.Device.SMU;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Device.Spectrum
{
    public class DeviceSpectrum : BaseDevice<SpectrumConfig>
    {
        public SpectrumService SpectrumService { get; set; }

        public SpectrumView ChartView { get; set; }

        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            SpectrumService = new SpectrumService(Config);
            ChartView = new SpectrumView();
        }

        public override UserControl GenDeviceControl() => new DeviceSpectrumControl(this);

    }
}
