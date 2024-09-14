using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.Menus
{
    public class ExportServiceLog : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "ServiceLog";
        public override string Header => Resources.ServiceLog;
    }



    public class ExportCameraLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "CameraLog";
        public override string Header => Resources.CameraLog;
        public override int Order => 4;
        public override string Url => "http://localhost:8064/system/device/camera/log";
    }
}
