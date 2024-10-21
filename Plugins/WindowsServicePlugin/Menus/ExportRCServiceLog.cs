using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.Menus
{
    public class ExportRCServiceLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "RCServiceLog";
        public override string Header => Resources.RCServiceLog;
        public override int Order => 100;

        public override string Url => "http://localhost:8080/system/log";
    }
}
