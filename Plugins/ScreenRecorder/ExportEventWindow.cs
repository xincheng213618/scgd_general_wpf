using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;

namespace ScreenRecorder
{
    public class ExporScreenRecorder : MenuItemBase
    {
        public override string OwnerGuid => "Tool";
        public override string GuidId => "ScreenRecorder";
        public override int Order => 500;
        public override string Header => "录制视频";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new MainWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

}
