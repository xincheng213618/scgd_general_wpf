using ColorVision.UI.LogImp;
using System.IO;
using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.Menus
{



    public class ExportCameraLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "CameraLog";
        public override string Header => Resources.CameraLog;
        public override int Order => 104;
        public override string Url => "http://localhost:8064/system/device/camera/log";
    }
    public class ExportCameraLog1 : ExportLogBase
    {
        public override string OwnerGuid => nameof(MenuLog);
        public override string Header => Resources.CameraLog;
        public override int Order => 103;
        public override string Url
        {
            get
            {
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
