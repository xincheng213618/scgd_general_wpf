using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using ColorVision.MVVM;
using ColorVision.MySql;
using log4net;

namespace ColorVision.Update
{
    public class AutoUpdater : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoUpdater));

        private static AutoUpdater _instance;
        private static readonly object _locker = new();
        public static AutoUpdater GetInstance() { lock (_locker) { return _instance ??= new AutoUpdater(); } }

        public string UpdateUrl { get => _UpdateUrl; set { _UpdateUrl = value; NotifyPropertyChanged(); } }
        private string _UpdateUrl = GlobalConst.UpdatePath + "/LATEST_RELEASE";

        public Version LatestVersion { get => _LatestVersion; set { _LatestVersion = value; NotifyPropertyChanged(); } }
        private Version _LatestVersion;


        public AutoUpdater()
        {
            UpdateCommand = new RelayCommand((e) =>  CheckAndUpdate(false));
            DeleteAllCachedUpdateFiles();
        }

        public RelayCommand UpdateCommand { get; set; }

        public static void DeleteAllCachedUpdateFiles()
        {
            // 获取临时文件夹路径
            string tempPath = Path.GetTempPath();

            // 搜索所有匹配的更新文件
            string[] updateFiles = Directory.GetFiles(tempPath, "ColorVision-*.exe");

            foreach (string updateFile in updateFiles)
            {
                try
                {
                    // 删除文件
                    File.Delete(updateFile);
                    log.Info($"Deleted update file: {updateFile}");
                }
                catch (Exception ex)
                {
                    // 如果删除过程中出现错误，输出错误信息
                    log.Info($"Error deleting the update file {updateFile}: {ex.Message}");
                }
            }

            if (updateFiles.Length == 0)
            {
                log.Info($"No update files found to delete.");
            }
        }

        // 调用函数以删除所有更新文件
        public async void CheckAndUpdate(bool detection = true)
        {
            // 获取本地版本
            var localVersion = Assembly.GetExecutingAssembly().GetName().Version;
            try
            {
                // 获取服务器版本
                LatestVersion = await GetLatestVersionNumber(UpdateUrl);

                if (LatestVersion > localVersion)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show($"发现新版本{LatestVersion},是否更新", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            WindowUpdate windowUpdate = new WindowUpdate() {  Owner =Application.Current.MainWindow ,WindowStartupLocation = WindowStartupLocation.CenterOwner};
                            windowUpdate.Show();
                            Task.Run(() => DownloadAndUpdate(LatestVersion));
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
                LatestVersion = localVersion;
                Console.WriteLine("An error occurred while updating: " + ex.Message);
            }
        }
        private static async Task<Version>  GetLatestVersionNumber(string url)
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
            string downloadUrl = $"{GlobalConst.UpdatePath}/ColorVision/ColorVision-{latestVersion}.exe";

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
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            RestartApplication(downloadPath);
                        });
                    }
                };
                await client.DownloadFileTaskAsync(new Uri(downloadUrl), downloadPath);
            }
        }


        private static void RestartApplication(string downloadPath)
        {
            // 保存数据库配置
            ConfigHandler.GetInstance().SaveConfig();


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
