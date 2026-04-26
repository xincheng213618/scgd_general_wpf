using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Database
{
    public class MenuEntityBrowser : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 50;
        public override string Header => "数据库浏览器";

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
