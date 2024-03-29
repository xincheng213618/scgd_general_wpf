using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ColorVision.Common.MVVM;

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

        private bool IsPassWorld;

        private async Task<Version>  GetLatestVersionNumber(string url)
        {
            using (HttpClient _httpClient = new HttpClient())
            {
                string versionString = null;
                try
                {
                    // First attempt to get the string without authentication
                    versionString = await _httpClient.GetStringAsync(url);
                }
                catch (HttpRequestException)
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
        }


        public int ProgressValue { get => _ProgressValue; set { _ProgressValue = value; NotifyPropertyChanged(); } }
        private int _ProgressValue;

        public string SpeedValue { get => _SpeedValue; set { _SpeedValue = value; NotifyPropertyChanged(); } }
        private string _SpeedValue;

        public string RemainingTimeValue { get => _RemainingTimeValue; set { _RemainingTimeValue = value; NotifyPropertyChanged(); } }
        private string _RemainingTimeValue;
        private async Task DownloadAndUpdate(Version latestVersion)
        {
            // 构建下载URL，这里假设下载路径与版本号相关
            string downloadUrl = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/ColorVision-{latestVersion}.exe";

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
                    MessageBox.Show("下载失败");
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
                        readBytes = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (readBytes == 0)
                        {
                            isMoreToRead = false;
                        }
                        else
                        {
                            await fileStream.WriteAsync(buffer, 0, readBytes);

                            totalReadBytes += readBytes;
                            int progressPercentage = totalBytes != -1L
                                ? (int)((totalReadBytes * 100) / totalBytes)
                                : -1;

                            ProgressValue = progressPercentage;

                            if (stopwatch.ElapsedMilliseconds > 500) // Update speed at least once per second
                            {
                                double speed = totalReadBytes / stopwatch.Elapsed.TotalSeconds;
                                SpeedValue = $"Current speed: {speed / 1024 / 1024:F2} MB/s";

                                if (totalBytes != -1L)
                                {
                                    double remainingBytes = totalBytes - totalReadBytes;
                                    double remainingTime = remainingBytes / speed; // in seconds
                                    RemainingTimeValue = $"Time left: {TimeSpan.FromSeconds(remainingTime):hh\\:mm\\:ss}";
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
