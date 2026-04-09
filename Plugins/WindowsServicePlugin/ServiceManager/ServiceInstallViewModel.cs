using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using log4net;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    public class ServiceInstallViewModel : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceInstallViewModel));
        private int _isCheckingUpdate;

        public ServiceManagerConfig Config => ServiceManagerConfig.Instance;

        public string LogText { get => _logText; set { _logText = value; OnPropertyChanged(); } }
        private string _logText = string.Empty;

        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
        private bool _isBusy;

        public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); } }
        private double _progress;

        public string ProgressText { get => _progressText; set { _progressText = value; OnPropertyChanged(); } }
        private string _progressText = string.Empty;

        public string UpdateStatusText { get => _updateStatusText; set { _updateStatusText = value; OnPropertyChanged(); } }
        private string _updateStatusText = "点击更新按钮下载服务包";

        public string ServicePackagePath { get => _servicePackagePath; set { _servicePackagePath = value; OnPropertyChanged(); } }
        private string _servicePackagePath = string.Empty;

        public string MySqlPackagePath { get => _mySqlPackagePath; set { _mySqlPackagePath = value; OnPropertyChanged(); } }
        private string _mySqlPackagePath = string.Empty;

        public string MqttInstallerPath { get => _mqttInstallerPath; set { _mqttInstallerPath = value; OnPropertyChanged(); } }
        private string _mqttInstallerPath = string.Empty;

        public bool AutoStartAfterInstall { get => _autoStartAfterInstall; set { _autoStartAfterInstall = value; OnPropertyChanged(); } }
        private bool _autoStartAfterInstall = true;

        public bool AutoUpdateDatabase { get => _autoUpdateDatabase; set { _autoUpdateDatabase = value; OnPropertyChanged(); } }
        private bool _autoUpdateDatabase;

        public bool BackupBeforeInstall { get => _backupBeforeInstall; set { _backupBeforeInstall = value; OnPropertyChanged(); } }
        private bool _backupBeforeInstall;

        public bool BackupServiceBeforeInstall { get => _backupServiceBeforeInstall; set { _backupServiceBeforeInstall = value; OnPropertyChanged(); } }
        private bool _backupServiceBeforeInstall;

        public bool BackupServiceCfgOnly { get => _backupServiceCfgOnly; set { _backupServiceCfgOnly = value; OnPropertyChanged(); } }
        private bool _backupServiceCfgOnly;

        public bool InstallServiceChecked
        {
            get => Config.InstallServiceChecked;
            set { Config.InstallServiceChecked = value; OnPropertyChanged(); }
        }

        public bool InstallMySqlChecked
        {
            get => Config.InstallMySqlChecked;
            set { Config.InstallMySqlChecked = value; OnPropertyChanged(); }
        }

        public bool InstallMqttChecked
        {
            get => Config.InstallMqttChecked;
            set { Config.InstallMqttChecked = value; OnPropertyChanged(); }
        }

        public string DownloadLocation
        {
            get => Config.DownloadLocation;
            set { Config.DownloadLocation = value; OnPropertyChanged(); }
        }

        public RelayCommand CheckUpdateCommand { get; }
        public RelayCommand DownloadMySqlCommand { get; }
        public RelayCommand DownloadMqttCommand { get; }
        public RelayCommand SelectServicePackageCommand { get; }
        public RelayCommand SelectMySqlZipCommand { get; }
        public RelayCommand SelectMqttInstallerCommand { get; }
        public RelayCommand ClearLogCommand { get; }
        public RelayCommand BackupNowCommand { get; }
        public RelayCommand RestoreBackupCommand { get; }
        public RelayCommand BackupServiceNowCommand { get; }
        public RelayCommand RestoreServiceBackupCommand { get; }
        public RelayCommand SelectDownloadLocationCommand { get; }
        public RelayCommand DoInstallCommand { get; }
        public RelayCommand OneKeyInstallAllCommand { get; }

        public ServiceInstallViewModel()
        {
            CheckUpdateCommand = new RelayCommand(a => _ = DownloadLatestServicePackageAsync(), a => !IsBusy);
            DownloadMySqlCommand = new RelayCommand(a => _ = DownloadMySqlAsync(), a => !IsBusy);
            DownloadMqttCommand = new RelayCommand(a => _ = DownloadMqttAsync(), a => !IsBusy);
            SelectServicePackageCommand = new RelayCommand(a => SelectServicePackage());
            SelectMySqlZipCommand = new RelayCommand(a => SelectMySqlZip());
            SelectMqttInstallerCommand = new RelayCommand(a => SelectMqttInstaller());
            ClearLogCommand = new RelayCommand(a => LogText = string.Empty);
            BackupNowCommand = new RelayCommand(a => _ = Task.Run(() => DoBackupNow()), a => !IsBusy);
            RestoreBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoRestoreBackup()), a => !IsBusy);
            BackupServiceNowCommand = new RelayCommand(a => _ = Task.Run(() => DoBackupServiceNow()), a => !IsBusy);
            RestoreServiceBackupCommand = new RelayCommand(a => _ = Task.Run(() => DoRestoreServiceBackup()), a => !IsBusy);
            SelectDownloadLocationCommand = new RelayCommand(a => SelectDownloadLocation());
            DoInstallCommand = new RelayCommand(a => _ = ExecuteInstallAsync(), a => !IsBusy);
            OneKeyInstallAllCommand = new RelayCommand(a => _ = OneKeyInstallAllAsync(), a => !IsBusy);
        }

        private async Task DownloadLatestServicePackageAsync()
        {
            if (Interlocked.Exchange(ref _isCheckingUpdate, 1) == 1)
            {
                AddLog("正在更新中，请勿重复点击");
                return;
            }

            try
            {
                SetBusy(true, "正在检查更新...");
                var latest = await FindLatestServicePackageAsync();
                if (latest == null)
                {
                    UpdateStatusText = "未找到可用服务包";
                    AddLog("未找到可用服务包");
                    return;
                }

                string downloadDir = EnsureServiceDownloadDirectory();
                if (string.IsNullOrWhiteSpace(downloadDir))
                    return;

                string targetPath = Path.Combine(downloadDir, latest.FileName);
                UpdateStatusText = $"发现新版本: {latest.Version}";
                AddLog($"发现新版本: {latest.Version}");

                bool ok = await DownloadFileToAsync(latest.DownloadUrl, targetPath);
                if (!ok)
                {
                    UpdateStatusText = "下载失败";
                    return;
                }

                ServicePackagePath = targetPath;
                UpdateStatusText = $"下载完成: {targetPath}";
                AddLog($"下载完成: {targetPath}");
            }
            catch (Exception ex)
            {
                UpdateStatusText = $"更新失败: {ex.Message}";
                AddLog($"更新失败: {ex.Message}");
                log.Error("更新失败", ex);
            }
            finally
            {
                SetBusy(false);
                Interlocked.Exchange(ref _isCheckingUpdate, 0);
            }
        }

        private async Task DownloadMySqlAsync()
        {
            string downloadDir = EnsureServiceDownloadDirectory();
            if (string.IsNullOrWhiteSpace(downloadDir))
                return;

            SetBusy(true, "正在下载 MySQL...");
            try
            {
                foreach (string baseUrl in GetApiBaseCandidates(Config.UpdateServerUrl))
                {
                    string fileName = "mysql-5.7.37-winx64.zip";
                    string url = baseUrl.TrimEnd('/') + "/download/Tool/Mysql/" + fileName;
                    string targetPath = Path.Combine(downloadDir, fileName);
                    if (await DownloadFileToAsync(url, targetPath, swallowError: true))
                    {
                        MySqlPackagePath = targetPath;
                        AddLog($"MySQL 下载完成: {targetPath}");
                        return;
                    }
                }

                AddLog("MySQL 下载失败：未命中可用地址");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task DownloadMqttAsync()
        {
            string downloadDir = EnsureServiceDownloadDirectory();
            if (string.IsNullOrWhiteSpace(downloadDir))
                return;

            SetBusy(true, "正在下载 MQTT...");
            try
            {
                foreach (string baseUrl in GetApiBaseCandidates(Config.UpdateServerUrl))
                {
                    string fileName = "mosquitto-2.0.18-install-windows-x64.exe";
                    string url = baseUrl.TrimEnd('/') + "/download/Tool/MQTT/" + fileName;
                    string targetPath = Path.Combine(downloadDir, fileName);
                    if (await DownloadFileToAsync(url, targetPath, swallowError: true))
                    {
                        MqttInstallerPath = targetPath;
                        AddLog($"MQTT 下载完成: {targetPath}");
                        return;
                    }
                }

                AddLog("MQTT 下载失败：未命中可用地址");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SelectServicePackage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "服务安装包 (*.zip)|*.zip",
                Title = "选择服务安装包"
            };

            if (dlg.ShowDialog() == true)
                ServicePackagePath = dlg.FileName;
        }

        private void SelectMySqlZip()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MySQL ZIP (*.zip)|*.zip",
                Title = "选择 MySQL 安装包"
            };

            if (dlg.ShowDialog() == true)
                MySqlPackagePath = dlg.FileName;
        }

        private void SelectMqttInstaller()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MQTT 安装程序 (*.exe)|*.exe",
                Title = "选择 MQTT 安装程序"
            };

            if (dlg.ShowDialog() == true)
                MqttInstallerPath = dlg.FileName;
        }

        private void SelectDownloadLocation()
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择下载保存目录",
                ShowNewFolderButton = true
            };
            if (!string.IsNullOrWhiteSpace(DownloadLocation) && Directory.Exists(DownloadLocation))
                dlg.SelectedPath = DownloadLocation;

            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadLocation = dlg.SelectedPath;
                AddLog($"下载目录已设置为: {DownloadLocation}");
            }
        }

        private async Task ExecuteInstallAsync()
        {
            string basePath = Config.BaseLocation;
            if (string.IsNullOrEmpty(basePath))
            {
                MessageBox.Show("请先设置安装根目录", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool hasAnyComponentChecked = InstallServiceChecked || InstallMySqlChecked || InstallMqttChecked;
            if (!hasAnyComponentChecked)
            {
                MessageBox.Show("请先勾选要安装的组件（服务包、MySQL、MQTT）", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "正在执行安装...");
            await Task.Run(() =>
            {
                try
                {
                    int progress = 0;

                    // 1. 备份数据库
                    if (BackupBeforeInstall)
                    {
                        SetProgress(progress += 5, "备份数据库...");
                        DoBackupNow();
                    }

                    bool servicesStoppedForInstall = false;

                    // 2. 备份服务文件夹
                    if (BackupServiceBeforeInstall && InstallServiceChecked)
                    {
                        SetProgress(progress += 5, "备份服务文件夹...");
                        StopPackagedServices();
                        servicesStoppedForInstall = true;
                        DoBackupServiceArchiveOnly();

                    }

                    // 3. 安装 MySQL
                    if (InstallMySqlChecked && !string.IsNullOrWhiteSpace(MySqlPackagePath) && File.Exists(MySqlPackagePath))
                    {
                        SetProgress(progress += 15, "安装 MySQL...");
                        string mysqlTarget = Path.Combine(Directory.GetParent(basePath)?.FullName ?? basePath, "Mysql");
                        var helper = new MySqlServiceHelper();
                        helper.InstallFromZipAsync(MySqlPackagePath, mysqlTarget, AddLog).GetAwaiter().GetResult();
                    }

                    // 4. 安装 MQTT
                    if (InstallMqttChecked && !string.IsNullOrWhiteSpace(MqttInstallerPath) && File.Exists(MqttInstallerPath))
                    {
                        SetProgress(progress += 15, "安装 MQTT...");
                        InstallMqttFromExe(MqttInstallerPath);
                    }

                    // 5. 安装服务包
                    if (InstallServiceChecked && !string.IsNullOrWhiteSpace(ServicePackagePath) && File.Exists(ServicePackagePath))
                    {
                        SetProgress(progress += 20, "安装 CVWindowsService...");
                        StopPackagedServices();
                        ZipFile.ExtractToDirectory(ServicePackagePath, basePath, true);
                        AddLog("解压服务包完成");

                        string installRoot = ResolveServiceInstallRoot(basePath);
                        AddLog($"服务安装根目录: {installRoot}");
                        DeleteCommonDllAfterUpdate(installRoot);

                        SetProgress(progress += 5, "注册/更新服务...");
                        InstallOrUpdatePackagedServices(installRoot);
                    }

                    // 6. 执行数据库脚本
                    if (AutoUpdateDatabase)
                    {
                        SetProgress(progress += 15, "执行数据库脚本...");
                        string sqlDir = Path.Combine(basePath, "SQL");
                        if (Directory.Exists(sqlDir))
                        {
                            foreach (var sqlFile in Directory.GetFiles(sqlDir, "*.sql").OrderBy(f => f))
                            {
                                AddLog($"执行 SQL: {Path.GetFileName(sqlFile)}");
                            }
                        }
                    }

                    // 7. 同步配置
                    SetProgress(progress += 10, "同步配置...");
                    ServiceManagerViewModel.Instance.ApplyConfigAndRefreshAfterInstall();

                    // 8. 启动服务
                    if (AutoStartAfterInstall)
                    {
                        SetProgress(progress += 10, "启动服务...");
                        ServiceManagerViewModel.Instance.OneKeyStartCommand.Execute(null);
                    }
                    else if (servicesStoppedForInstall)
                    {
                        AddLog("安装前已停止服务，当前未自动启动（根据配置）");
                    }

                    CleanupPackDirectory(basePath);

                    SetProgress(100, "安装完成");
                    AddLog("安装完成！");
                }
                catch (Exception ex)
                {
                    AddLog($"安装失败: {ex.Message}");
                    log.Error("安装失败", ex);
                    MessageBox.Show($"安装失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });

            SetBusy(false);
        }

        private void CleanupPackDirectory(string basePath)
        {
            try
            {
                string packDir = Path.Combine(basePath, "pack");
                if (Directory.Exists(packDir))
                {
                    Directory.Delete(packDir, true);
                    AddLog($"安装后已删除pack目录: {packDir}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"清理pack目录失败: {ex.Message}");
            }
        }

        private async Task OneKeyInstallAllAsync()
        {
            // 选择安装包
            string? zipFile = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "安装包 (*.zip)|*.zip",
                    Title = "选择一键安装包 (FullPackage.zip)"
                };
                if (dlg.ShowDialog() == true)
                    zipFile = dlg.FileName;
            });

            if (string.IsNullOrWhiteSpace(zipFile) || !File.Exists(zipFile))
                return;

            SetBusy(true, "正在解析安装包...");
            await Task.Run(() =>
            {
                try
                {
                    string packDir = Path.Combine(Directory.GetParent(zipFile)!.FullName, Path.GetFileNameWithoutExtension(zipFile));
                    if (Directory.Exists(packDir))
                        Directory.Delete(packDir, true);

                    ZipFile.ExtractToDirectory(zipFile, packDir);
                    AddLog($"解压完成: {packDir}");

                    // 自动识别包内内容，配置路径并勾选
                    string mysqlZip = Path.Combine(packDir, "mysql-5.7.37-winx64.zip");
                    string mqttInstaller = Path.Combine(packDir, "mosquitto-2.0.18-install-windows-x64.exe");
                    string serviceZip = FindServicePackageZip(packDir);
                    bool hasSvc = !string.IsNullOrWhiteSpace(serviceZip) && File.Exists(serviceZip);

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (hasSvc)
                        {
                            ServicePackagePath = serviceZip;
                            InstallServiceChecked = true;
                            AddLog($"已应用服务包路径: {ServicePackagePath}");
                        }
                        if (File.Exists(mysqlZip))
                        {
                            MySqlPackagePath = mysqlZip;
                            InstallMySqlChecked = true;
                            AddLog($"已应用 MySQL 包路径: {MySqlPackagePath}");
                        }
                        if (File.Exists(mqttInstaller))
                        {
                            MqttInstallerPath = mqttInstaller;
                            InstallMqttChecked = true;
                            AddLog($"已应用 MQTT 包路径: {MqttInstallerPath}");
                        }
                    });

                    AddLog($"检测到组件: 服务={hasSvc}, MySQL={File.Exists(mysqlZip)}, MQTT={File.Exists(mqttInstaller)}");
                    AddLog("一键安装包解析完成，pack目录将在安装成功后清理");
                }
                catch (Exception ex)
                {
                    AddLog($"解析一键安装包失败: {ex.Message}");
                }
            });

            SetBusy(false);
        }

        private static string FindServicePackageZip(string packDir)
        {
            if (!Directory.Exists(packDir))
                return string.Empty;

            string exact = Path.Combine(packDir, "CVWindowsService.zip");
            if (File.Exists(exact))
                return exact;

            string[] serviceZipCandidates = Directory.GetFiles(packDir, "*.zip", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    string name = Path.GetFileName(f);
                    return !name.Equals("mysql-5.7.37-winx64.zip", StringComparison.OrdinalIgnoreCase)
                        && name.Contains("CVWindowsService", StringComparison.OrdinalIgnoreCase);
                })
                .ToArray();

            if (serviceZipCandidates.Length > 0)
                return serviceZipCandidates[0];

            return string.Empty;
        }

        private void DoBackupNow()
        {
            try
            {
                var helper = new MySqlServiceHelper();
                helper.DetectFromRegistry();
                if (!helper.IsRunning)
                {
                    AddLog("MySQL 未运行，跳过备份");
                    return;
                }

                var mySqlConfig = ColorVision.Database.MySqlSetting.Instance.MySqlConfig;
                string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss");
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup");
                Directory.CreateDirectory(backupDir);
                string bakFile = Path.Combine(backupDir, $"color_vision_{timestamp}.sql");

                helper.BackupDatabase(mySqlConfig.UserName, mySqlConfig.UserPwd, mySqlConfig.Database, bakFile, AddLog);
                AddLog($"数据库备份完成: {bakFile}");
            }
            catch (Exception ex)
            {
                AddLog($"备份失败: {ex.Message}");
            }
        }

        private void DoRestoreBackup()
        {
            string? filePath = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "SQL 备份文件 (*.sql)|*.sql",
                    Title = "选择备份文件",
                    InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup")
                };
                if (dlg.ShowDialog() == true)
                    filePath = dlg.FileName;
            });

            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                var helper = new MySqlServiceHelper();
                helper.DetectFromRegistry();
                if (!helper.IsRunning)
                {
                    AddLog("MySQL 未运行，无法恢复");
                    return;
                }

                var mySqlConfig = ColorVision.Database.MySqlSetting.Instance.MySqlConfig;
                helper.RestoreDatabase(mySqlConfig.UserName, mySqlConfig.UserPwd, mySqlConfig.Database, filePath, AddLog);
                AddLog($"数据库恢复完成: {filePath}");
            }
            catch (Exception ex)
            {
                AddLog($"恢复失败: {ex.Message}");
            }
        }

        private void DoBackupServiceNow()
        {
            bool stopped = false;
            try
            {
                StopPackagedServices();
                stopped = true;
                DoBackupServiceArchiveOnly();
            }
            finally
            {
                if (stopped)
                {
                    StartPackagedServices();
                }
            }
        }

        private void DoBackupServiceArchiveOnly()
        {
            try
            {
                string basePath = Config.BaseLocation;
                if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                {
                    AddLog("未设置安装根目录或目录不存在，跳过服务备份");
                    return;
                }

                string sourcePath = ResolveBestServiceRootForBackup(basePath);
                if (!Directory.Exists(sourcePath))
                {
                    AddLog($"服务备份源目录不存在: {sourcePath}");
                    return;
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd'T'HHmmss");
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "ServiceBackup");
                Directory.CreateDirectory(backupDir);
                string versionTag = GetServiceVersionTag(sourcePath);
                string backupFile = BackupServiceCfgOnly
                    ? Path.Combine(backupDir, $"CVWindowsService_cfg_{versionTag}_{timestamp}.zip")
                    : Path.Combine(backupDir, $"CVWindowsService_{versionTag}_{timestamp}.rar");

                AddLog($"备份服务文件夹: {sourcePath} → {backupFile}");
                if (File.Exists(backupFile)) File.Delete(backupFile);

                if (BackupServiceCfgOnly)
                {
                    int cfgCount = CreateCfgOnlyBackupZip(sourcePath, backupFile);
                    AddLog($"CFG 备份完成，文件数: {cfgCount}");
                }
                else
                {
                    bool winRarOk = TryCreateServiceBackupWithWinRar(sourcePath, backupFile);
                    if (!winRarOk)
                    {
                        backupFile = Path.ChangeExtension(backupFile, ".zip");
                        if (File.Exists(backupFile)) File.Delete(backupFile);
                        int packedCount = CreateServiceBackupZip(sourcePath, backupFile);
                        AddLog($"WinRAR 不可用或执行失败，已回退 ZIP 全量压缩，文件数: {packedCount}");
                    }
                }

                AddLog($"服务备份完成: {backupFile}");
            }
            catch (Exception ex)
            {
                AddLog($"服务备份失败: {ex.Message}");
            }
        }

        private void DoRestoreServiceBackup()
        {
            string? filePath = null;
            Application.Current?.Dispatcher.Invoke(() =>
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "服务备份文件 (*.zip;*.rar)|*.zip;*.rar",
                    Title = "选择服务备份文件",
                    InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "ServiceBackup")
                };
                if (dlg.ShowDialog() == true)
                    filePath = dlg.FileName;
            });

            if (string.IsNullOrEmpty(filePath)) return;

            bool stopped = false;
            try
            {
                string basePath = Config.BaseLocation;
                if (string.IsNullOrEmpty(basePath))
                {
                    AddLog("未设置安装根目录，无法恢复");
                    return;
                }

                StopPackagedServices();
                stopped = true;

                AddLog($"恢复服务文件夹: {filePath} → {basePath}");

                // 清空目标并恢复
                if (Directory.Exists(basePath))
                    Directory.Delete(basePath, true);

                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".rar")
                {
                    if (!ExtractRarToDirectory(filePath, basePath))
                    {
                        AddLog("RAR 恢复失败：未检测到可用 WinRAR/RAR 命令行");
                        return;
                    }
                }
                else
                {
                    ZipFile.ExtractToDirectory(filePath, basePath, true);
                }
                AddLog($"服务恢复完成");
            }
            catch (Exception ex)
            {
                AddLog($"服务恢复失败: {ex.Message}");
            }
            finally
            {
                if (stopped)
                {
                    StartPackagedServices();
                }
            }
        }

        private void InstallMqttFromExe(string exeFile)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exeFile,
                Verb = "runas",
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);
            process?.WaitForExit();

            Tool.ExecuteCommandAsAdmin("net start mosquitto");
            AddLog("MQTT 服务已启动");
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LogText += $"[{timestamp}] {message}\n";
            });
        }

        private void SetBusy(bool busy, string text = "")
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsBusy = busy;
                ProgressText = text;
                if (!busy)
                {
                    Progress = 0;
                }
            });
        }

        private void SetProgress(double value, string text)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                Progress = value;
                ProgressText = text;
            });
            AddLog(text);
        }

        private string EnsureServiceDownloadDirectory()
        {
            string downloadPath = Config.DownloadLocation;
            if (string.IsNullOrWhiteSpace(downloadPath))
            {
                downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Downloads");
                Config.DownloadLocation = downloadPath;
            }

            Directory.CreateDirectory(downloadPath);
            return downloadPath;
        }


        private async Task<ServicePackageInfo?> FindLatestServicePackageAsync()
        {
            using HttpClient httpClient = new();
            foreach (var apiBaseUrl in GetApiBaseCandidates(Config.UpdateServerUrl))
            {
                try
                {
                    string releasesUrl = apiBaseUrl.TrimEnd('/') + "/api/tool/cvwindowsservice/releases";
                    using var response = await httpClient.GetAsync(releasesUrl);
                    if (!response.IsSuccessStatusCode)
                        continue;

                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    string latestVersion = root.TryGetProperty("latestVersion", out var lv) ? lv.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(latestVersion))
                        continue;

                    if (!Version.TryParse(latestVersion, out var version))
                        continue;

                    string fileName = $"FullPackage[{latestVersion}].zip";
                    if (root.TryGetProperty("packages", out var packagesArray))
                    {
                        foreach (var pkg in packagesArray.EnumerateArray())
                        {
                            string pkgVersion = pkg.TryGetProperty("version", out var pv) ? pv.GetString() ?? string.Empty : string.Empty;
                            if (pkgVersion == latestVersion)
                            {
                                fileName = pkg.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? fileName : fileName;
                                break;
                            }
                        }
                    }

                    string downloadUrl = apiBaseUrl.TrimEnd('/') + "/api/tool/cvwindowsservice/download/" + latestVersion;
                    return new ServicePackageInfo(version, fileName, downloadUrl);
                }
                catch
                {
                }
            }

            return null;
        }

        private async Task<bool> DownloadFileToAsync(string requestUrl, string targetFilePath, bool swallowError = false)
        {
            try
            {
                if (File.Exists(targetFilePath))
                {
                    AddLog($"文件已存在，跳过下载: {targetFilePath}");
                    return true;
                }

                using HttpClient client = new();
                using var response = await client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    if (!swallowError)
                        AddLog($"下载失败: {response.StatusCode} {requestUrl}");
                    return false;
                }

                string? parent = Path.GetDirectoryName(targetFilePath);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                await using var source = await response.Content.ReadAsStreamAsync();
                await using var target = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await source.CopyToAsync(target);
                return true;
            }
            catch (Exception ex)
            {
                if (!swallowError)
                    AddLog($"下载异常: {ex.Message}");
                return false;
            }
        }

        private static IEnumerable<string> GetApiBaseCandidates(string configuredUrl)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(configuredUrl))
            {
                try
                {
                    var uri = new Uri(configuredUrl.TrimEnd('/'));
                    candidates.Add(uri.GetLeftPart(UriPartial.Authority));
                    if (uri.Port == 9999)
                    {
                        candidates.Add($"{uri.Scheme}://{uri.Host}:9998");
                    }
                }
                catch
                {
                }
            }

            candidates.Add("http://xc213618.ddns.me:9998");
            return candidates.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private string ResolveBestServiceRootForBackup(string configuredBasePath)
        {
            var candidates = new List<string> { configuredBasePath };
            string nested = Path.Combine(configuredBasePath, "CVWindowsService");
            if (Directory.Exists(nested))
                candidates.Add(nested);

            string best = configuredBasePath;
            int bestScore = -1;

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(candidate))
                    continue;

                int score = 0;
                string reg = Path.Combine(candidate, "RegWindowsService");
                string mainX64 = Path.Combine(candidate, "CVMainWindowsService_x64");
                string commonDll = Path.Combine(candidate, "CommonDll");
                if (Directory.Exists(reg)) score += 200;
                if (Directory.Exists(mainX64)) score += 200;
                if (Directory.Exists(commonDll)) score += 200;

                try
                {
                    int fileCount = Directory.EnumerateFiles(candidate, "*", SearchOption.AllDirectories)
                        .Count(path => !IsLogPath(path, candidate));
                    score += fileCount;
                }
                catch
                {
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            AddLog($"服务备份源目录选择: {best}");
            return best;
        }

        private int CreateServiceBackupZip(string sourceRoot, string zipPath)
        {
            int packed = 0;
            using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

            foreach (var filePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                if (IsLogPath(filePath, sourceRoot))
                    continue;

                try
                {
                    string entryName = Path.GetRelativePath(sourceRoot, filePath).Replace('\\', '/');
                    archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
                    packed++;
                }
                catch (Exception ex)
                {
                    AddLog($"跳过文件（打包失败）: {filePath}，原因: {ex.Message}");
                }
            }

            return packed;
        }

        private int CreateCfgOnlyBackupZip(string sourceRoot, string zipPath)
        {
            int packed = 0;
            using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

            string[] cfgRoots =
            {
                Path.Combine(sourceRoot, "CVMainWindowsService_dev", "cfg"),
                Path.Combine(sourceRoot, "CVMainWindowsService_x64", "cfg"),
                Path.Combine(sourceRoot, "RegWindowsService", "cfg")
            };

            foreach (string cfgRoot in cfgRoots)
            {
                if (!Directory.Exists(cfgRoot))
                    continue;

                foreach (var filePath in Directory.EnumerateFiles(cfgRoot, "*", SearchOption.AllDirectories))
                {
                    string entryName = Path.GetRelativePath(sourceRoot, filePath).Replace('\\', '/');
                    archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
                    packed++;
                }
            }

            return packed;
        }

        private bool TryCreateServiceBackupWithWinRar(string sourceRoot, string backupFile)
        {
            string? winRarExe = FindWinRarExecutable();
            if (string.IsNullOrWhiteSpace(winRarExe))
                return false;

            string args = $"a -ma5 -r -ep1 -oi1 -x*\\log\\* \"{backupFile}\" \"{sourceRoot}\\*\"";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = winRarExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = sourceRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var p = Process.Start(psi);
                if (p == null)
                    return false;

                p.WaitForExit();
                string stdErr = p.StandardError.ReadToEnd();
                string stdOut = p.StandardOutput.ReadToEnd();
                if (p.ExitCode <= 1 && File.Exists(backupFile))
                {
                    AddLog("WinRAR 压缩成功（RAR 格式，相同文件按参考保存，已忽略 log）");
                    return true;
                }

                AddLog($"WinRAR 压缩失败，ExitCode={p.ExitCode}");
                if (!string.IsNullOrWhiteSpace(stdErr))
                    AddLog($"WinRAR stderr: {stdErr}");
                if (!string.IsNullOrWhiteSpace(stdOut))
                    AddLog($"WinRAR stdout: {stdOut}");
                return false;
            }
            catch (Exception ex)
            {
                AddLog($"调用 WinRAR 异常: {ex.Message}");
                return false;
            }
        }

        private string? FindWinRarExecutable()
        {
            string[] fixedCandidates =
            {
                @"C:\Program Files\WinRAR\WinRAR.exe",
                @"C:\Program Files (x86)\WinRAR\WinRAR.exe",
                @"C:\Program Files\WinRAR\Rar.exe",
                @"C:\Program Files (x86)\WinRAR\Rar.exe"
            };

            foreach (var candidate in fixedCandidates)
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string winRar = Path.Combine(dir.Trim(), "WinRAR.exe");
                    if (File.Exists(winRar))
                        return winRar;

                    string rar = Path.Combine(dir.Trim(), "Rar.exe");
                    if (File.Exists(rar))
                        return rar;
                }
                catch
                {
                }
            }

            return null;
        }

        private bool ExtractRarToDirectory(string rarFile, string targetDir)
        {
            string? winRarExe = FindWinRarExecutable();
            if (string.IsNullOrWhiteSpace(winRarExe))
                return false;

            Directory.CreateDirectory(targetDir);
            string args = $"x -y \"{rarFile}\" \"{targetDir}\\\"";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = winRarExe,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var p = Process.Start(psi);
                if (p == null)
                    return false;

                p.WaitForExit();
                return p.ExitCode <= 1;
            }
            catch (Exception ex)
            {
                AddLog($"RAR 解压异常: {ex.Message}");
                return false;
            }
        }

        private void StopPackagedServices()
        {

            var entries = ServiceManagerConfig.GetDefaultServiceEntries();
            foreach (var svc in entries)
            {
                try
                {
                    if (WinServiceHelper.IsServiceExisted(svc.ServiceName) && WinServiceHelper.IsServiceRunning(svc.ServiceName))
                    {
                        AddLog($"停止服务: {svc.ServiceName}");
                        WinServiceHelper.StopService(svc.ServiceName, 30);
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"停止服务失败: {svc.ServiceName}, {ex.Message}");
                }
                Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
            }
        }

        private void StartPackagedServices()
        {
            var entries = ServiceManagerConfig.GetDefaultServiceEntries();
            foreach (var svc in entries)
            {
                try
                {
                    if (WinServiceHelper.IsServiceExisted(svc.ServiceName))
                    {
                        AddLog($"启动服务: {svc.ServiceName}");
                        WinServiceHelper.StartService(svc.ServiceName, 30);
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"启动服务失败: {svc.ServiceName}, {ex.Message}");
                }
                Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
            }

        }

        private void InstallOrUpdatePackagedServices(string basePath)
        {
            var entries = ServiceManagerConfig.GetDefaultServiceEntries();
            foreach (var svc in entries)
            {
                if (!svc.IsPackaged)
                    continue;

                string exePath = Path.Combine(basePath, svc.FolderName, svc.GetExecutableName());
                if (!File.Exists(exePath))
                {
                    AddLog($"跳过服务（未找到可执行文件）: {svc.ServiceName}");
                    continue;
                }

                if (WinServiceHelper.IsServiceExisted(svc.ServiceName))
                {
                    try
                    {
                        WinServiceHelper.StopService(svc.ServiceName, 20);
                        Application.Current.Dispatcher.Invoke(() =>  ServiceManagerViewModel.Instance.RefreshAll());
                        
                    }
                    catch
                    {
                    }
                    WinServiceHelper.UninstallService(svc.ServiceName);
                    Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
                }

                bool ok = WinServiceHelper.InstallService(svc.ServiceName, exePath);
                Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
                AddLog(ok
                    ? $"服务安装成功: {svc.ServiceName}"
                    : $"服务安装失败: {svc.ServiceName}");
            }
        }

        private string GetServiceVersionTag(string sourceRoot)
        {
            string[] candidates =
            {
                Path.Combine(sourceRoot, "CVMainWindowsService_x64", "CVMainWindowsService_x64.exe"),
                Path.Combine(sourceRoot, "CVMainWindowsService_dev", "CVMainWindowsService_dev.exe"),
                Path.Combine(sourceRoot, "RegWindowsService", "RegWindowsService.exe")
            };

            foreach (string exe in candidates)
            {
                if (!File.Exists(exe))
                    continue;

                var ver = WinServiceHelper.GetFileVersion(exe);
                if (ver != null)
                {
                    string version = ver.ToString();
                    if (!string.IsNullOrWhiteSpace(version))
                        return version.Replace('.', '_');
                }
            }

            return "unknown";
        }

        private string ResolveServiceInstallRoot(string basePath)
        {
            string nested = Path.Combine(basePath, "CVWindowsService");
            string[] candidates =
            {
                basePath,
                nested
            };

            string[] markers =
            {
                "RegWindowsService",
                "CVMainWindowsService_x64",
                "CVMainWindowsService_dev"
            };

            foreach (string candidate in candidates)
            {
                if (!Directory.Exists(candidate))
                    continue;

                if (markers.Any(m => Directory.Exists(Path.Combine(candidate, m))))
                    return candidate;
            }

            return basePath;
        }

        private static bool IsLogPath(string fullPath, string rootPath)
        {
            string relative = Path.GetRelativePath(rootPath, fullPath).Replace('\\', '/');
            return relative.StartsWith("log/", StringComparison.OrdinalIgnoreCase)
                || relative.Contains("/log/", StringComparison.OrdinalIgnoreCase)
                || relative.Equals("log", StringComparison.OrdinalIgnoreCase);
        }

        private void DeleteCommonDllAfterUpdate(string basePath)
        {
            string commonDllDir = Path.Combine(basePath, "CommonDll");
            try
            {
                if (Directory.Exists(commonDllDir))
                {
                    string[] targets =
                    {
                        Path.Combine(basePath, "RegWindowsService"),
                        Path.Combine(basePath, "CVMainWindowsService_x64"),
                        Path.Combine(basePath, "CVMainWindowsService_dev")
                    };

                    foreach (string target in targets)
                    {
                        if (!Directory.Exists(target))
                        {
                            AddLog($"CommonDll 复制目标目录不存在，跳过: {target}");
                            continue;
                        }

                        int copiedCount = CopyDirectoryRecursive(commonDllDir, target);
                        AddLog($"已复制 CommonDll 到: {target}，文件数: {copiedCount}");
                    }

                    Directory.Delete(commonDllDir, true);
                    AddLog("安装后已删除 CommonDll 目录");
                }
                else
                {
                    AddLog($"未找到 CommonDll 目录: {commonDllDir}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"删除 CommonDll 失败: {ex.Message}");
            }
        }

        private static int CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            int copiedCount = 0;

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(targetDir, relativePath);
                string? destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrWhiteSpace(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(file, destFile, true);
                copiedCount++;
            }

            return copiedCount;
        }
    }

    public class ServicePackageInfo
    {
        public Version Version { get; }
        public string FileName { get; }
        public string DownloadUrl { get; }

        public ServicePackageInfo(Version version, string fileName, string downloadUrl)
        {
            Version = version;
            FileName = fileName;
            DownloadUrl = downloadUrl;
        }
    }
}
