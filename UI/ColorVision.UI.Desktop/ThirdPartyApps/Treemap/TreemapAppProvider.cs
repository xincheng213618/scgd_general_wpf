using ColorVision.Common.ThirdPartyApps;
using System.Windows;

namespace ColorVision.UI.Desktop.ThirdPartyApps.Treemap
{
    public class TreemapAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new[]
            {
                new ThirdPartyAppInfo
                {
                    Name = "Treemap",
                    Group = "ColorVision",
                    Order = -1000,
                    LaunchAction = () =>
                    {
                        new TreemapWindow
                        {
                            Owner = Application.Current.GetActiveWindow(),
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        }.Show();
                    },
                    GetIconPath = () => Environment.ProcessPath
                }
            };
        }
    }
}
