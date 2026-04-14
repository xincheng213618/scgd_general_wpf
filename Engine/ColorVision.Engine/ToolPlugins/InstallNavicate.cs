using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace WindowsServicePlugin
{
    public class InstallNavicateAppProvider : IThirdPartyAppProvider
    {
        private const string Url = "http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/navicat/navicat161_premium_cs_x64.exe";
        private static readonly string DownloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "Navicat",
                    Group = "InstallTools",
                    Order = 50,
                    LaunchAction = ExecuteInstall,
                }
            };
        }

        private static void ExecuteInstall()
        {
            var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
            if (service == null) return;

            service.ShowDownloadWindow();
            service.Download(Url, DownloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
            {
                if (filePath == null) return;
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ProcessStartInfo startInfo = new()
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        FileName = filePath,
                        Verb = "runas"
                    };
                    try
                    {
                        Process p = Process.Start(startInfo);
                        p?.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                        File.Delete(filePath);
                    }
                });
            });
        }

    }



}
