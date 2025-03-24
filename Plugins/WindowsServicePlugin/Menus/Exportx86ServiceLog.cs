using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.Menus
{
    public class Exportx86ServiceLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "x86ServiceLog";
        public override string Header => Resources.x86ServiceLog;
        public override int Order => 103;
        public override string Url => "http://localhost:8086/system/log";
    }


}
