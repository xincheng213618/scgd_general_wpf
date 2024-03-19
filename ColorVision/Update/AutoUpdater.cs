using ColorVision.Common.MVVM;
using ColorVision.Settings;
using log4net;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

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
        }

        public RelayCommand UpdateCommand { get; set; }

        public static void DeleteAllCachedUpdateFiles()
        {
            // 获取临时文件夹路径
            string tempPath = Path.GetTempPath();

            // 搜索所有匹配的更新文件
            string[] updateFiles = Directory.GetFiles(tempPath, "ColorVision-*.exe");

            var localVersion = Assembly.GetExecutingAssembly().GetName().Version;

            foreach (string updateFile in updateFiles)
            {
                try
                {
                    //这里先注释掉，逻辑有些问题
                    //FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(updateFile);
                    //if (localVersion < new Version(fileVersionInfo.FileVersion??string.Empty))
                    //{
                    //    Application.Current.Dispatcher.Invoke(() =>
                    //    {
                    //        RestartApplication(updateFile);
                    //    });
                    //}
                    //else
                    //{
                    //    // 删除文件

                    //}
                    File.Delete(updateFile);
                    log.Info($"Deleted update file: {updateFile}");
                }
                catch (Exception ex)
                {
                    File.Delete(updateFile);
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
                        if (MessageBox.Show($"{Properties.Resource.NewVersionFound}{LatestVersion},{Properties.Resource.ConfirmUpdate}", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
                            WindowUpdate windowUpdate = new WindowUpdate() { Owner = Application.Current.MainWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                            windowUpdate.Closed += (s, e) =>
                            {
                                _cancellationTokenSource.Cancel();
                            };
                            SpeedValue = string.Empty ;
                            RemainingTimeValue = string.Empty;
                            ProgressValue = 0;
                            Task.Run(() => DownloadAndUpdate(LatestVersion, _cancellationTokenSource.Token));
                            windowUpdate.Show();
                        }
                    });              
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (detection)
                            MessageBox.Show(Properties.Resource.CurrentVersionIsUpToDate, "ColorVision", MessageBoxButton.OK);
                    });

                }
            }
            catch (Exception ex)
            {
                LatestVersion = localVersion??new Version();
                Console.WriteLine("An error occurred while updating: " + ex.Message);
            }
        }
        bool IsPassWorld;


        private async Task<Version> GetLatestVersionNumber(string url)
        {
            using HttpClient _httpClient = new HttpClient();
            string versionString = null;
            try
            {
                // First attempt to get the string without authentication
                versionString = await _httpClient.GetStringAsync(url);
            }
            catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                IsPassWorld = true;
                // If the request is unauthorized, add the authentication header and try again
                var byteArray = System.Text.Encoding.ASCII.GetBytes("1:1");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                // You should also consider handling other potential issues here, such as network errors
                versionString = await _httpClient.GetStringAsync(url);
            }

            // If versionString is still null, it means there was an issue with getting the version number
            if (versionString == null)
            {
                throw new InvalidOperationException("Unable to retrieve version number.");
            }

            return new Version(versionString.Trim());
        }

        public int ProgressValue { get => _ProgressValue; set { _ProgressValue = value; NotifyPropertyChanged(); } }
        private int _ProgressValue;

        public string SpeedValue { get => _SpeedValue; set { _SpeedValue = value; NotifyPropertyChanged(); } }
        private string _SpeedValue;

        public string RemainingTimeValue { get => _RemainingTimeValue; set { _RemainingTimeValue = value; NotifyPropertyChanged(); } }
        private string _RemainingTimeValue;


        private async Task DownloadAndUpdate(Version latestVersion,CancellationToken cancellationToken)
        {
            // 构建下载URL，这里假设下载路径与版本号相关
            string downloadUrl = $"{GlobalConst.UpdatePath}/ColorVision/ColorVision-{latestVersion}.exe";

            // 指定下载路径
            string downloadPath = Path.Combine(Path.GetTempPath(), $"ColorVision-{latestVersion}.exe");

            using (var client = new HttpClient())
            {
                if (IsPassWorld)
                {
                    string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{1}:{1}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                }

                Stopwatch stopwatch = new Stopwatch();

                var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    MessageBox.Show($"{Properties.Resource.ErrorOccurred}: {response.ReasonPhrase}");
                    return;
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var totalReadBytes = 0L;
                var readBytes = 0;
                var buffer = new byte[8192];
                var isMoreToRead = true;

                stopwatch.Start();

                using (var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    do
                    {
                        readBytes = await stream.ReadAsync(buffer);
                        if (readBytes == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            await fileStream.WriteAsync(buffer.AsMemory(0, readBytes));

                            totalReadBytes += readBytes;
                            int progressPercentage = totalBytes != -1L
                                ? (int)((totalReadBytes * 100) / totalBytes)
                                : -1;

                            ProgressValue = progressPercentage;

                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            if (stopwatch.ElapsedMilliseconds > 200) // Update speed at least once per second
                            {
                                double speed = totalReadBytes / stopwatch.Elapsed.TotalSeconds;
                                SpeedValue = $"{ColorVision.Properties.Resource.CurrentSpeed} {speed / 1024 / 1024:F2} MB/s";

                                if (totalBytes != -1L)
                                {
                                    double remainingBytes = totalBytes - totalReadBytes;
                                    double remainingTime = remainingBytes / speed; // in seconds
                                    RemainingTimeValue = $"{ColorVision.Properties.Resource.TimeLeft} {TimeSpan.FromSeconds(remainingTime):hh\\:mm\\:ss}";
                                }
                            }
                        }
                    }
                    while (isMoreToRead);
                }

                stopwatch.Stop();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    RestartApplication(downloadPath);
                });
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
