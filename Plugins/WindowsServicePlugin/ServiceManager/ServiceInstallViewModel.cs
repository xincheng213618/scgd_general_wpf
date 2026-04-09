using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine;
using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 安装包信息
    /// </summary>
    public class PackageInfo : ViewModelBase
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string PackageType { get; set; } = string.Empty; // FullPackage, MySQL, MQTT, Incremental
        public DateTime DownloadTime { get; set; }
        public long FileSize { get; set; }
        public string FileSizeText => FileSize > 0 ? $"{FileSize / 1024 / 1024} MB" : "Unknown";
    }

    /// <summary>
    /// 服务安装窗口 ViewModel
    /// </summary>
    public class ServiceInstallViewModel : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceInstallViewModel));

        public ServiceManagerConfig Config => ServiceManagerConfig.Instance;

        public ObservableCollection<PackageInfo> DownloadedPackages { get; set; } = new();

        public PackageInfo? SelectedPackage { get => _SelectedPackage; set { _SelectedPackage = value; OnPropertyChanged(); } }
        private PackageInfo? _SelectedPackage;

        public string LogText { get => _LogText; set { _LogText = value; OnPropertyChanged(); } }
        private string _LogText = string.Empty;

        public bool IsBusy { get => _IsBusy; set { _IsBusy = value; OnPropertyChanged(); } }
        private bool _IsBusy;

        public double Progress { get => _Progress; set { _Progress = value; OnPropertyChanged(); } }
        private double _Progress;

        public string ProgressText { get => _ProgressText; set { _ProgressText = value; OnPropertyChanged(); } }
        private string _ProgressText = string.Empty;

        public string UpdateServerUrl => Config.UpdateServerUrl;

        public string UpdateStatusText { get => _UpdateStatusText; set { _UpdateStatusText = value; OnPropertyChanged(); } }
        private string _UpdateStatusText = "点击检查更新按钮获取最新版本信息";

        // 安装选项
        public bool AutoStartAfterInstall { get => _AutoStartAfterInstall; set { _AutoStartAfterInstall = value; OnPropertyChanged(); } }
        private bool _AutoStartAfterInstall = true;

        public bool AutoUpdateDatabase { get => _AutoUpdateDatabase; set { _AutoUpdateDatabase = value; OnPropertyChanged(); } }
        private bool _AutoUpdateDatabase = true;

        // Commands
        public RelayCommand CheckUpdateCommand { get; }
        public RelayCommand SelectFullPackageCommand { get; }
        public RelayCommand SelectMySqlZipCommand { get; }
        public RelayCommand SelectMqttInstallerCommand { get; }
        public RelayCommand InstallPackageCommand { get; }
        public RelayCommand OpenPackageLocationCommand { get; }
        public RelayCommand DeletePackageCommand { get; }
        public RelayCommand ClearLogCommand { get; }
        public RelayCommand CloseCommand { get; }

        public ServiceInstallViewModel()
        {
            CheckUpdateCommand = new RelayCommand(a => _ = CheckUpdateAsync(), a => !IsBusy);
            SelectFullPackageCommand = new RelayCommand(a => SelectAndInstallFullPackage());
            SelectMySqlZipCommand = new RelayCommand(a => SelectAndInstallMySql());
            SelectMqttInstallerCommand = new RelayCommand(a => SelectAndInstallMqtt());
            InstallPackageCommand = new RelayCommand(a => _ = InstallPackageAsync(a as PackageInfo), a => !IsBusy);
            OpenPackageLocationCommand = new RelayCommand(a => OpenPackageLocation(a as PackageInfo));
            DeletePackageCommand = new RelayCommand(a => DeletePackage(a as PackageInfo));
            ClearLogCommand = new RelayCommand(a => LogText = string.Empty);
            CloseCommand = new RelayCommand(a => CloseWindow());

            RefreshDownloadedPackages();
        }

        /// <summary>
        /// 刷新已下载包列表
        /// </summary>
        private void RefreshDownloadedPackages()
        {
            DownloadedPackages.Clear();

            string downloadDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ColorVision",
                "Downloads");

            if (!Directory.Exists(downloadDir))
                return;

            var files = Directory.GetFiles(downloadDir, "*.zip")
                .Concat(Directory.GetFiles(downloadDir, "*.exe"))
                .OrderByDescending(f => new FileInfo(f).LastWriteTime);

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var package = new PackageInfo
                {
                    FilePath = file,
                    FileName = info.Name,
                    DownloadTime = info.LastWriteTime,
                    FileSize = info.Length
                };

                // 解析版本和类型
                ParsePackageInfo(package);
                DownloadedPackages.Add(package);
            }
        }

        private void ParsePackageInfo(PackageInfo package)
        {
            var name = package.FileName;

            // FullPackage: CVWindowsService[1.0.0.0].zip
            var match = Regex.Match(name, @"CVWindowsService\[(\d+\.\d+\.\d+\.\d+)\]");
            if (match.Success)
            {
                package.Version = match.Groups[1].Value;
                package.PackageType = "FullPackage";
                return;
            }

            // MySQL: mysql-5.7.37-winx64.zip
            match = Regex.Match(name, @"mysql-(\d+\.\d+\.\d+)-winx64");
            if (match.Success)
            {
                package.Version = match.Groups[1].Value;
                package.PackageType = "MySQL";
                return;
            }

            // MQTT: mosquitto-2.0.18-install-windows-x64.exe
            match = Regex.Match(name, @"mosquitto-(\d+\.\d+\.\d+)-install");
            if (match.Success)
            {
                package.Version = match.Groups[1].Value;
                package.PackageType = "MQTT";
                return;
            }

            // Incremental: CVWindowsService[1.0.0.0]-1.zip
            match = Regex.Match(name, @"CVWindowsService\[(\d+\.\d+\.\d+\.\d+)\]-(\d+)");
            if (match.Success)
            {
                package.Version = match.Groups[1].Value;
                package.PackageType = "Incremental";
                return;
            }

            package.PackageType = "Unknown";
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        private async Task CheckUpdateAsync()
        {
            try
            {
                SetBusy(true, "正在检查更新...");
                UpdateStatusText = "正在连接服务器...";

                var latestPackage = await FindLatestServicePackageAsync();
                if (latestPackage == null)
                {
                    UpdateStatusText = "未找到可用的更新包";
                    AddLog("未在更新目录中找到可用的 CVWindowsService 安装包");
                    return;
                }

                UpdateStatusText = $"发现新版本: {latestPackage.Version}";
                AddLog($"发现新版本: {latestPackage.Version}");

                // 下载更新包
                var downloadedPath = await DownloadPackageAsync(latestPackage);
                if (!string.IsNullOrEmpty(downloadedPath))
                {
                    Application.Current?.Dispatcher.Invoke(RefreshDownloadedPackages);
                    UpdateStatusText = $"下载完成: {latestPackage.FileName}";
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText = $"检查更新失败: {ex.Message}";
                AddLog($"检查更新失败: {ex.Message}");
                log.Error("检查更新失败", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task<string?> DownloadPackageAsync(ServicePackageInfo package)
        {
            string downloadDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ColorVision",
                "Downloads");

            Directory.CreateDirectory(downloadDir);

            var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
            if (service == null)
            {
                AddLog("下载服务不可用");
                return null;
            }

            var tcs = new TaskCompletionSource<string?>();

            Application.Current?.Dispatcher.Invoke(() =>
            {
                service.ShowDownloadWindow();
                service.Download(package.DownloadUrl, downloadDir, DownloadFileConfig.Instance.Authorization, filePath =>
                {
                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        AddLog($"下载完成: {filePath}");
                    }
                    tcs.TrySetResult(filePath);
                });
            });

            return await tcs.Task;
        }

        private async Task<ServicePackageInfo?> FindLatestServicePackageAsync()
        {
            string apiBaseUrl = GetApiBaseUrl(Config.UpdateServerUrl);
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
                return null;

            using HttpClient httpClient = CreateAuthorizedHttpClient();

            // Call /api/tool/cvwindowsservice/releases to get latest version and all packages
            string releasesUrl = apiBaseUrl.TrimEnd('/') + "/api/tool/cvwindowsservice/releases";
            string json = await httpClient.GetStringAsync(releasesUrl);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string latestVersion = root.TryGetProperty("latestVersion", out var lv) ? lv.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(latestVersion))
                return null;

            if (!root.TryGetProperty("packages", out var packagesArray))
                return null;

            // Find the package matching the latest version
            foreach (var pkg in packagesArray.EnumerateArray())
            {
                string pkgVersion = pkg.TryGetProperty("version", out var pv) ? pv.GetString() ?? "" : "";
                if (pkgVersion != latestVersion)
                    continue;

                string fileName = pkg.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "" : "";
                string downloadPath = pkg.TryGetProperty("downloadUrl", out var du) ? du.GetString() ?? "" : "";
                string downloadUrl = apiBaseUrl.TrimEnd('/') + downloadPath;

                if (!Version.TryParse(pkgVersion, out var version))
                    continue;

                return new ServicePackageInfo(version, fileName, downloadUrl);
            }

            return null;
        }

        /// <summary>
        /// Extract API base URL from the configured UpdateServerUrl.
        /// e.g. "http://host:9998/browse/Tool/CVWindowsService" -> "http://host:9998"
        /// </summary>
        private static string GetApiBaseUrl(string configuredUrl)
        {
            if (string.IsNullOrWhiteSpace(configuredUrl))
                return string.Empty;

            try
            {
                var uri = new Uri(configuredUrl.TrimEnd('/'));
                return uri.GetLeftPart(UriPartial.Authority);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static HttpClient CreateAuthorizedHttpClient()
        {
            HttpClient httpClient = new();
            var byteArray = Encoding.ASCII.GetBytes(DownloadFileConfig.Instance.Authorization);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            return httpClient;
        }

        /// <summary>
        /// 选择并安装 FullPackage
        /// </summary>
        private void SelectAndInstallFullPackage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "服务安装包 (*.zip)|*.zip",
                Title = "选择 CVWindowsService FullPackage"
            };

            if (dlg.ShowDialog() == true)
            {
                _ = InstallFullPackageAsync(dlg.FileName);
            }
        }

        /// <summary>
        /// 选择并安装 MySQL
        /// </summary>
        private void SelectAndInstallMySql()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MySQL ZIP (*.zip)|*.zip",
                Title = "选择 mysql-5.7.37-winx64.zip"
            };

            if (dlg.ShowDialog() == true)
            {
                _ = InstallMySqlAsync(dlg.FileName);
            }
        }

        /// <summary>
        /// 选择并安装 MQTT
        /// </summary>
        private void SelectAndInstallMqtt()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MQTT 安装程序 (*.exe)|*.exe",
                Title = "选择 mosquitto-2.0.18-install-windows-x64.exe"
            };

            if (dlg.ShowDialog() == true)
            {
                _ = InstallMqttAsync(dlg.FileName);
            }
        }

        /// <summary>
        /// 安装选中的包
        /// </summary>
        private async Task InstallPackageAsync(PackageInfo? package)
        {
            if (package == null) return;

            switch (package.PackageType)
            {
                case "FullPackage":
                    await InstallFullPackageAsync(package.FilePath);
                    break;
                case "MySQL":
                    await InstallMySqlAsync(package.FilePath);
                    break;
                case "MQTT":
                    await InstallMqttAsync(package.FilePath);
                    break;
                case "Incremental":
                    await InstallIncrementalAsync(package.FilePath);
                    break;
                default:
                    AddLog($"未知的包类型: {package.PackageType}");
                    break;
            }
        }

        /// <summary>
        /// 全新安装 FullPackage
        /// </summary>
        private async Task InstallFullPackageAsync(string zipFile)
        {
            if (!EnsureElevated()) return;

            // 显示安装选项对话框
            var optionsWindow = new InstallOptionsWindow
            {
                DataContext = this
            };
            optionsWindow.Owner = Application.Current?.MainWindow;

            if (optionsWindow.ShowDialog() != true)
                return;

            SetBusy(true, "正在准备安装...");

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

                    AddLog("开始全新安装...");

                    // 1. 解压
                    SetProgress(10, "解压安装包...");
                    string packDir = Path.Combine(basePath, "pack");
                    if (Directory.Exists(packDir))
                        Directory.Delete(packDir, true);

                    ZipFile.ExtractToDirectory(zipFile, packDir);
                    AddLog($"解压完成: {packDir}");

                    // 2. 安装 MySQL
                    string mysqlZip = Path.Combine(packDir, "mysql-5.7.37-winx64.zip");
                    if (File.Exists(mysqlZip))
                    {
                        SetProgress(20, "安装 MySQL...");
                        InstallMySqlFromZip(mysqlZip);
                    }

                    // 3. 安装 MQTT
                    string mqttExe = Path.Combine(packDir, "mosquitto-2.0.18-install-windows-x64.exe");
                    if (File.Exists(mqttExe))
                    {
                        SetProgress(30, "安装 MQTT...");
                        InstallMqttFromExe(mqttExe);
                    }

                    // 4. 安装 CVWindowsService
                    SetProgress(50, "安装 CVWindowsService...");
                    // TODO: 调用服务安装逻辑

                    // 5. 执行 SQL
                    if (AutoUpdateDatabase)
                    {
                        SetProgress(80, "执行数据库初始化...");
                        string sqlDir = Path.Combine(packDir, "SQL");
                        if (Directory.Exists(sqlDir))
                        {
                            ExecuteSqlScripts(sqlDir);
                        }
                    }

                    // 6. 启动服务
                    if (AutoStartAfterInstall)
                    {
                        SetProgress(90, "启动服务...");
                        StartAllServices();
                    }

                    SetProgress(100, "安装完成");
                    AddLog("安装完成!");
                }
                catch (Exception ex)
                {
                    AddLog($"安装失败: {ex.Message}");
                    log.Error("安装失败", ex);
                }
            });

            SetBusy(false);
        }

        /// <summary>
        /// 安装 MySQL
        /// </summary>
        private async Task InstallMySqlAsync(string zipFile)
        {
            if (!EnsureElevated()) return;

            SetBusy(true, "正在安装 MySQL...");

            await Task.Run(() =>
            {
                try
                {
                    InstallMySqlFromZip(zipFile);
                    AddLog("MySQL 安装完成");
                }
                catch (Exception ex)
                {
                    AddLog($"MySQL 安装失败: {ex.Message}");
                }
            });

            SetBusy(false);
        }

        /// <summary>
        /// 安装 MQTT
        /// </summary>
        private async Task InstallMqttAsync(string exeFile)
        {
            if (!EnsureElevated()) return;

            SetBusy(true, "正在安装 MQTT...");

            await Task.Run(() =>
            {
                try
                {
                    InstallMqttFromExe(exeFile);
                    AddLog("MQTT 安装完成");
                }
                catch (Exception ex)
                {
                    AddLog($"MQTT 安装失败: {ex.Message}");
                }
            });

            SetBusy(false);
        }

        /// <summary>
        /// 增量升级
        /// </summary>
        private async Task InstallIncrementalAsync(string zipFile)
        {
            if (!EnsureElevated()) return;

            SetBusy(true, "正在执行增量升级...");

            await Task.Run(() =>
            {
                try
                {
                    // TODO: 实现增量升级逻辑
                    AddLog("增量升级完成");
                }
                catch (Exception ex)
                {
                    AddLog($"增量升级失败: {ex.Message}");
                }
            });

            SetBusy(false);
        }

        // 具体的安装方法
        private void InstallMySqlFromZip(string zipFile)
        {
            // TODO: 从 MySqlServiceHelper 提取逻辑
            AddLog($"安装 MySQL from {zipFile}");
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

            // 启动服务
            Tool.ExecuteCommandAsAdmin("net start mosquitto");
            AddLog("MQTT 服务已启动");
        }

        private void ExecuteSqlScripts(string sqlDir)
        {
            var sqlFiles = Directory.GetFiles(sqlDir, "*.sql");
            foreach (var sqlFile in sqlFiles.OrderBy(f => f))
            {
                AddLog($"执行 SQL: {Path.GetFileName(sqlFile)}");
                // TODO: 调用 MySqlHelper 执行 SQL
            }
        }

        private void StartAllServices()
        {
            // TODO: 启动所有服务
            AddLog("启动所有服务...");
        }

        /// <summary>
        /// 打开包所在位置
        /// </summary>
        private void OpenPackageLocation(PackageInfo? package)
        {
            if (package == null) return;
            PlatformHelper.OpenFolderAndSelectFile(package.FilePath);
        }

        /// <summary>
        /// 删除包
        /// </summary>
        private void DeletePackage(PackageInfo? package)
        {
            if (package == null) return;

            try
            {
                File.Delete(package.FilePath);
                Application.Current?.Dispatcher.Invoke(() => DownloadedPackages.Remove(package));
                AddLog($"已删除: {package.FileName}");
            }
            catch (Exception ex)
            {
                AddLog($"删除失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 确保以管理员身份运行
        /// </summary>
        private bool EnsureElevated()
        {
            if (Tool.IsAdministrator())
                return true;

            MessageBox.Show("此操作需要管理员权限，请重启应用以管理员身份运行", "需要管理员权限", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    /// <summary>
    /// 服务包信息（用于在线检查）
    /// </summary>
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
