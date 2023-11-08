using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using System.Windows.Controls;

namespace ColorVision.Device.Spectrum
{
    public class DeviceSpectrum : BaseDevice<SpectrumConfig>
    {
        public SpectrumService DeviceService { get; set; }

        public SpectrumView View { get; set; }

        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new SpectrumService(Config);
            View = new SpectrumView();
        }

        public override UserControl GetDeviceControl() => new DeviceSpectrumControl(this);

        public override UserControl GetDisplayControl() => new SpectrumDisplayControl(this);

    }
}
