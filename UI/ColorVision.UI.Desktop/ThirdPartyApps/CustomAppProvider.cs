using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ColorVision.UI.Desktop.ThirdPartyApps
{
    /// <summary>
    /// 从 CustomAppsConfig 读取用户自定义应用和脚本，注册到 ThirdPartyAppManager
    /// </summary>
    public class CustomAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            var config = CustomAppsConfig.Instance;
            return config.Entries.Select(CreateAppInfo).Where(a => a != null)!;
        }

        private static ThirdPartyAppInfo? CreateAppInfo(CustomAppEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Command))
                return null;

            var info = new ThirdPartyAppInfo
            {
                Name = entry.Name,
                Group = !string.IsNullOrEmpty(entry.Group) ? entry.Group : CustomAppsConfig.Instance.DefaultCustomGroup,
                Order = entry.Order,
            };

            switch (entry.AppType)
            {
                case CustomAppType.Executable:
                    info.LaunchPath = entry.Command;
                    info.LaunchArguments = entry.Arguments;
                    break;
                case CustomAppType.CmdScript:
                    info.LaunchPath = "cmd.exe";
                    info.LaunchArguments = $"/c \"{entry.Command}\" {entry.Arguments}".Trim();
                    if (string.IsNullOrEmpty(entry.Group))
                        info.Group = CustomAppsConfig.Instance.DefaultScriptGroup;
                    break;
                case CustomAppType.PowerShellScript:
                    info.LaunchPath = "powershell.exe";
                    info.LaunchArguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{entry.Command}\" {entry.Arguments}".Trim();
                    if (string.IsNullOrEmpty(entry.Group))
                        info.Group = CustomAppsConfig.Instance.DefaultScriptGroup;
                    break;
            }

            return info;
        }
    }
}
