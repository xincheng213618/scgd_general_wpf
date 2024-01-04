using ColorVision.Device.Spectrum.Views;
using ColorVision.MySql.DAO;
using ColorVision.Services.Device;
using System.Windows.Controls;

namespace ColorVision.Device.Spectrum
{
    public class DeviceSpectrum : BaseDevice<ConfigSpectrum>
    {
        public SpectrumService DeviceService { get; set; }

        public ViewSpectrum View { get; set; }

        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            DeviceService = new SpectrumService(Config);
            View = new ViewSpectrum();
        }

        public override UserControl GetDeviceControl() => new DeviceSpectrumControl(this);
        public override UserControl GetDeviceInfo() => new DeviceSpectrumControl(this, false);
        public override UserControl GetDisplayControl() => new SpectrumDisplayControl(this);

    }
}
