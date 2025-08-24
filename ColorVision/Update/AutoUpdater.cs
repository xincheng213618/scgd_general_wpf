﻿#pragma warning disable CS8604,CA1822
using ColorVision.Common.MVVM;
using ColorVision.Properties;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{

    public class AutoUpdateConfig:ViewModelBase, IConfig
    {
        public static AutoUpdateConfig Instance  => ConfigService.Instance.GetRequiredService<AutoUpdateConfig>();    

        public string UpdatePath { get => _UpdatePath; set { _UpdatePath = value; OnPropertyChanged(); } }
        private string _UpdatePath = "http://xc213618.ddns.me:9999/D%3A/ColorVision";

        /// <summary>
        /// 是否自动更新
        /// </summary>
        public bool IsAutoUpdate { get => _IsAutoUpdate; set { _IsAutoUpdate = value; OnPropertyChanged(); } }
        private bool _IsAutoUpdate = true;

    }


    public class AutoUpdater : ViewModelBase,IUpdate
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoUpdater));
        private static AutoUpdater _instance;
        private static readonly object _locker = new();
        public static AutoUpdater GetInstance() { lock (_locker) { return _instance ??= new AutoUpdater(); } }
        
        public string UpdateUrl { get => _UpdateUrl; set { _UpdateUrl = value; OnPropertyChanged(); } }
        private string _UpdateUrl = AutoUpdateConfig.Instance.UpdatePath + "/LATEST_RELEASE";

        public string CHANGELOGUrl { get => _CHANGELOG; set { _CHANGELOG = value; OnPropertyChanged(); } }
        private string _CHANGELOG = AutoUpdateConfig.Instance.UpdatePath + "/CHANGELOG.md";

        public Version LatestVersion { get => _LatestVersion; set { _LatestVersion = value; OnPropertyChanged(); } }
        private Version _LatestVersion;


        public AutoUpdater()
        {
            UpdateCommand = new RelayCommand( async (e) => await CheckAndUpdate(false));
        }

        public RelayCommand UpdateCommand { get; set; }

        public static void DeleteAllCachedUpdateFiles()
        {
            string tempPath = Path.GetTempPath();
            string[] updatePatterns = { "ColorVision-*.exe", "ColorVision-*.zip" };
            foreach (string pattern in updatePatterns)
            {
                string[] updateFiles = Directory.GetFiles(tempPath, pattern);
                foreach (string updateFile in updateFiles)
                {
                    try
                    {
                        File.Delete(updateFile);
                        log.Info($"Deleted update file: {updateFile}");
                    }
                    catch (Exception ex)
                    {
                        // 如果删除过程中出现错误，输出错误信息
                        log.Info($"Error deleting the update file {updateFile}: {ex.Message}");
                    }
                }
            }
        }

        public static Version? CurrentVersion { get => Assembly.GetExecutingAssembly().GetName().Version; }

        public static bool IsUpdateAvailable(string Version)
        {
            return true;
        }
        public void Update(string Version, string DownloadPath) => Update(new Version(Version.Trim()), DownloadPath);
        public void Update(Version Version, string DownloadPath,bool IsIncrement = false)
        {
            CancellationTokenSource _cancellationTokenSource = new();
            WindowUpdate windowUpdate = new WindowUpdate(this) { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            windowUpdate.Title = $"正在下载 {Version} {(IsIncrement?"增量":"")}更新";
            windowUpdate.Closed += (s, e) =>
            {
                _cancellationTokenSource.Cancel();
            };
            SpeedValue = string.Empty;
            RemainingTimeValue = string.Empty;
            ProgressValue = 0;
            Task.Run(() => DownloadAndUpdate(Version, DownloadPath, _cancellationTokenSource.Token, IsIncrement));
            windowUpdate.Show();
        }

        public async Task ForceUpdate()
        {
            LatestVersion = await GetLatestVersionNumber(UpdateUrl);
            if (LatestVersion == new Version()) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Update(LatestVersion, Path.GetTempPath());
            });
        }

        public async Task CheckAndUpdateV1(bool detection = true)
        {
            // 获取本地版本
            try
            {
                // 获取服务器版本
                LatestVersion = await GetLatestVersionNumber(UpdateUrl);
                if (LatestVersion == new Version()) return;

                var Version = Assembly.GetExecutingAssembly().GetName().Version;
                if (LatestVersion > Version)
                {
                    bool IsIncrement = false;
                    if (LatestVersion.Minor == Version.Minor)
                        IsIncrement = true;
                    if (IsIncrement && LatestVersion.Build != Version.Build)
                    {
                        LatestVersion = new Version(Version.Major, Version.Minor, Version.Build + 1, 1);
                    }

                    string CHANGELOG = await GetChangeLog(CHANGELOGUrl);
                    string versionPattern = $"## \\[{LatestVersion}\\].*?\\n(.*?)(?=\\n## |$)";
                    Match match = Regex.Match(CHANGELOG ?? string.Empty, versionPattern, RegexOptions.Singleline);
                    if (match.Success)
                    {
                        // 如果找到匹配项，提取变更日志
                        string changeLogForCurrentVersion = match.Groups[1].Value.Trim();

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (MessageBox1.Show(Application.Current.GetActiveWindow(), $"{changeLogForCurrentVersion}{Environment.NewLine}{Environment.NewLine}{Properties.Resources.ConfirmUpdate}?", $"{Properties.Resources.NewVersionFound}{LatestVersion}", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                            {
                                Update(LatestVersion, Path.GetTempPath(), IsIncrement);
                            }
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (MessageBox1.Show(Application.Current.GetActiveWindow(), $"{Properties.Resources.NewVersionFound}{LatestVersion},{Properties.Resources.ConfirmUpdate}", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                            {
                                Update(LatestVersion, Path.GetTempPath(), IsIncrement);
                            }
                        });
                    }
                }
                else
                {
                    if (detection)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.CurrentVersionIsUpToDate, Version?.ToString(), MessageBoxButton.OK);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LatestVersion = CurrentVersion ?? new Version();
                MessageBox.Show(ex.Message);
                log.Info(ex);
            }
        }


        // 调用函数以删除所有更新文件
        public async Task CheckAndUpdate(bool detection = true,bool IsIncrement = false)
        {
            // 获取本地版本
            try
            {
                // 获取服务器版本
                LatestVersion = await GetLatestVersionNumber(UpdateUrl);
                if (LatestVersion == new Version()) return;

                var Version = Assembly.GetExecutingAssembly().GetName().Version;
                if (LatestVersion > Version)
                {
                    if (IsIncrement && LatestVersion.Build != Version.Build)
                    {
                        LatestVersion = new Version(LatestVersion.Major, LatestVersion.Minor, LatestVersion.Build + 1, 1);
                    }

                    string CHANGELOG = await GetChangeLog(CHANGELOGUrl);
                    string versionPattern = $"## \\[{LatestVersion}\\].*?\\n(.*?)(?=\\n## |$)";
                    Match match = Regex.Match(CHANGELOG??string.Empty, versionPattern, RegexOptions.Singleline);
                    if (match.Success)
                    {
                        // 如果找到匹配项，提取变更日志
                        string changeLogForCurrentVersion = match.Groups[1].Value.Trim();

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (MessageBox1.Show(Application.Current.GetActiveWindow(),$"{changeLogForCurrentVersion}{Environment.NewLine}{Environment.NewLine}{Properties.Resources.ConfirmUpdate}?",$"{ Properties.Resources.NewVersionFound}{ LatestVersion}", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                            {
                                Update(LatestVersion, Path.GetTempPath(), IsIncrement);
                            }
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (MessageBox1.Show(Application.Current.GetActiveWindow(),$"{Properties.Resources.NewVersionFound}{LatestVersion},{Properties.Resources.ConfirmUpdate}", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                            {
                                Update(LatestVersion, Path.GetTempPath(), IsIncrement);
                            }
                        });
                    }
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (detection)
                            MessageBox1.Show(Application.Current.GetActiveWindow(),Properties.Resources.CurrentVersionIsUpToDate, "ColorVision", MessageBoxButton.OK);
                    });

                }
            }
            catch (Exception ex)
            {
                LatestVersion = CurrentVersion??new Version();
                log.Info(ex);
            }
        }

        public async Task<string?> GetChangeLog(string url)
        {
            using HttpClient _httpClient = new();
            string versionString = null;
            try
            {
                if (DownloadFileConfig.Instance.IsPassWorld)
                {
                    var byteArray = Encoding.ASCII.GetBytes("1:1");
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                }
                // First attempt to get the string without authentication
                versionString = await _httpClient.GetStringAsync(url);
            }
            catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                DownloadFileConfig.Instance.IsPassWorld = true;
                // If the request is unauthorized, add the authentication header and try again
                var byteArray = Encoding.ASCII.GetBytes("1:1");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                // You should also consider handling other potential issues here, such as network errors
                versionString = await _httpClient.GetStringAsync(url);
            }

            // If versionString is still null, it means there was an issue with getting the version number
            if (versionString == null)
            {
                return null;
            }

            return versionString;
        }



        public async Task<Version> GetLatestVersionNumber(string url)
        {
            using HttpClient _httpClient = new();
            string versionString = null;
            try
            {
                if (DownloadFileConfig.Instance.IsPassWorld)
                {
                    var byteArray = Encoding.ASCII.GetBytes("1:1");
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                }
                // First attempt to get the string without authentication
                versionString = await _httpClient.GetStringAsync(url);
            }
            catch (HttpRequestException e) when (e.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                DownloadFileConfig.Instance.IsPassWorld = true;
                // If the request is unauthorized, add the authentication header and try again
                var byteArray = Encoding.ASCII.GetBytes("1:1");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                // You should also consider handling other potential issues here, such as network errors
                versionString = await _httpClient.GetStringAsync(url);
            }
            catch(Exception ex)
            {
                log.Error(ex);
                //MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                DownloadFileConfig.Instance.IsPassWorld = false;
                return new Version();
            }

            // If versionString is still null, it means there was an issue with getting the version number
            if (versionString == null)
            {
                throw new InvalidOperationException("Unable to retrieve version number.");
            }

            return new Version(versionString.Trim());
        }

        public int ProgressValue { get => _ProgressValue; set { _ProgressValue = value; OnPropertyChanged(); } }
        private int _ProgressValue;

        public string SpeedValue { get => _SpeedValue; set { _SpeedValue = value; OnPropertyChanged(); } }
        private string _SpeedValue;

        public string RemainingTimeValue { get => _RemainingTimeValue; set { _RemainingTimeValue = value; OnPropertyChanged(); } }
        private string _RemainingTimeValue;

        public string DownloadTile { get => _DownloadTile; set{ _DownloadTile = value; OnPropertyChanged(); } }
        private string _DownloadTile = Resources.ColorVisionUpdater;


        private async Task DownloadAndUpdate(Version latestVersion, string downloadPath, CancellationToken cancellationToken, bool isIncrement = false)
        {
            string downloadUrl;
            string filePath;

            if (isIncrement)
            {
                downloadUrl = $"{AutoUpdateConfig.Instance.UpdatePath}/Update/ColorVision-Update-[{latestVersion}].zip";
                filePath = Path.Combine(downloadPath, $"ColorVision-Update-[{latestVersion}].zip");
            }
            else
            {
                downloadUrl = $"{AutoUpdateConfig.Instance.UpdatePath}/ColorVision-{latestVersion}.exe";
                filePath = Path.Combine(downloadPath, $"ColorVision-{latestVersion}.exe");
            }

            await DownloadFileAsync(downloadUrl, filePath, cancellationToken);
            UpdateApplication(filePath, isIncrement);
        }

        private async Task SilenceDownloadAndUpdate(Version latestVersion, string downloadPath, CancellationToken cancellationToken)
        {
            string downloadUrl;
            string filePath;
            downloadUrl = $"{AutoUpdateConfig.Instance.UpdatePath}/Update/ColorVision-Update-[{latestVersion}].zip";
            filePath = Path.Combine(downloadPath, $"ColorVision-Update-[{latestVersion}].zip");

            await DownloadFileAsync(downloadUrl, filePath, cancellationToken);


            void update()
            {
                try
                {
                    // 解压缩 ZIP 文件到临时目录
                    string tempDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionUpdate");
                    if (Directory.Exists(tempDirectory))
                    {
                        Directory.Delete(tempDirectory, true);
                    }
                    ZipFile.ExtractToDirectory(filePath, tempDirectory);

                    // 创建批处理文件内容
                    string batchFilePath = Path.Combine(tempDirectory, "update.bat");
                    string programDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string? executableName = Path.GetFileName(Environment.ProcessPath);

                    string batchContent = $@"
@echo off
taskkill /f /im ""{executableName}""
timeout /t 0
xcopy /y /e ""{tempDirectory}\*"" ""{programDirectory}""
rd /s /q ""{tempDirectory}""
del ""%~f0"" & exit
";

                    File.WriteAllText(batchFilePath, batchContent);
                    // 设置批处理文件的启动信息
                    ProcessStartInfo startInfo = new()
                    {
                        FileName = batchFilePath,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden // 隐藏命令行窗口
                    };

                    if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
                    {
                        startInfo.Verb = "runas"; // 请求管理员权限
                        startInfo.WindowStyle = ProcessWindowStyle.Normal;
                    }

                    // 启动批处理文件并退出当前程序
                    Process.Start(startInfo);
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"更新失败: {ex.Message}");
                }
            }
            AppDomain.CurrentDomain.ProcessExit += (s, e) => update();
        }

        private void UpdateApplication(string downloadPath, bool isIncrement)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (isIncrement)
                {
                    RestartIsIncrementApplication(downloadPath);
                }
                else
                {
                    RestartApplication(downloadPath);
                }
            });
        }

        private async Task DownloadFileAsync(string url, string downloadPath, CancellationToken cancellationToken)
        {
            using var client = new HttpClient();

            if (DownloadFileConfig.Instance.IsPassWorld)
            {
                string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{1}:{1}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }

            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"{Properties.Resources.ErrorOccurred}: {response.ReasonPhrase}");
                return;
            }

            double totalBytes = response.Content.Headers.ContentLength ?? -1L;
            double totalReadBytes = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;

            using var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            Stopwatch stopwatch = new();
            stopwatch.Start();

            do
            {
                var readBytes = await stream.ReadAsync(buffer, cancellationToken);
                if (readBytes == 0)
                {
                    isMoreToRead = false;
                }
                else
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, readBytes), cancellationToken);
                    totalReadBytes += readBytes;

                    int progressPercentage = totalBytes != -1L ? (int)((totalReadBytes * 100) / totalBytes) : -1;
                    ProgressValue = progressPercentage;

                    if (cancellationToken.IsCancellationRequested)
                    {
                        /// 取消下载后删除未下载的文件
                        File.Delete(downloadPath);
                        return;
                    }

                    if (stopwatch.ElapsedMilliseconds > 200)
                    {
                        double speed = totalReadBytes / stopwatch.Elapsed.TotalSeconds;
                        SpeedValue = $"{Properties.Resources.CurrentSpeed} {speed / 1024 / 1024:F2} MB/s   {totalReadBytes / 1024 / 1024:F2} MB/{totalBytes / 1024 / 1024:F2} MB";

                        if (totalBytes != -1L)
                        {
                            double remainingBytes = totalBytes - totalReadBytes;
                            double remainingTime = remainingBytes / speed;
                            RemainingTimeValue = $"{Properties.Resources.TimeLeft} {TimeSpan.FromSeconds(remainingTime):hh\\:mm\\:ss}";
                        }
                    }
                }
            } while (isMoreToRead);

            stopwatch.Stop();
        }


        public static void RestartIsIncrementApplication(string downloadPath)
        {
            // 保存数据库配置
            ConfigHandler.GetInstance().SaveConfigs();
            try
            {
                // 解压缩 ZIP 文件到临时目录
                string tempDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionUpdate");
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
                ZipFile.ExtractToDirectory(downloadPath, tempDirectory);

                // 创建批处理文件内容
                string batchFilePath = Path.Combine(tempDirectory, "update.bat");
                string programDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string? executableName = Path.GetFileName(Environment.ProcessPath);

                string batchContent = $@"
@echo off
taskkill /f /im ""{executableName}""
timeout /t 0
xcopy /y /e ""{tempDirectory}\*"" ""{programDirectory}""
start """" ""{Path.Combine(programDirectory, executableName)}""
rd /s /q ""{tempDirectory}""
del ""%~f0"" & exit
";

                File.WriteAllText(batchFilePath, batchContent);

                // 设置批处理文件的启动信息
                ProcessStartInfo startInfo = new()
                {
                    FileName = batchFilePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden // 隐藏命令行窗口
                };

                if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
                {
                    startInfo.Verb = "runas"; // 请求管理员权限
                    startInfo.WindowStyle = ProcessWindowStyle.Normal;
                }
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
            catch (Exception ex)
            {
                MessageBox.Show($"更新失败: {ex.Message}");
            }
        }

        public static void RestartApplication(string downloadPath)
        {
            // 保存数据库配置
            ConfigHandler.GetInstance().SaveConfigs();


            // 启动新的实例
            ProcessStartInfo startInfo = new();
            startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = downloadPath;
            if (Environment.CurrentDirectory.Contains("C:\\Program Files"))
            {
                startInfo.Verb = "runas"; // 请求管理员权限
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
            }
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
