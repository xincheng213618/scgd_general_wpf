using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.Menus
{
    public class ExportSpectrometerLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "SpectrometerLog";
        public override string Header => Resources.SpectrometerLog;
        public override int Order => 105;
        public override string Url => "http://localhost:8064/system/device/Spectrum/log";
    }
}
