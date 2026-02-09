using ColorVision.Common.ThirdPartyApps;
using System.Collections.Generic;

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
                    Name = "CMD",
                    Group = group,
                    LaunchPath = "cmd.exe",
                },
                new ThirdPartyAppInfo
                {
                    Name = "PowerShell",
                    Group = group,
                    LaunchPath = "powershell.exe",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Control Panel",
                    Group = group,
                    LaunchPath = "control.exe",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Registry Editor",
                    Group = group,
                    LaunchPath = "regedit.exe",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Group Policy",
                    Group = group,
                    LaunchPath = "mmc.exe",
                    LaunchArguments = "gpedit.msc",
                },
                new ThirdPartyAppInfo
                {
                    Name = "System Information",
                    Group = group,
                    LaunchPath = "msinfo32.exe",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Remote Desktop",
                    Group = group,
                    LaunchPath = "mstsc.exe",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Event Viewer",
                    Group = group,
                    LaunchPath = "mmc.exe",
                    LaunchArguments = "eventvwr.msc",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Task Scheduler",
                    Group = group,
                    LaunchPath = "mmc.exe",
                    LaunchArguments = "taskschd.msc",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Services",
                    Group = group,
                    LaunchPath = "mmc.exe",
                    LaunchArguments = "services.msc",
                },
                new ThirdPartyAppInfo
                {
                    Name = "Network Connections",
                    Group = group,
                    LaunchPath = "control.exe",
                    LaunchArguments = "ncpa.cpl",
                },
            };
        }
    }
}
