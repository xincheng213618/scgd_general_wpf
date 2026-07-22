using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;

namespace ColorVision.UI.Desktop.ThirdPartyApps
{
    public class ThirdPartyAppsRightMenuItemProvider : IRightMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var icon = new System.Windows.Controls.TextBlock
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 15,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Text = "\xE74C",
                TextAlignment = System.Windows.TextAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            icon.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "GlobalTextBrush");

            return new[]
            {
                new MenuItemMetadata
                {
                    Header = Properties.Resources.ThirdPartyApps,
                    Order = 200,
                    Command = new RelayCommand(a =>
                    {
                        ThirdPartyAppsWindow.ShowInstance();
                    }),
                    Icon = icon
                }
            };
        }
    }
}
