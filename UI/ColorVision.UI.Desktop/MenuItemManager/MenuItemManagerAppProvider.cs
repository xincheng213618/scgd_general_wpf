using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Desktop.Properties;
using System.Windows;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    /// <summary>
    /// Exposes the advanced menu customizer from the centralized Apps &amp; Tools hub.
    /// Keeping it out of the customizable menu prevents the tool from hiding itself.
    /// </summary>
    public sealed class MenuItemManagerAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return
            [
                new ThirdPartyAppInfo
                {
                    Name = Resources.MenuMenuItemManager,
                    Group = "ColorVision",
                    Order = 20,
                    Category = ThirdPartyAppCategory.Internal,
                    RequiredPermission = PermissionMode.Administrator,
                    IconGlyph = ThirdPartyAppIconGlyphs.MenuManager,
                    LaunchAction = OpenMenuItemManager,
                }
            ];
        }

        private static void OpenMenuItemManager()
        {
            Window? owner = Application.Current?.GetActiveWindow();
            new MenuItemManagerWindow
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            }.ShowDialog();
        }
    }
}
