using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using ColorVision.MVVM;

namespace ColorVision.Update
{
    public class AutoUpdater : ViewModelBase
    {
        private static AutoUpdater _instance;
        private static readonly object _locker = new object();
        public static AutoUpdater GetInstance() { lock (_locker) { if (_instance == null) _instance = new AutoUpdater(); return _instance; } }


        public string UpdateUrl { get => _UpdateUrl; set { _UpdateUrl = value; NotifyPropertyChanged(); } }
        private string _UpdateUrl = "http://xc213618.ddns.me:9999/D%3A/LATEST_RELEASE";

        public Version LatestVersion { get => _LatestVersion; set { _LatestVersion = value; NotifyPropertyChanged(); } }
        private Version _LatestVersion;


        public AutoUpdater()
        {
            Status = "正在检查版本";
            CheckAndUpdate(false);
        }

        // 调用函数以删除所有更新文件
        public async void CheckAndUpdate(bool detection = true)
        {
            // 获取本地版本
            try
            {
                // 获取服务器版本
                LatestVersion = await GetLatestVersionNumber(UpdateUrl);
                Status ="最新版本为：" + LatestVersion.ToString();
                _ = Task.Run(() => DownloadAndUpdate(LatestVersion));
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while updating: " + ex.Message);
            }
        }
        private static async Task<Version>  GetLatestVersionNumber(string url)
        {
            using (HttpClient _httpClient = new HttpClient())
            {
                string versionString = await _httpClient.GetStringAsync(url);
                return new Version(versionString.Trim());
            }
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
                    Status = $"正在下载{LatestVersion} {_ProgressValue}%";
                };
                // 绑定下载完成事件
                client.DownloadFileCompleted += (sender, e) =>
                {
                    // 检查是否出错或者操作被取消
                    if (e.Cancelled)
                    {
                        MessageBox.Show("Download cancelled.");
                        Status = "安装已经取消";
                    }
                    else if (e.Error != null)
                    {
                        MessageBox.Show($"An error occurred: {e.Error.Message}");
                    }
                    else
                    {

                        Status = "正在安装";
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            RestartApplication(downloadPath);
                        });
                    }
                };
                await client.DownloadFileTaskAsync(new Uri(downloadUrl), downloadPath);
            }
        }

        public string Status { get => _Status; set { _Status = value; NotifyPropertyChanged(); } }
        private string _Status;


        private void RestartApplication(string downloadPath)
        {
            // 启动新的实例
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = downloadPath;
            startInfo.Verb = "runas"; // "runas"指定启动程序时请求管理员权限
            try
            {
                Process p = Process.Start(startInfo);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


    }
}
