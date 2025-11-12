using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ScreenRecorder
{
    public class ExporScreenRecorder : MenuItemBase
    {
        public override string OwnerGuid => "Tool";
        public override string GuidId => "ScreenRecorder";
        public override int Order => 500;
        public override string Header => ScreenRecorder.Properties.Resources.ScreenRecorder;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new MainWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }

}
