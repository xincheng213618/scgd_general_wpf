using ColorVision.Device.Spectrum.Configs;
using ColorVision.Device.Spectrum.Views;
using ColorVision.MySql.DAO;
using ColorVision.Services.Device;
using System.Windows.Controls;

namespace ColorVision.Device.Spectrum
{
    public class DeviceSpectrum : DeviceService<ConfigSpectrum>
    {
        public MQTTSpectrum DeviceService { get; set; }

        public ViewSpectrum View { get; set; }

        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new MQTTSpectrum(Config);
            View = new ViewSpectrum(this);
        }

        public override UserControl GetDeviceControl() => new DeviceSpectrumControl(this);
        public override UserControl GetDeviceInfo() => new DeviceSpectrumControl(this, false);
        public override UserControl GetDisplayControl() => new DisplaySpectrumControl(this);

    }
}
