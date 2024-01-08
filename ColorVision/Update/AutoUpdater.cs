using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ColorVision.MVVM;
using ColorVision.SettingUp;
using OpenCvSharp;

namespace ColorVision.Update
{
    public class AutoUpdater : ViewModelBase
    {
        private static AutoUpdater _instance;
        private static readonly object _locker = new();
        public static AutoUpdater GetInstance() { lock (_locker) { return _instance ??= new AutoUpdater(); } }

        public string UpdateUrl { get; set; } = "http://xc213618.ddns.me:9999/D%3A/LATEST_RELEASE";
        public Version LatestVersion { get => _LatestVersion; set { _LatestVersion = value; NotifyPropertyChanged(); } }
        private Version _LatestVersion;


        public AutoUpdater()
        {
            UpdateCommand = new RelayCommand((e) =>  CheckAndUpdate(false));
        }
        public RelayCommand UpdateCommand { get; set; }



        public async void CheckAndUpdate(bool detection = true)
        {
            try
            {
                // 获取本地版本
                var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
                // 获取服务器版本
                LatestVersion = await GetLatestVersionNumber(UpdateUrl);

                if (LatestVersion > localVersion)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show($"发现新版本{LatestVersion},是否更新", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            DownloadAndUpdate(LatestVersion);
                        }
                    });              
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (detection)
                            MessageBox.Show("当前版本已经是最新版本", "ColorVision", MessageBoxButton.OK);
                    });

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while updating: " + ex.Message);
            }
        }
        private async Task<Version>  GetLatestVersionNumber(string url)
        {
            using HttpClient _httpClient = new HttpClient();
            string versionString = await _httpClient.GetStringAsync(url);
            return new Version(versionString.Trim());
        }

        public int ProgressValue { get => _ProgressValue; set { _ProgressValue = value; NotifyPropertyChanged(); } }
        private int _ProgressValue;


        private async Task DownloadAndUpdate(Version latestVersion)
        {
            // 构建下载URL，这里假设下载路径与版本号相关
            string downloadUrl = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/ColorVision-{latestVersion}.exe";

            // 指定下载路径
            string downloadPath = Path.Combine(Path.GetTempPath(), $"ColorVision-{latestVersion}.exe");

            // 实例化 WebClient
            using (var client = new WebClient())
            {
                // 创建进度报告器并订阅进度更新事件
                var progressIndicator = new Progress<int>(progress =>
                {
                    // 更新进度条
                    _ProgressValue = progress;
                });

                // 绑定下载进度事件
                client.DownloadProgressChanged += (sender, e) =>
                {
                    ProgressValue = e.ProgressPercentage;
                };

                // 绑定下载完成事件
                client.DownloadFileCompleted += (sender, e) =>
                {
                    // 检查是否出错或者操作被取消
                    if (e.Cancelled)
                    {
                        MessageBox.Show("Download cancelled.");
                    }
                    else if (e.Error != null)
                    {
                        MessageBox.Show($"An error occurred: {e.Error.Message}");
                    }
                    else
                    {
                        RestartApplication(downloadPath);
                    }
                };
                await client.DownloadFileTaskAsync(new Uri(downloadUrl), downloadPath);
            }
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
