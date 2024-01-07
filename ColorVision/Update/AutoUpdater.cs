using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public class AutoUpdater
    {
        public string UpdateUrl { get; set; } = "http://xc213618.ddns.me:9999/D%3A/LATEST_RELEASE";

        public void CheckAndUpdate(bool detection = true)
        {
            try
            {
                // 获取本地版本
                var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
                // 获取服务器版本
                var latestVersion = GetLatestVersionNumber(UpdateUrl);

                if (latestVersion > localVersion)
                {
                    if (MessageBox.Show(Application.Current.MainWindow, $"发现新版本{latestVersion},是否更新", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        // 如果服务器版本较新，则下载并更新软件
                        DownloadAndUpdate(latestVersion);
                    }
                    
                }
                else
                {
                    if (detection)
                        MessageBox.Show(Application.Current.MainWindow, "当前版本已经是最新版本", "ColorVision", MessageBoxButton.OK);
                }
            }
            catch (Exception ex)
            {
                // 处理错误
                Console.WriteLine("An error occurred while updating: " + ex.Message);
            }
        }

        private Version GetLatestVersionNumber(string url)
        {
            using (var client = new WebClient())
            {
                // 从URL下载版本信息
                string versionString = client.DownloadString(url);
                return new Version(versionString.Trim());
            }
        }

        private void DownloadAndUpdate(Version latestVersion)
        {
            // 构建下载URL，这里假设下载路径与版本号相关
            string downloadUrl = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/ColorVision-{latestVersion}.exe";

            // 指定下载路径
            string downloadPath = Path.Combine(Path.GetTempPath(), $"ColorVision-{latestVersion}.exe");

            using (var client = new WebClient())
            {
                // 下载新版本
                client.DownloadFile(downloadUrl, downloadPath);
            }

            RestartApplication(downloadPath);
        }


        private void RestartApplication(string downloadPath)
        {
            // 启动新的实例
            Process.Start(downloadPath);

            // 关闭当前实例
            Environment.Exit(0);
        }


    }
}
