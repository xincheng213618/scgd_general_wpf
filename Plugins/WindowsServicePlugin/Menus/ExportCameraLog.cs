using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.Menus
{



    public class ExportCameraLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "CameraLog";
        public override string Header => Resources.CameraLog;
        public override int Order => 4;
        public override string Url => "http://localhost:8064/system/device/camera/log";
    }
}
