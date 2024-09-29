using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.Menus
{

    public class Exportx64ServiceLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "x64ServiceLog";
        public override string Header => Resources.x64ServiceLog;
        public override int Order => 2;
        public override string Url => "http://localhost:8064/system/log";
    }
}
