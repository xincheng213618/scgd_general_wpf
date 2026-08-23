using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Authorizations;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    public class ModelViewer3DAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new[]
            {
                new ThirdPartyAppInfo
                {
                    Name = Properties.Resources.MenuModelViewer3D,
                    Group = "ColorVision",
                    Category = ThirdPartyAppCategory.Internal,
                    RequiredPermission = PermissionMode.Guest,
                    Order = 10,
                    IconGlyph = ThirdPartyAppIconGlyphs.Model3D,
                    LaunchAction = OpenModelViewer,
                }
            };
        }

        private static void OpenModelViewer()
        {
            Window? owner = Application.Current.GetActiveWindow();
            new ModelViewer3DWindow
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
            }.Show();
        }
    }
}
