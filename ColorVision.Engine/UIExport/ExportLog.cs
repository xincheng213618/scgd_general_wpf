#pragma warning disable CS4014
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Properties;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.UIExport
{
    public class ExportServiceLog : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "ServiceLog";
        public override string Header => Resources.ServiceLog;
    }

    public abstract class ExportLogBase : MenuItemBase
    {
        public abstract string Url { get; }

        public override void Execute()
        {
            PlatformHelper.OpenFolder(Url);
        }
    }

    public class ExportRCServiceLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "RCServiceLog";
        public override string Header => Resources.RCServiceLog;
        public override int Order => 1;

        public override string Url => "http://localhost:8080/system/log";
    }



    public class Exportx64ServiceLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "x64ServiceLog";
        public override string Header => Resources.x64ServiceLog;
        public override int Order => 2;
        public override string Url => "http://localhost:8064/system/log";
    }
    public class Exportx86ServiceLog : ExportLogBase
    {
        public override string OwnerGuid => "ServiceLog";
        public override string GuidId => "x86ServiceLog";
        public override string Header => Resources.x86ServiceLog;
        public override int Order => 3;
        public override string Url => "http://localhost:8086/system/log";
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
