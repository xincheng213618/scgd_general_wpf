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
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "Everything",
                    Group = group,
                    InstallerPath = Path.Combine("Assets", "InstallTool", "Everything-1.4.1.1032.x64-Setup.exe"),
                    RegistryKeys = new[]
                    {
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Everything",
                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Everything",
                    }
                },
                new ThirdPartyAppInfo
                {
                    Name = "WinRAR",
                    Group = group,
                    InstallerPath = Path.Combine("Assets", "InstallTool", "winrar-x64-720sc.exe"),
                    RegistryKeys = new[]
                    {
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WinRAR archiver",
                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\WinRAR archiver",
                    }
                },
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
                    .Select(a => Path.GetFullPath(a.InstallerPath)));

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
