using ColorVision.Database.Properties;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Database
{
    public class MenuEntityBrowser : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 50;
        public override string Header => Resources.MenuEntityBrowser;

        public override void Execute()
        {
            new DatabaseBrowserWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }
    }
}
