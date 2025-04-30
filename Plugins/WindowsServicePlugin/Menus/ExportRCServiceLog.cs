using ColorVision.UI.LogImp;
using System.IO;
using WindowsServicePlugin.CVWinSMS;
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

    public class ExportRCServiceLog1 : ExportLogBase
    {
        public override string OwnerGuid => nameof(MenuLog);
        public override string Header => Resources.RCServiceLog;
        public override int Order => 103;
        public override string Url
        {
            get
            {
                CVWinSMSConfig.Instance.Init();
                if (!Directory.Exists(CVWinSMSConfig.Instance.BaseLocation))
                    return string.Empty;
                string path = Path.Combine(CVWinSMSConfig.Instance.BaseLocation, "RegWindowsService", "log");
                if (!Directory.Exists(path))
                    return string.Empty;
                return path;
            }
        }
    }
}
