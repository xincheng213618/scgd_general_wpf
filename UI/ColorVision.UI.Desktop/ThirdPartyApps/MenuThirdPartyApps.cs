using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.UI.Desktop.ThirdPartyApps
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

    public class ThirdPartyAppsRightMenuItemProvider : IRightMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return new[]
            {
                new MenuItemMetadata
                {
                    Header = Properties.Resources.ThirdPartyApps,
                    Order = 200,
                    Command = new RelayCommand(a =>
                    {
                        new ThirdPartyAppsWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
                    }),
                    Icon = new System.Windows.Controls.TextBlock
                    {
                        Text = "\xE74C",
                        FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                        FontSize = 16,
                    }
                }
            };
        }
    }
}
