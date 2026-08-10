using ColorVision.Common.ThirdPartyApps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace ColorVision.ToolPlugins.ThirdPartyApps
{
    public class InternalAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "上网网卡选择",
                    Group = ThirdPartyAppGroupNames.CommonTools,
                    Order = -897,
                    LaunchAction = () =>
                    {
                        new NetworkAdapterPriorityWindow
                        {
                            Owner = Application.Current.GetActiveWindow(),
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        }.Show();
                    },
                    GetIconPath = () => Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "control.exe")
                }
            };
        }
    }
}
