#pragma warning disable CA1861
using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.ToolPlugins.ThirdPartyApps
{
    public class InstallNavicateAppProvider : IThirdPartyAppProvider
    {
        private const string Url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/navicat/navicat161_premium_cs_x64.exe";
        private const string NavicatExeFileName = "navicat.exe";
        private const string DefaultNavicatExePath = @"C:\Program Files\PremiumSoft\Navicat Premium 16\navicat.exe";
        private static readonly string DownloadDir = Environments.DirToolPackageCache;

        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "Navicat",
                    Group = ThirdPartyAppGroupNames.CommonTools,
                    Order = -897,
                    ExecutableFileName = NavicatExeFileName,
                    InstallAction = DownloadAndInstall,
                    KnownExePaths = new[]
                    {
                        DefaultNavicatExePath,
                    },
                    RegistryKeys = new[]
                    {
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PremiumSoft Navicat Premium 16",
                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\PremiumSoft Navicat Premium 16",
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Navicat Premium 16",
                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Navicat Premium 16",
                    },
                    RegistryDisplayNames = new[]
                    {
                        "Navicat Premium 16",
                    },
                }
            };
        }

        private static void DownloadAndInstall()
        {
            var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
            if (service == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "Download service is unavailable.", "ColorVision");
                return;
            }

            service.ShowDownloadWindow();
            service.Download(Url, DownloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
            {
                if (string.IsNullOrEmpty(filePath)) return;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ProcessStartInfo startInfo = new()
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory,
                        FileName = filePath,
                        Verb = "runas"
                    };
                    try
                    {
                        using Process? process = Process.Start(startInfo);
                        process?.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                        TryDelete(filePath);
                    }
                });
            });
        }

        private static void TryDelete(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
            }
        }


    }
}
