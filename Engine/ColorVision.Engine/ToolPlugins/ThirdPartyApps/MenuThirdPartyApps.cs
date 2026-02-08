using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Engine.ToolPlugins.ThirdPartyApps
{
    public class MenuThirdPartyApps : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => Properties.Resources.ThirdPartyApps;
        public override int Order => 200;

        public override void Execute()
        {
            new ThirdPartyAppsWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
