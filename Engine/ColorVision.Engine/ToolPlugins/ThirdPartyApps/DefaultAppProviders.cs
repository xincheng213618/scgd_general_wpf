using ColorVision.Common.ThirdPartyApps;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.ToolPlugins.ThirdPartyApps
{
    public class PortableAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            string group = Properties.Resources.PortableTools;
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
                    Group = group,
                    IsPortable = true,
                    PortableExePath = Path.Combine("Assets", "Tool", "sscom5.13.1.exe"),
                },
                new ThirdPartyAppInfo
                {
                    Name = "CommMonitor",
                    Group = group,
                    IsPortable = true,
                    PortableExePath = Path.Combine("Assets", "Tool", "CommMonitor.exe"),
                },
                new ThirdPartyAppInfo
                {
                    Name = "SpectrAdj",
                    Group = group,
                    IsPortable = true,
                    PortableExePath = Path.Combine("Assets", "Tool", "SpectrAdj.exe"),
                },
            };
        }
    }

    public class KnownAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            string group = Properties.Resources.InstallTools;
            string everythingInstallerPath = Path.Combine("Assets", "InstallTool", "Everything-1.4.1.1032.x64-Setup.exe");
            string winRarInstallerPath = Path.Combine("Assets", "InstallTool", "winrar-x64-720sc.exe");

            var everything = new ThirdPartyAppInfo
            {
                Name = "Everything",
                Group = group,
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
                Group = group,
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

            string group = Properties.Resources.InstallTools;
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
