using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;


namespace WindowsServicePlugin.CVWinSMS
{
    internal enum InstallToolUpdateCheckStatus
    {
        ToolMissing,
        UpToDate,
        Unavailable,
    }

    public class InstallTool : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(InstallTool));


        public override string OwnerGuid => "ServiceLog";

        public override string GuidId => "InstallTool";

        public string Name => GetType().Name;   

        public override int Order => 1;

        public override string Header => Properties.Resources.ManagementService;

        public string Description => GetDescription();

        public string GetDescription()
        {
            string Description = "打开已配置的服务管理工具；如果不存在，可确认下载并指定保存位置。更新检查请使用同组的手动检查入口。";
            if (File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                string filePath = Directory.GetParent(CVWinSMSConfig.Instance.CVWinSMSPath) + @"\config\App.config";
                if (File.Exists(filePath))
                {
                    Description += Environment.NewLine + "配置文件路径：" + filePath;
                    Description += Environment.NewLine + File.ReadAllText(filePath);
                }
            }
            return Description;
        }

        public static CVWinSMSConfig Config => CVWinSMSConfig.Instance;
        public static string LatestReleaseUrl => Config.UpdatePath + "/LATEST_RELEASE";


        // Keep the public entry point for callers, but do not enroll an optional
        // external-tool update request in the application's startup initializers.
        public Task Initialize() => GetLatestReleaseVersion();
        public bool ConfigurationStatus { get => _ConfigurationStatus; set { _ConfigurationStatus = value; OnPropertyChanged(); } }
        private bool _ConfigurationStatus = File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath);

        public async Task GetLatestReleaseVersion()
        {
            try
            {
                await CheckForUpdatesAsync(
                    () => File.Exists(Config.CVWinSMSPath)
                        ? new Version(FileVersionInfo.GetVersionInfo(Config.CVWinSMSPath).FileVersion!)
                        : null,
                    () => new DownloadFile().GetLatestVersionNumber(LatestReleaseUrl),
                    ShowUpdateCheckStatus,
                    version =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (MessageBox.Show(Application.Current.GetActiveWindow(), "服务管理工具:找到新版本，是否更新", "CVWinSMS", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            string downloadDir = Environments.DirToolPackageCache;
                            string url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[{version}].zip";

                            var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
                            if (service == null) return;

                            service.ShowDownloadWindow();
                            service.Download(url, downloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
                            {
                                if (filePath == null) return;
                                _ = Task.Run(async () =>
                                {
                                    Process.GetProcessesByName("CVWinSMS").ToList().ForEach(p => p.Kill());
                                    log.Info("正在关闭CVWinSMS");
                                    await Task.Delay(3000);
                                    Application.Current?.Dispatcher.Invoke(() =>
                                    {
                                        try
                                        {
                                            string? folderBrowser = Directory.GetParent(Directory.GetParent(CVWinSMSConfig.Instance.CVWinSMSPath)?.FullName)?.FullName;
                                            if (folderBrowser != null)
                                            {
                                                ZipFile.ExtractToDirectory(filePath, folderBrowser, true);

                                                DirectoryInfo directoryInfo = Directory.GetParent(CVWinSMSConfig.Instance.CVWinSMSPath);
                                                if (directoryInfo.Name != "InstallTool")
                                                {
                                                    string ConfigPath = directoryInfo.FullName + "\\config\\App.config";
                                                    string dirconfig = folderBrowser + "\\InstallTool\\config\\App.config";
                                                    DirectoryInfo targetDirInfo = Directory.GetParent(dirconfig);
                                                    if (!targetDirInfo.Exists)
                                                    {
                                                        targetDirInfo.Create();
                                                    }
                                                    File.Copy(ConfigPath, dirconfig, true);
                                                    directoryInfo.Delete(true);
                                                }
                                                CVWinSMSConfig.Instance.CVWinSMSPath = folderBrowser + "\\InstallTool\\CVWinSMS.exe";
                                            }
                                            else
                                            {
                                                MessageBox.Show("更新失败， 找不到更新所在的文件夹");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show("更新失败，" + ex.Message);
                                        }

                                        ProcessStartInfo startInfo = new()
                                        {
                                            UseShellExecute = true,
                                            WorkingDirectory = Environment.CurrentDirectory,
                                            FileName = CVWinSMSConfig.Instance.CVWinSMSPath,
                                            Verb = "runas"
                                        };
                                        try
                                        {
                                            Process.Start(startInfo);
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show(ex.ToString());
                                            File.Delete(filePath);
                                        }
                                    });
                                });
                            });

                        };

                    });

                });
            }catch(Exception ex)
            {
                ReportUpdateCheckFailure(ex);
            }
        }

        internal static async Task CheckForUpdatesAsync(
            Func<Version?> getInstalledVersion,
            Func<Task<Version>> getLatestVersion,
            Action<InstallToolUpdateCheckStatus> reportStatus,
            Action<Version> offerUpdate)
        {
            Version? installedVersion;
            try
            {
                installedVersion = getInstalledVersion();
            }
            catch (Exception ex)
            {
                log.Error("读取服务管理工具版本失败。", ex);
                reportStatus(InstallToolUpdateCheckStatus.Unavailable);
                return;
            }

            if (installedVersion == null)
            {
                reportStatus(InstallToolUpdateCheckStatus.ToolMissing);
                return;
            }

            Version latestVersion;
            try
            {
                latestVersion = await GetRequiredLatestVersionAsync(getLatestVersion);
            }
            catch (Exception ex)
            {
                log.Error("检查服务管理工具更新失败。", ex);
                reportStatus(InstallToolUpdateCheckStatus.Unavailable);
                return;
            }

            if (latestVersion > installedVersion)
                offerUpdate(latestVersion);
            else
                reportStatus(InstallToolUpdateCheckStatus.UpToDate);
        }

        internal static async Task<Version> GetRequiredLatestVersionAsync(Func<Task<Version>> getLatestVersion)
        {
            Version version = await getLatestVersion();
            if (version == null || version <= new Version(0, 0, 0, 0))
                throw new InvalidOperationException("无法获取有效的服务管理工具版本，请检查网络或更新地址后重试。");
            return version;
        }

        private static void ShowUpdateCheckStatus(InstallToolUpdateCheckStatus status)
        {
            string message = status switch
            {
                InstallToolUpdateCheckStatus.ToolMissing => "未找到旧服务管理工具，请先通过服务管理工具入口指定路径或下载。",
                InstallToolUpdateCheckStatus.UpToDate => "未发现更新，当前服务管理工具可继续使用。",
                _ => "无法检查服务管理工具更新，请检查本地工具、网络或更新地址后重试。",
            };
            Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                Application.Current.GetActiveWindow(), message, "检查旧服务管理工具更新",
                MessageBoxButton.OK,
                status == InstallToolUpdateCheckStatus.Unavailable ? MessageBoxImage.Warning : MessageBoxImage.Information));
        }

        internal static void ReportUpdateCheckFailure(Exception ex)
        {
            log.Error("检查服务管理工具更新失败。", ex);
            Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                Application.Current.GetActiveWindow(), $"检查服务管理工具更新失败：{ex.Message}",
                "检查旧服务管理工具更新", MessageBoxButton.OK, MessageBoxImage.Error));
        }

        public async Task Download()
        {
            var downloadFile = new DownloadFile();
            Version version = await GetRequiredLatestVersionAsync(() => downloadFile.GetLatestVersionNumber(LatestReleaseUrl));
            string downloadDir = Environments.DirToolPackageCache;
            string url = $"http://xc213618.ddns.me:9999/D%3A/ColorVision/Tool/InstallTool/InstallTool[{version}].zip";

            var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
            if (service == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                service.ShowDownloadWindow();
                service.Download(url, downloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
                {
                    if (filePath == null) return;
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        Process.GetProcessesByName("CVWinSMS").ToList().ForEach(p => p.Kill());
                        using (System.Windows.Forms.FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog())
                        {
                            folderBrowser.Description = "请选择解压缩目录";
                            folderBrowser.ShowNewFolderButton = true;
                            folderBrowser.RootFolder = Environment.SpecialFolder.Desktop;
                            if (folderBrowser.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                            ZipFile.ExtractToDirectory(filePath, folderBrowser.SelectedPath, true);

                            CVWinSMSConfig.Instance.CVWinSMSPath = folderBrowser.SelectedPath + "\\InstallTool\\CVWinSMS.exe";
                        }

                        ConfigurationStatus = File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath);

                        ProcessStartInfo startInfo = new()
                        {
                            UseShellExecute = true,
                            WorkingDirectory = Environment.CurrentDirectory,
                            FileName = CVWinSMSConfig.Instance.CVWinSMSPath,
                            Verb = "runas"
                        };
                        try
                        {
                            Process.Start(startInfo);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.ToString());
                            File.Delete(filePath);
                        }
                    });
                });
            });
        }

        public override async void Execute()
        {
            await ExecuteMenuActionAsync(ExecuteCoreAsync, ReportMenuFailure);
        }

        internal static async Task ExecuteMenuActionAsync(Func<Task> action, Action<Exception> reportFailure)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                try
                {
                    reportFailure(ex);
                }
                catch (Exception reportException)
                {
                    log.Error("报告服务管理工具启动失败时发生异常。", reportException);
                }
            }
        }

        private static void ReportMenuFailure(Exception ex)
        {
            log.Error("打开或下载服务管理工具失败。", ex);
            MessageBox.Show(
                Application.Current?.GetActiveWindow(),
                $"打开或下载服务管理工具失败：{ex.Message}",
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private async Task ExecuteCoreAsync()
        {
            if (!File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath))
            {
                Process[] processes = Process.GetProcessesByName("CVWinSMS");
                if (processes.Length == 1)
                {
                    foreach (Process process in processes)
                    {
                        try
                        {
                            // 获取进程的主模块文件路径
                            CVWinSMSConfig.Instance.CVWinSMSPath = process.MainModule.FileName;
                            log.Info($"进程ID: {process.Id}, 文件路径: {CVWinSMSConfig.Instance.CVWinSMSPath}");

                            return;
                        }
                        catch (Exception ex)
                        {
                            log.Debug($"无法获取进程的文件路径: {ex.Message}");
                        }
                    }
                }
                if (MessageBox.Show(Application.Current.GetActiveWindow(), "找不到管理工具，是否下载", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    await Download();
                    return;
                }

                if (MessageBox.Show(Application.Current.GetActiveWindow(), "I can't find CVWinSMS (CVWinSMS.exe). Would you like to help me find it?", "Open in CVWinSMS", MessageBoxButton.YesNo) == MessageBoxResult.No) return;
                using (System.Windows.Forms.OpenFileDialog openFileDialog = new())
                {
                    openFileDialog.Title = "Select CVWinSMS.exe";
                    openFileDialog.Filter = "CVWinSMS.exe|CVWinSMS.exe";
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                    CVWinSMSConfig.Instance.CVWinSMSPath = openFileDialog.FileName;
                }
            }



            ProcessStartInfo startInfo = new();
            startInfo.UseShellExecute = true;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = CVWinSMSConfig.Instance.CVWinSMSPath;
            startInfo.Verb = "runas";
            try
            {
                ConfigurationStatus = File.Exists(CVWinSMSConfig.Instance.CVWinSMSPath);
                Process p = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }


    }
}
