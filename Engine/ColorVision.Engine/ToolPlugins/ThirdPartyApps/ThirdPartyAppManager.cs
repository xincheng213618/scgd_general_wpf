using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.ToolPlugins.ThirdPartyApps
{
    public class ThirdPartyAppManager
    {
        private static ThirdPartyAppManager? _instance;
        public static ThirdPartyAppManager GetInstance() => _instance ??= new ThirdPartyAppManager();

        public ObservableCollection<ThirdPartyAppInfo> Apps { get; set; } = new ObservableCollection<ThirdPartyAppInfo>();

        private ThirdPartyAppManager()
        {
            LoadApps();
        }

        public void LoadApps()
        {
            Apps.Clear();

            var portableApps = GetPortableApps();
            foreach (var app in portableApps)
            {
                app.RefreshStatus();
                Apps.Add(app);
            }

            var knownApps = GetKnownApps();
            foreach (var app in knownApps)
            {
                app.RefreshStatus();
                Apps.Add(app);
            }

            ScanInstallToolDirectory();
        }

        public void Refresh()
        {
            foreach (var app in Apps)
            {
                app.RefreshStatus();
            }
        }

        private void ScanInstallToolDirectory()
        {
            string installToolDir = Path.Combine("Assets", "InstallTool");
            if (!Directory.Exists(installToolDir)) return;

            var existingInstallers = new HashSet<string>(Apps.Where(a => !string.IsNullOrEmpty(a.InstallerPath)).Select(a => Path.GetFullPath(a.InstallerPath)));

            foreach (var file in Directory.GetFiles(installToolDir, "*.exe"))
            {
                string fullPath = Path.GetFullPath(file);
                if (existingInstallers.Contains(fullPath)) continue;

                var app = new ThirdPartyAppInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    InstallerPath = file,
                };
                app.RefreshStatus();
                Apps.Add(app);
            }
        }

        private static List<ThirdPartyAppInfo> GetPortableApps()
        {
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "UsbTreeView",
                    IsPortable = true,
                    PortableExePath = Path.Combine("Assets", "Tool", "UsbTreeView.exe"),
                },
                new ThirdPartyAppInfo
                {
                    Name = "SSCOM",
                    IsPortable = true,
                    PortableExePath = Path.Combine("Assets", "Tool", "sscom5.13.1.exe"),
                },
                new ThirdPartyAppInfo
                {
                    Name = "CommMonitor",
                    IsPortable = true,
                    PortableExePath = Path.Combine("Assets", "Tool", "CommMonitor.exe"),
                },
                new ThirdPartyAppInfo
                {
                    Name = "SpectrAdj",
                    IsPortable = true,
                    PortableExePath = Path.Combine("Assets", "Tool", "SpectrAdj.exe"),
                },
            };
        }

        private static List<ThirdPartyAppInfo> GetKnownApps()
        {
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "Everything",
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
}
