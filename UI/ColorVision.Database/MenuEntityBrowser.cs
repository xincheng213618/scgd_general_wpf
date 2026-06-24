using ColorVision.Common.ThirdPartyApps;
using ColorVision.Database.Properties;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Database
{
    public class DatabaseBrowserAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new[]
            {
                new ThirdPartyAppInfo
                {
                    Name = Resources.MenuEntityBrowser,
                    Group = "ColorVision",
                    Order = 50,
                    LaunchAction = OpenDatabaseBrowser,
                    GetIconPath = () => Environment.ProcessPath
                }
            };
        }

        private static void OpenDatabaseBrowser()
        {
            Window? owner = Application.Current.GetActiveWindow();
            new DatabaseBrowserWindow
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
            }.Show();
        }
    }
}
