using ColorVision.Common.MVVM;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Scheduler
{
    public class MenuTaskViewer :  MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override int Order => 10;

        public override string Header => Properties.Resources.TaskViewerWindow;

        public override void Execute()
        {
            new TaskViewerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
