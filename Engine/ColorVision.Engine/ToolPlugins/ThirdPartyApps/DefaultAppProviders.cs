using System.Collections.Generic;
using System.IO;

namespace ColorVision.Engine.ToolPlugins.ThirdPartyApps
{
    public class PortableAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
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
    }

    public class KnownAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
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
