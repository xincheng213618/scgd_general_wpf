using ColorVision.Common.MVVM;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Desktop.Download;
using log4net;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
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
        [DisplayName("CheckUpdatesOnStartup")]
        public bool IsAutoUpdate { get => _IsAutoUpdate; set { _IsAutoUpdate = value; OnPropertyChanged(); } }
        private bool _IsAutoUpdate = true;

        /// <summary>
        /// 用户选择跳过的版本
        /// </summary>
        public string SkippedVersion { get => _SkippedVersion; set { _SkippedVersion = value; OnPropertyChanged(); } }
        private string _SkippedVersion = string.Empty;

    }


    public class AutoUpdater : ViewModelBase
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

        public static Version? CurrentVersion { get => Assembly.GetExecutingAssembly().GetName().Version; }

        public void Update(string Version, string DownloadPath) => Update(new Version(Version.Trim()), DownloadPath);

        public void Update(Version Version, string DownloadPath,bool IsIncrement = false)
        {
            string downloadUrl;
            string filePath;

            if (IsIncrement)
            {
                downloadUrl = $"{AutoUpdateConfig.Instance.UpdatePath}/Update/ColorVision-Update-[{Version}].cvx";
                filePath = Path.Combine(DownloadPath, $"ColorVision-Update-[{Version}].cvx");
            }
            else
            {
                downloadUrl = $"{AutoUpdateConfig.Instance.UpdatePath}/ColorVision-{Version}.exe";
                filePath = Path.Combine(DownloadPath, $"ColorVision-{Version}.exe");
            }
            Action<DownloadTask>? taskCallback;
            taskCallback = task =>
            {
                if (task.Status == DownloadStatus.Completed)
                {
                    UpdateApplication(task.SavePath, IsIncrement);
                }
                else
                {
                    log.Error($"Download failed via IDownloadService: {downloadUrl}");
                }
            };
            string auth = "1:1";
            DownloadWindow.ShowInstance();
            Aria2cDownloadManager.GetInstance().AddDownload(downloadUrl, DownloadPath, "1:1", taskCallback);
        }

        public async Task ForceUpdate()
        {
            LatestVersion = await GetLatestVersionNumber(UpdateUrl);
            if (LatestVersion == new Version()) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Update(LatestVersion, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision"));
            });
        }

        public async Task CheckAndUpdateV1(bool detection = true,bool skipped =false)
        {
            // 获取本地版本
            try
            {
                // 获取服务器版本
                LatestVersion = await GetLatestVersionNumber(UpdateUrl);
                log.Info(LatestVersion);
                if (LatestVersion == new Version()) return;

                var Version = Assembly.GetExecutingAssembly().GetName().Version;
                if (LatestVersion > Version)
                {
                    // 检查是否是用户已跳过的版本
                    if (skipped)
                    {
                        if (!string.IsNullOrEmpty(AutoUpdateConfig.Instance.SkippedVersion))
                        {
                            try
                            {
                                Version skippedVersion = new Version(AutoUpdateConfig.Instance.SkippedVersion);
                                if (LatestVersion == skippedVersion)
                                {
                                    return;
                                }
                            }
                            catch
                            {
                                AutoUpdateConfig.Instance.SkippedVersion = string.Empty;
                            }
                        }

                    }



                    bool IsIncrement = false;
                    if (LatestVersion.Minor == Version.Minor)
                        IsIncrement = true;
                    if (IsIncrement)
                    {
                        if (LatestVersion.Build != Version.Build)
                            LatestVersion = new Version(Version.Major, Version.Minor, Version.Build + 1, 1);
                    }



                    string CHANGELOG = await GetChangeLog(CHANGELOGUrl);
                    string versionPattern = $"## \\[{LatestVersion}\\].*?\\n(.*?)(?=\\n## |$)";
                    Match match = Regex.Match(CHANGELOG ?? string.Empty, versionPattern, RegexOptions.Singleline);
                    string msg = string.Empty;
                    if (match.Success)
                    {
                        // 如果找到匹配项，提取变更日志
                        string changeLogForCurrentVersion = match.Groups[1].Value.Trim();
                        msg = $"{changeLogForCurrentVersion}{Environment.NewLine}{Environment.NewLine}{Properties.Resources.ConfirmUpdate}?{Environment.NewLine}{Environment.NewLine}{ColorVision.Properties.Resources.ClickYesToUpdateNow}";
                    }
                    else
                    {
                        msg = $"{Properties.Resources.NewVersionFound}{LatestVersion},{Properties.Resources.ConfirmUpdate}{Environment.NewLine}{ColorVision.Properties.Resources.ClickYesToUpdateNow}";
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBoxResult result = MessageBox1.Show(Application.Current.GetActiveWindow(), msg, $"{Properties.Resources.NewVersionFound}{LatestVersion}", MessageBoxButton.YesNoCancel);
                        if (result == MessageBoxResult.Yes)
                        {
                            Update(LatestVersion, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision"), IsIncrement);
                        }
                        else if (result == MessageBoxResult.No)
                        {
                            // 用户选择跳过该版本
                            AutoUpdateConfig.Instance.SkippedVersion = LatestVersion.ToString();
                        }
                    });
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
                                Update(LatestVersion, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision"), IsIncrement);
                            }
                        });
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (MessageBox1.Show(Application.Current.GetActiveWindow(),$"{Properties.Resources.NewVersionFound}{LatestVersion},{Properties.Resources.ConfirmUpdate}", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                            {
                                Update(LatestVersion, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision"), IsIncrement);
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
                var byteArray = Encoding.ASCII.GetBytes(DownloadFileConfig.Instance.Authorization);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                // First attempt to get the string without authentication
                versionString = await _httpClient.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
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
                var byteArray = Encoding.ASCII.GetBytes(DownloadFileConfig.Instance.Authorization);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                // First attempt to get the string without authentication
                versionString = await _httpClient.GetStringAsync(url);
            }
            catch(Exception ex)
            {
                log.Error(ex);
                return new Version();
            }

            // If versionString is still null, it means there was an issue with getting the ServiceVersion number
            if (versionString == null)
            {
                throw new InvalidOperationException("Unable to retrieve version number.");
            }

            return new Version(versionString.Trim());
        }

        private void UpdateApplication(string downloadPath, bool isIncrement)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ConfigHandler.GetInstance().SaveConfigs();

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


        public static void RestartIsIncrementApplication(string downloadPath)
        {
            // 保存数据库配置
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
timeout /t 3
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
                MessageBox.Show(ColorVision.Properties.Resources.UpdateFailed+$": {ex.Message}");
            }
        }

        public static void RestartApplication(string downloadPath)
        {
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
