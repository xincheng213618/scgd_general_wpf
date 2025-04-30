using ColorVision.UI.LogImp;
using System.IO;
using WindowsServicePlugin.CVWinSMS;
using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.Menus
{

    public class Exportx64ServiceLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "x64ServiceLog";
        public override string Header => Resources.x64ServiceLog;
        public override int Order => 102;
        public override string Url => "http://localhost:8064/system/log";
    }
    public class Exportx64ServiceLog1 : ExportLogBase
    {
        public override string OwnerGuid => nameof(MenuLog);
        public override string Header => Resources.x64ServiceLog;
        public override int Order => 103;
        public override string Url
        {
            get
            {
                CVWinSMSConfig.Instance.Init();
                if (!Directory.Exists(CVWinSMSConfig.Instance.BaseLocation))
                    return string.Empty;
                string path = Path.Combine(CVWinSMSConfig.Instance.BaseLocation, "CVMainWindowsService_x64", "log");
                if (!Directory.Exists(path))
                    return string.Empty;
                return path;
            }
        }
    }
}
