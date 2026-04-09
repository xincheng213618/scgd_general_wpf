using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using log4net;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
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
        private bool _autoUpdateDatabase = true;

        public RelayCommand CheckUpdateCommand { get; }
        public RelayCommand DownloadMySqlCommand { get; }
        public RelayCommand DownloadMqttCommand { get; }
        public RelayCommand SelectServicePackageCommand { get; }
        public RelayCommand SelectMySqlZipCommand { get; }
        public RelayCommand SelectMqttInstallerCommand { get; }
        public RelayCommand InstallServicePackageCommand { get; }
        public RelayCommand InstallMySqlCommand { get; }
        public RelayCommand InstallMqttCommand { get; }
        public RelayCommand ClearLogCommand { get; }
        public RelayCommand CloseCommand { get; }

        public ServiceInstallViewModel()
        {
            CheckUpdateCommand = new RelayCommand(a => _ = DownloadLatestServicePackageAsync(), a => !IsBusy);
            DownloadMySqlCommand = new RelayCommand(a => _ = DownloadMySqlAsync(), a => !IsBusy);
            DownloadMqttCommand = new RelayCommand(a => _ = DownloadMqttAsync(), a => !IsBusy);
            SelectServicePackageCommand = new RelayCommand(a => SelectServicePackage());
            SelectMySqlZipCommand = new RelayCommand(a => SelectMySqlZip());
            SelectMqttInstallerCommand = new RelayCommand(a => SelectMqttInstaller());
            InstallServicePackageCommand = new RelayCommand(a => _ = InstallServicePackageAsync(), a => !IsBusy);
            InstallMySqlCommand = new RelayCommand(a => _ = InstallMySqlAsync(), a => !IsBusy);
            InstallMqttCommand = new RelayCommand(a => _ = InstallMqttAsync(), a => !IsBusy);
            ClearLogCommand = new RelayCommand(a => LogText = string.Empty);
            CloseCommand = new RelayCommand(a => CloseWindow());
        }

        private async Task DownloadLatestServicePackageAsync()
        {
            if (!EnsureElevated("更新"))
                return;

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
            if (!EnsureElevated("下载 MySQL"))
                return;

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
            if (!EnsureElevated("下载 MQTT"))
                return;

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

        private async Task InstallServicePackageAsync()
        {
            if (!EnsureElevated("安装服务包"))
                return;

            if (string.IsNullOrWhiteSpace(ServicePackagePath) || !File.Exists(ServicePackagePath))
            {
                MessageBox.Show("请先选择有效的服务安装包路径", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "正在安装服务包...");
            await Task.Run(() =>
            {
                try
                {
                    string basePath = Config.BaseLocation;
                    if (string.IsNullOrEmpty(basePath))
                    {
                        MessageBox.Show("请先设置安装根目录", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    string packDir = Path.Combine(basePath, "pack");
                    if (Directory.Exists(packDir))
                        Directory.Delete(packDir, true);

                    SetProgress(10, "解压服务包...");
                    ZipFile.ExtractToDirectory(ServicePackagePath, packDir);
                    AddLog($"解压完成: {packDir}");

                    // 普通更新场景：仅更新服务本体
                    SetProgress(45, "安装 CVWindowsService...");
                    InstallCvWindowsServices(packDir, basePath);

                    if (AutoUpdateDatabase)
                    {
                        string sqlDir = Path.Combine(packDir, "SQL");
                        if (Directory.Exists(sqlDir))
                        {
                            SetProgress(75, "执行数据库脚本...");
                            ExecuteSqlScripts(sqlDir);
                        }
                    }

                    // 安装/复制完成后必须同步配置，避免使用安装包默认配置。
                    SetProgress(85, "同步配置...");
                    ServiceManagerViewModel.Instance.ApplyConfigAndRefreshAfterInstall();

                    if (AutoStartAfterInstall)
                    {
                        SetProgress(92, "启动服务...");
                        StartAllServices();
                    }

                    SetProgress(100, "服务安装完成");
                    AddLog("服务安装完成");
                }
                catch (Exception ex)
                {
                    AddLog($"服务安装失败: {ex.Message}");
                    log.Error("服务安装失败", ex);
                }
            });

            SetBusy(false);
        }

        private async Task InstallMySqlAsync()
        {
            if (!EnsureElevated("安装 MySQL"))
                return;

            if (string.IsNullOrWhiteSpace(MySqlPackagePath) || !File.Exists(MySqlPackagePath))
            {
                MessageBox.Show("请先选择有效的 MySQL ZIP 路径", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "正在安装 MySQL...");
            await Task.Run(() =>
            {
                try
                {
                    InstallMySqlFromZip(MySqlPackagePath);
                    ServiceManagerViewModel.Instance.ApplyConfigAndRefreshAfterInstall();
                    AddLog("MySQL 安装完成");
                }
                catch (Exception ex)
                {
                    AddLog($"MySQL 安装失败: {ex.Message}");
                }
            });
            SetBusy(false);
        }

        private async Task InstallMqttAsync()
        {
            if (!EnsureElevated("安装 MQTT"))
                return;

            if (string.IsNullOrWhiteSpace(MqttInstallerPath) || !File.Exists(MqttInstallerPath))
            {
                MessageBox.Show("请先选择有效的 MQTT 安装程序路径", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "正在安装 MQTT...");
            await Task.Run(() =>
            {
                try
                {
                    InstallMqttFromExe(MqttInstallerPath);
                    ServiceManagerViewModel.Instance.ApplyConfigAndRefreshAfterInstall();
                    AddLog("MQTT 安装完成");
                }
                catch (Exception ex)
                {
                    AddLog($"MQTT 安装失败: {ex.Message}");
                }
            });
            SetBusy(false);
        }

        private void InstallMySqlFromZip(string zipFile)
        {
            AddLog($"开始安装 MySQL: {zipFile}");

            string basePath = Config.BaseLocation;
            if (string.IsNullOrEmpty(basePath))
            {
                AddLog("错误：未设置安装根目录");
                return;
            }

            string targetPath = Path.Combine(Directory.GetParent(basePath)?.FullName ?? basePath, "Mysql");
            Directory.CreateDirectory(targetPath);

            var helper = new MySqlServiceHelper();
            bool ok = helper.InstallFromZipAsync(zipFile, targetPath, AddLog).GetAwaiter().GetResult();
            AddLog(ok ? "MySQL 安装成功" : "MySQL 安装失败");
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

        private void InstallCvWindowsServices(string extractedPackDir, string basePath)
        {
            var serviceEntries = ServiceManagerConfig.GetDefaultServiceEntries().Where(s => s.IsPackaged).ToList();

            foreach (var svc in serviceEntries)
            {
                if (!WinServiceHelper.IsServiceExisted(svc.ServiceName))
                    continue;

                try
                {
                    Tool.ExecuteCommandAsAdmin($"net stop {svc.ServiceName}");
                }
                catch
                {
                }

                bool removed = WinServiceHelper.UninstallService(svc.ServiceName);
                AddLog(removed ? $"卸载服务成功: {svc.ServiceName}" : $"卸载服务失败: {svc.ServiceName}");
                RefreshServiceStatus();
            }

            CopyDirectory(extractedPackDir, basePath);
            CopyCommonDllToAllServices(basePath, serviceEntries);

            foreach (var svc in serviceEntries)
            {
                string exePath = Path.Combine(basePath, svc.FolderName, svc.GetExecutableName());
                if (!File.Exists(exePath))
                {
                    AddLog($"未找到服务可执行文件，跳过: {exePath}");
                    continue;
                }

                bool ok = WinServiceHelper.InstallService(svc.ServiceName, exePath);
                AddLog(ok ? $"安装服务成功: {svc.ServiceName}" : $"安装服务失败: {svc.ServiceName}");
                RefreshServiceStatus();
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(targetDir))
                Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }

            foreach (var directory in Directory.GetDirectories(sourceDir))
            {
                string name = Path.GetFileName(directory);
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                CopyDirectory(directory, Path.Combine(targetDir, name));
            }
        }

        private static void CopyCommonDllToAllServices(string basePath, IEnumerable<ServiceEntry> serviceEntries)
        {
            string commonDir = Path.Combine(basePath, "CommonDll");
            if (!Directory.Exists(commonDir))
                return;

            foreach (var svc in serviceEntries)
            {
                string svcDir = Path.Combine(basePath, svc.FolderName);
                if (Directory.Exists(svcDir))
                {
                    CopyDirectory(commonDir, svcDir);
                }
            }
        }

        private void ExecuteSqlScripts(string sqlDir)
        {
            foreach (var sqlFile in Directory.GetFiles(sqlDir, "*.sql").OrderBy(f => f))
            {
                AddLog($"执行 SQL: {Path.GetFileName(sqlFile)}");
            }
        }

        private void StartAllServices()
        {
            ServiceManagerViewModel.Instance.OneKeyStartCommand.Execute(null);
            AddLog("启动所有服务...");
        }

        private void RefreshServiceStatus()
        {
            Application.Current?.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
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

                    string fileName = $"CVWindowsService[{latestVersion}].zip";
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

        private string EnsureServiceDownloadDirectory()
        {
            string basePath = Config.BaseLocation;
            if (string.IsNullOrWhiteSpace(basePath))
            {
                MessageBox.Show("请先在服务管理器中设置安装根目录", "下载", MessageBoxButton.OK, MessageBoxImage.Warning);
                return string.Empty;
            }

            Directory.CreateDirectory(basePath);
            return basePath;
        }

        private bool EnsureElevated(string actionName)
        {
            if (Tool.IsAdministrator())
                return true;

            MessageBox.Show($"{actionName}需要管理员权限，请使用管理员身份启动程序后重试。", "需要管理员权限", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
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

        public void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Application.Current?.Dispatcher.Invoke(() =>
            {
                LogText += $"[{timestamp}] {message}\n";
            });
        }

        public Action? CloseAction { get; set; }

        private void CloseWindow()
        {
            CloseAction?.Invoke();
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
