using ColorVision.Common.ThirdPartyApps;

namespace ColorVision.UI.Desktop.ThirdPartyApps
{
    public class SystemAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            string group = Properties.Resources.SystemTools;
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "Control Panel",
                    Group = group,
                    Category = ThirdPartyAppCategory.System,
                    LaunchPath = "control.exe",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Registry Editor",
                    Group = group,
                    Category = ThirdPartyAppCategory.System,
                    LaunchPath = "C:\\Windows\\SysWOW64\\regedit.exe",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Group Policy",
                    Group = group,
                    Category = ThirdPartyAppCategory.System,
                    LaunchPath = "mmc.exe",
                    LaunchArguments = "gpedit.msc",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Network Connections",
                    Group = group,
                    Category = ThirdPartyAppCategory.System,
                    LaunchPath = "control.exe",
                    LaunchArguments = "ncpa.cpl",
                },
            };
        }
    }
}
