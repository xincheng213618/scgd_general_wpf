using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Database
{
    public class MenuEntityBrowser : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 50;
        public override string Header => "实体浏览器";

        public override void Execute()
        {
            new EntityBrowserWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }
    }
}
