using ColorVision.MySql.DAO;
using ColorVision.Services.Device;
using System.Windows.Controls;

namespace ColorVision.Device.Spectrum
{
    public class DeviceSpectrum : BaseDevice<ConfigSpectrum>
    {
        public SpectrumService DeviceService { get; set; }

        public SpectrumView View { get; set; }

        public DeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            //if(Config.ShutterCfg == null) Config.ShutterCfg = new ShutterConfig() { Addr="COM1", BaudRate=115200, DelayTime=1000, OpenCmd="a", CloseCmd="b" };
            DeviceService = new SpectrumService(Config);
            View = new SpectrumView();
        }

        public override UserControl GetDeviceControl() => new DeviceSpectrumControl(this);
        public override UserControl GetDeviceInfo() => new DeviceSpectrumControl(this, false);
        public override UserControl GetDisplayControl() => new SpectrumDisplayControl(this);

    }
}
