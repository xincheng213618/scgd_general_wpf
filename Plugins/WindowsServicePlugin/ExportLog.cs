using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin
{
    public class ExportServiceLog : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "ServiceLog";
        public override string Header => Resources.ServiceLog;
    }


    public class ExportRCServiceLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "RCServiceLog";
        public override string Header => Resources.RCServiceLog;
        public override int Order => 1;

        public override string Url => "http://localhost:8080/system/log";
    }
    public class ExportSpectrometerLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "SpectrometerLog";
        public override string Header => Resources.SpectrometerLog;
        public override int Order => 5;
        public override string Url => "http://localhost:8086/system/device/Spectrum/log";
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
