using ColorVision.Common.ThirdPartyApps;
using System;
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
                    Order = 10,
                    LaunchAction = OpenModelViewer,
                    GetIconPath = () => Environment.ProcessPath
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
