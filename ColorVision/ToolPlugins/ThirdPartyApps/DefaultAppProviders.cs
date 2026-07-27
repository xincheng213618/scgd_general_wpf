using ColorVision.Common.ThirdPartyApps;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ColorVision.ToolPlugins.ThirdPartyApps
{
    internal static class ThirdPartyAppGroupNames
    {
        public static string CommonTools => ColorVision.Engine.Properties.Resources.CommonTools;

        private static bool IsChineseUICulture()
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", System.StringComparison.OrdinalIgnoreCase);
        }
    }

    public class PortableAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            string group = ColorVision.Engine.Properties.Resources.PortableTools;
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "UsbTreeView",
                    Group = group,
                    IsPortable = true,
                    PortableExePath = Path.Combine("Assets", "Tool", "UsbTreeView.exe"),
                },
                new ThirdPartyAppInfo
                {
                    Name = "SSCOM",
                    Group = ThirdPartyAppGroupNames.CommonTools,
                    Order = -898,
                    IsPortable = true,
                    PortableExePath = Path.Combine("Assets", "Tool", "sscom5.13.1.exe"),
                }
            };
        }
    }

    public class KnownAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            string everythingInstallerPath = Path.Combine("Assets", "InstallTool", "Everything-1.4.1.1032.x64-Setup.exe");
            string winRarInstallerPath = Path.Combine("Assets", "InstallTool", "winrar-x64-720sc.exe");

            var everything = new ThirdPartyAppInfo
            {
                Name = "Everything",
                Group = ThirdPartyAppGroupNames.CommonTools,
                Order = -900,
                InstallerPath = everythingInstallerPath,
                ExecutableFileName = "Everything.exe",
                KnownExePaths = new[]
                {
                    @"C:\Program Files\Everything\Everything.exe",
                    @"C:\Program Files (x86)\Everything\Everything.exe",
                },
                RegistryKeys = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Everything",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Everything",
                }
            };

            var winRar = new ThirdPartyAppInfo
            {
                Name = "WinRAR",
                Group = ThirdPartyAppGroupNames.CommonTools,
                Order = -899,
                InstallerPath = winRarInstallerPath,
                ExecutableFileName = "WinRAR.exe",
                KnownExePaths = new[]
                {
                    @"C:\Program Files\WinRAR\WinRAR.exe",
                    @"C:\Program Files (x86)\WinRAR\WinRAR.exe",
                },
                RegistryKeys = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WinRAR archiver",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\WinRAR archiver",
                },
            };

            winRar.ContextActions.Add(new ThirdPartyAppContextAction
            {
                Header = "覆盖安装",
                Execute = winRar.RunInstaller,
                CanExecute = winRar.CanRunInstaller,
            });

            return new List<ThirdPartyAppInfo>
            {
                everything,
                winRar,
            };
        }
    }

    public class InstallToolDirectoryProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            string installToolDir = Path.Combine("Assets", "InstallTool");
            if (!Directory.Exists(installToolDir))
                return Enumerable.Empty<ThirdPartyAppInfo>();

            string group = ColorVision.Engine.Properties.Resources.InstallTools;
            var apps = new List<ThirdPartyAppInfo>();
            var knownProvider = new KnownAppProvider();
            var knownInstallers = new HashSet<string>(
                knownProvider.GetThirdPartyApps()
                    .Where(a => !string.IsNullOrEmpty(a.InstallerPath))
                    .Select(a => Path.GetFullPath(a.InstallerPath)),
                System.StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(installToolDir, "*.exe"))
            {
                string fullPath = Path.GetFullPath(file);
                if (knownInstallers.Contains(fullPath)) continue;

                apps.Add(new ThirdPartyAppInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Group = group,
                    InstallerPath = file,
                });
            }
            return apps;
        }
    }
}
