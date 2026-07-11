using ColorVision.UI;
using ColorVision.UI.Marketplace;
using Newtonsoft.Json.Linq;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Diagnostics;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 安装编排：组件安装、一键安装、服务生命周期管理
    /// </summary>
    public partial class ServiceInstallViewModel
    {
        private const string ArchiveServiceName = "CVArchService";
        private const string ArchiveServiceDisplayName = "CVArchService";
        private const string ArchiveServiceExecutableName = "ArchivedWindowsService.exe";

        private async Task ExecuteInstallAsync()
        {
            string basePath = Config.BaseLocation;
            bool hasAnyComponentChecked = InstallServiceChecked || InstallMySqlChecked || InstallMqttChecked || InstallVc2013Checked;
            bool needsBasePath = InstallServiceChecked || InstallMySqlChecked || InstallMqttChecked || BackupBeforeInstall || BackupServiceBeforeInstall || AutoUpdateDatabase;
            if (needsBasePath && string.IsNullOrEmpty(basePath))
            {
                MessageBox.Show("请先设置安装根目录", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!hasAnyComponentChecked)
            {
                MessageBox.Show("请先勾选要安装的组件", "安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetBusy(true, "正在执行安装...");
            try
            {
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
                        bool mysqlInstalledThisRun = false;
                        var serviceManager = ServiceManagerViewModel.Instance;
                        bool hasServiceWork = InstallServiceChecked || InstallMySqlChecked || InstallMqttChecked;

                        // 2. 备份服务文件夹
                        if (BackupServiceBeforeInstall && InstallServiceChecked)
                        {
                            SetProgress(progress += 5, "备份服务文件夹...");
                            StopPackagedServices();
                            servicesStoppedForInstall = true;
                            DoBackupServiceArchiveOnly();

                        }

                        bool needsVc2013Runtime = !IsVc2013RuntimeInstalled() && (InstallVc2013Checked || InstallMySqlChecked);
                        if (needsVc2013Runtime)
                        {
                            SetProgress(progress += 10, "安装 VC++ 2013 运行库...");
                            if (!InstallVc2013Runtime())
                                throw new InvalidOperationException("VC++ 2013 运行库安装失败");
                        }

                        // 3. 安装 MySQL
                        if (InstallMySqlChecked)
                        {
                            if (string.IsNullOrWhiteSpace(MySqlPackagePath) || !File.Exists(MySqlPackagePath))
                                throw new InvalidOperationException("已勾选 MySQL，但未选择有效的 MySQL ZIP 安装包");

                            SetProgress(progress += 15, "安装 MySQL...");
                            serviceManager.MySqlManager.RefreshStatus(serviceManager.Services, serviceManager.Config.MySqlPort);
                            bool mysqlWasInstalled = serviceManager.MySqlManager.Config.IsInstalled || serviceManager.MySqlManager.Helper.IsInstalled;
                            bool mysqlInstalled = serviceManager.MySqlManager.InstallFromZipViaServiceHostAsync(MySqlPackagePath, basePath, log.Info).GetAwaiter().GetResult();
                            if (!mysqlInstalled)
                            {
                                throw new InvalidOperationException("MySQL 安装失败");
                            }
                            mysqlInstalledThisRun = !mysqlWasInstalled;
                        }

                        // 4. 安装 MQTT
                        if (InstallMqttChecked)
                        {
                            if (string.IsNullOrWhiteSpace(MqttInstallerPath) || !File.Exists(MqttInstallerPath))
                                throw new InvalidOperationException("已勾选 MQTT，但未选择有效的 MQTT 安装程序");

                            SetProgress(progress += 15, "安装 MQTT...");
                            InstallMqttFromExe(MqttInstallerPath);
                        }

                        // 5. 安装/更新服务包
                        if (InstallServiceChecked)
                        {
                            if (string.IsNullOrWhiteSpace(ServicePackagePath) || !File.Exists(ServicePackagePath))
                                throw new InvalidOperationException("已勾选服务包，但未选择有效的 CVWindowsService 完整安装包");

                            if (!IsFullServicePackageZip(ServicePackagePath))
                            {
                                throw new InvalidOperationException("当前安装流程只支持完整服务包，请选择 CVWindowsService 完整安装包");
                            }
                            // 完整安装包：全量解压 + 重新注册服务。
                            SetProgress(progress += 20, "安装 CVWindowsService...");
                            StopLegacyManagementToolProcesses();
                            if (!servicesStoppedForInstall)
                            {
                                StopPackagedServices();
                                servicesStoppedForInstall = true;
                            }
                            ArchiveServiceInstallState? archiveServiceState = null;
                            try
                            {
                                archiveServiceState = UninstallArchiveServiceBeforePackageUpdate();
                                if (archiveServiceState != null && !WaitForServiceRemoval(ArchiveServiceName, TimeSpan.FromSeconds(15)))
                                {
                                    throw new InvalidOperationException($"{ArchiveServiceName} 已卸载但仍被系统占用，请关闭服务属性窗口后重试");
                                }

                                CleanExistingServicePackageTargets(ServicePackagePath, basePath);
                                ZipFile.ExtractToDirectory(ServicePackagePath, basePath, true);
                                log.Info("解压服务包完成");

                                string installRoot = ResolveServiceInstallRoot(basePath);
                                log.Info($"服务安装根目录: {installRoot}");
                                DeleteCommonDllAfterUpdate(installRoot);

                                SetProgress(progress += 5, "注册/更新服务...");
                                InstallOrUpdatePackagedServices(installRoot);
                                ReinstallArchiveServiceAfterPackageUpdate(installRoot, archiveServiceState);
                            }
                            catch
                            {
                                TryRestoreArchiveServiceAfterFailedPackageUpdate(basePath, archiveServiceState);
                                throw;
                            }
                        }

                        // 6. 同步配置
                        if (hasServiceWork)
                        {
                            SetProgress(progress += 10, "同步配置...");
                            serviceManager.ApplyConfigAndRefreshAfterInstall();
                        }

                        // 7. 更新数据库
                        if (mysqlInstalledThisRun)
                        {
                            SetProgress(progress += 15, "初始化数据库...");
                            if (!InitializeColorVisionDatabase(basePath))
                            {
                                throw new InvalidOperationException("初始化数据库失败");
                            }

                            serviceManager.ApplyConfigAndRefreshAfterInstall();
                        }
                        else if (AutoUpdateDatabase)
                        {
                            SetProgress(progress += 15, "更新数据库到最近版本...");
                            if (!ExecuteColorVisionAllSql(basePath))
                            {
                                throw new InvalidOperationException("更新数据库到最近版本失败");
                            }

                            serviceManager.ApplyConfigAndRefreshAfterInstall();
                        }

                        // 8. 启动服务
                        if (hasServiceWork)
                        {
                            SetProgress(progress += 10, "启动服务...");
                            StartInstalledServicesAfterInstall();
                        }

                        if (!string.IsNullOrWhiteSpace(basePath))
                        {
                            CleanupPackDirectory(basePath);
                        }

                        SetProgress(100, "安装完成");
                        log.Info("安装完成！");
                    }
                    catch (Exception ex)
                    {
                        log.Info($"安装失败: {ex.Message}");
                        log.Error("安装失败", ex);
                        Application.Current?.Dispatcher.Invoke(() =>
                            MessageBox.Show($"安装失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                });
            }
            finally
            {
                SetBusy(false);
                RefreshInstallComponentState();
            }
        }

        private void CleanupPackDirectory(string basePath)
        {
            try
            {
                string packDir = Path.Combine(basePath, "pack");
                if (Directory.Exists(packDir))
                {
                    Directory.Delete(packDir, true);
                    log.Info($"安装后已删除pack目录: {packDir}");
                }
            }
            catch (Exception ex)
            {
                log.Info($"清理pack目录失败: {ex.Message}");
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
                    if (IsFullServicePackageZip(zipFile))
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            ServicePackagePath = zipFile;
                            InstallServiceChecked = true;
                            MySqlPackagePath = string.Empty;
                            InstallMySqlChecked = false;
                            MqttInstallerPath = string.Empty;
                            InstallMqttChecked = false;
                            log.Info($"已识别完整服务包: {ServicePackagePath}");
                        });
                        log.Info("安装包解析完成：当前包仅包含 CVWindowsService 服务主体");
                        return;
                    }

                    string packDir = Path.Combine(Directory.GetParent(zipFile)!.FullName, Path.GetFileNameWithoutExtension(zipFile));
                    if (Directory.Exists(packDir))
                        Directory.Delete(packDir, true);

                    ZipFile.ExtractToDirectory(zipFile, packDir);
                    log.Info($"解压完成: {packDir}");

                    // 自动识别包内内容，配置路径并勾选
                    string mysqlZip = Path.Combine(packDir, "mysql-5.7.37-winx64.zip");
                    string mqttInstaller = Path.Combine(packDir, "mosquitto-2.0.18-install-windows-x64.exe");
                    string serviceZip = FindServicePackageZip(packDir);
                    bool hasSvc = !string.IsNullOrWhiteSpace(serviceZip) && File.Exists(serviceZip);

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        ServicePackagePath = hasSvc ? serviceZip : string.Empty;
                        InstallServiceChecked = hasSvc;
                        MySqlPackagePath = File.Exists(mysqlZip) ? mysqlZip : string.Empty;
                        InstallMySqlChecked = File.Exists(mysqlZip);
                        MqttInstallerPath = File.Exists(mqttInstaller) ? mqttInstaller : string.Empty;
                        InstallMqttChecked = File.Exists(mqttInstaller);

                        if (hasSvc)
                        {
                            log.Info($"已应用服务包路径: {ServicePackagePath}");
                        }
                        if (File.Exists(mysqlZip))
                        {
                            log.Info($"已应用 MySQL 包路径: {MySqlPackagePath}");
                        }
                        if (File.Exists(mqttInstaller))
                        {
                            log.Info($"已应用 MQTT 包路径: {MqttInstallerPath}");
                        }
                    });

                    log.Info($"检测到组件: 服务={hasSvc}, MySQL={File.Exists(mysqlZip)}, MQTT={File.Exists(mqttInstaller)}");
                    log.Info("一键安装包解析完成，pack目录将在安装成功后清理");
                }
                catch (Exception ex)
                {
                    log.Info($"解析一键安装包失败: {ex.Message}");
                }
            });

            SetBusy(false);
        }

        private async Task DownloadServicePackageAsync()
        {
            SetBusy(true, "正在获取最新服务包...");
            try
            {
                ApplyDownloadedServicePackage(await DownloadServicePackageFileAsync().ConfigureAwait(true));
            }
            catch (Exception ex)
            {
                log.Info($"下载 CVWindowsService 服务包失败: {ex.Message}");
                Application.Current?.Dispatcher.Invoke(() =>
                    MessageBox.Show($"下载 CVWindowsService 服务包失败: {ex.Message}", "下载服务包", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
            finally
            {
                CloseDownloadWindow();
                SetBusy(false);
            }
        }

        private async Task<string> DownloadServicePackageFileAsync()
        {
            string downloadDir = string.IsNullOrWhiteSpace(Config.DownloadLocation) ? Path.Combine(Environments.DirToolPackageCache, "CVWindowsService") : Config.DownloadLocation;
            Directory.CreateDirectory(downloadDir);
            CvwsPackageInfo packageInfo = await GetLatestCvWindowsServicePackageAsync(Config.UpdateServerUrl).ConfigureAwait(false);
            Version version = packageInfo.Version;
            if (!IsValidLatestVersion(version))
                throw new InvalidOperationException("无法获取 CVWindowsService 最新版本号");

            string? cachedPackage = FindCachedCvWindowsServicePackage(downloadDir, version);
            if (!string.IsNullOrWhiteSpace(cachedPackage) && IsFullServicePackageZip(cachedPackage))
            {
                log.Info($"已使用本地缓存服务包: {cachedPackage}");
                return cachedPackage;
            }

            IDownloadService service = GetDownloadService();
            string downloadUrl = packageInfo.DownloadUrl;
            log.Info($"开始下载 CVWindowsService 服务包 {version}: {downloadUrl}");
            Application.Current.Dispatcher.Invoke(service.ShowDownloadWindow);
            string? filePath = await DownloadWithServiceAsync(service, downloadUrl, downloadDir).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new InvalidOperationException("CVWindowsService 服务包下载失败");
            if (!IsFullServicePackageZip(filePath))
                throw new InvalidOperationException($"下载完成，但文件不是有效的 CVWindowsService 完整服务包: {filePath}");

            return filePath;
        }

        private void ApplyDownloadedServicePackage(string packagePath)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ServicePackagePath = packagePath;
                InstallServiceChecked = true;
                log.Info($"已应用服务包路径: {ServicePackagePath}");
            });
        }

        private async Task OnlineDownloadAsync()
        {
            SetBusy(true, "正在在线下载...");
            try
            {
                RefreshInstallComponentState();
                Task<string> serviceTask = DownloadServicePackageFileAsync();
                Task<string>? mysqlTask = IsMySqlInstallVisible ? DownloadMySqlPackageFileAsync() : null;
                Task<string>? mqttTask = IsMqttInstallVisible ? DownloadMqttInstallerFileAsync() : null;
                Task<string>? vc2013Task = IsVc2013InstallVisible ? DownloadVc2013InstallerFileAsync() : null;
                var tasks = new List<Task> { serviceTask };
                if (mysqlTask != null) tasks.Add(mysqlTask);
                if (mqttTask != null) tasks.Add(mqttTask);
                if (vc2013Task != null) tasks.Add(vc2013Task);

                await Task.WhenAll(tasks).ConfigureAwait(true);
                ApplyDownloadedServicePackage(serviceTask.Result);
                if (mysqlTask != null) ApplyDownloadedMySqlPackage(mysqlTask.Result);
                if (mqttTask != null) ApplyDownloadedMqttInstaller(mqttTask.Result);
                if (vc2013Task != null) ApplyDownloadedVc2013Installer(vc2013Task.Result);
                log.Info("在线下载完成");
            }
            catch (Exception ex)
            {
                log.Info($"在线下载失败: {ex.Message}");
                Application.Current?.Dispatcher.Invoke(() => MessageBox.Show($"在线下载失败: {ex.Message}", "在线下载", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
            finally
            {
                CloseDownloadWindow();
                SetBusy(false);
                RefreshInstallComponentState();
            }
        }

        private async Task DownloadMySqlPackageAsync()
        {
            SetBusy(true, "正在下载 MySQL...");
            try
            {
                ApplyDownloadedMySqlPackage(await DownloadMySqlPackageFileAsync().ConfigureAwait(true));
            }
            catch (Exception ex)
            {
                log.Info($"下载 MySQL 失败: {ex.Message}");
                Application.Current?.Dispatcher.Invoke(() => MessageBox.Show($"下载 MySQL 失败: {ex.Message}", "下载 MySQL", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
            finally
            {
                CloseDownloadWindow();
                SetBusy(false);
            }
        }

        private Task<string> DownloadMySqlPackageFileAsync()
        {
            return DownloadToolFileAsync("MySQL", item => item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase), "MySQL", "MySQL");
        }

        private void ApplyDownloadedMySqlPackage(string packagePath)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MySqlPackagePath = packagePath;
                InstallMySqlChecked = true;
            });
            log.Info($"已应用 MySQL 包路径: {packagePath}");
        }

        private async Task DownloadMqttInstallerAsync()
        {
            SetBusy(true, "正在下载 MQTT...");
            try
            {
                ApplyDownloadedMqttInstaller(await DownloadMqttInstallerFileAsync().ConfigureAwait(true));
            }
            catch (Exception ex)
            {
                log.Info($"下载 MQTT 失败: {ex.Message}");
                Application.Current?.Dispatcher.Invoke(() => MessageBox.Show($"下载 MQTT 失败: {ex.Message}", "下载 MQTT", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
            finally
            {
                CloseDownloadWindow();
                SetBusy(false);
            }
        }

        private Task<string> DownloadMqttInstallerFileAsync()
        {
            return DownloadToolFileAsync("MQTT", item => item.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase), "MQTT", "MQTT");
        }

        private void ApplyDownloadedMqttInstaller(string installerPath)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MqttInstallerPath = installerPath;
                InstallMqttChecked = true;
            });
            log.Info($"已应用 MQTT 安装包路径: {installerPath}");
        }

        private async Task DownloadVc2013InstallerAsync()
        {
            SetBusy(true, "正在下载 VC++ 2013...");
            try
            {
                ApplyDownloadedVc2013Installer(await DownloadVc2013InstallerFileAsync().ConfigureAwait(true));
            }
            catch (Exception ex)
            {
                log.Info($"下载 VC++ 2013 失败: {ex.Message}");
                Application.Current?.Dispatcher.Invoke(() => MessageBox.Show($"下载 VC++ 2013 失败: {ex.Message}", "下载 VC++ 2013", MessageBoxButton.OK, MessageBoxImage.Warning));
            }
            finally
            {
                CloseDownloadWindow();
                SetBusy(false);
            }
        }

        private async Task<string> DownloadVc2013InstallerFileAsync()
        {
            log.Info("开始下载 VC++ 2013 x64 运行库...");
            string installerPath = await DownloadToolFileAsync("VC++", item => item.Name.Equals("vcredist_2013u5_x64.exe", StringComparison.OrdinalIgnoreCase), "VC++", "VC++ 2013 x64").ConfigureAwait(false);
            log.Info($"已下载 VC++ 2013 x64 运行库: {installerPath}");
            return installerPath;
        }

        private void ApplyDownloadedVc2013Installer(string installerPath)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Vc2013InstallerPath = installerPath;
                InstallVc2013Checked = true;
            });
            log.Info($"已应用 VC++ 2013 安装包路径: {installerPath}");
        }

        private async Task<string> DownloadToolFileAsync(string toolFolder, Func<ToolFileInfo, bool> predicate, string cacheFolder, string displayName)
        {
            string downloadDir = Path.Combine(GetDownloadRoot(), cacheFolder);
            Directory.CreateDirectory(downloadDir);
            ToolFileInfo fileInfo = await GetLatestToolFileAsync(Config.UpdateServerUrl, toolFolder, predicate).ConfigureAwait(false);
            string cachedPath = Path.Combine(downloadDir, fileInfo.Name);
            if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length == fileInfo.Size)
            {
                log.Info($"已使用本地缓存 {displayName}: {cachedPath}");
                return cachedPath;
            }

            IDownloadService service = GetDownloadService();
            string downloadUrl = BuildAbsoluteDownloadUrl(Config.UpdateServerUrl, fileInfo.RelativePath);
            log.Info($"开始下载 {displayName}: {downloadUrl}");
            Application.Current.Dispatcher.Invoke(service.ShowDownloadWindow);
            string? filePath = await DownloadWithServiceAsync(service, downloadUrl, downloadDir).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new InvalidOperationException($"{displayName} 下载失败");

            return filePath;
        }

        private static IDownloadService GetDownloadService()
        {
            return AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault()
                ?? throw new InvalidOperationException("下载服务不可用");
        }

        private static void CloseDownloadWindow()
        {
            try
            {
                GetDownloadService().CloseDownloadWindow();
            }
            catch
            {
            }
        }

        private static Task<string?> DownloadWithServiceAsync(IDownloadService service, string downloadUrl, string downloadDir)
        {
            TaskCompletionSource<string?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            service.Download(downloadUrl, downloadDir, null, filePath => completion.TrySetResult(filePath));
            return completion.Task;
        }

        private static async Task<ToolFileInfo> GetLatestToolFileAsync(string updateServerUrl, string toolFolder, Func<ToolFileInfo, bool> predicate)
        {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
            string browseUrl = BuildToolBrowseApiUrl(updateServerUrl, toolFolder);
            string text = await client.GetStringAsync(browseUrl).ConfigureAwait(false);
            JObject payload = JObject.Parse(text);
            ToolFileInfo? file = payload["items"]?
                .OfType<JObject>()
                .Select(ToolFileInfo.FromJson)
                .OfType<ToolFileInfo>()
                .Where(item => !item.IsDirectory && predicate(item))
                .OrderByDescending(item => item.ModifiedUtc)
                .FirstOrDefault();

            return file ?? throw new InvalidOperationException($"未找到 Tool/{toolFolder} 下载文件");
        }

        private string GetDownloadRoot()
        {
            return string.IsNullOrWhiteSpace(Config.DownloadLocation) ? Path.Combine(Environments.DirToolPackageCache, "CVWindowsService") : Config.DownloadLocation;
        }

        private static string BuildToolBrowseApiUrl(string updateServerUrl, string toolFolder)
        {
            Uri root = BuildServerRoot(updateServerUrl);
            return new Uri(root, $"/api/site/browse/Tool/{Uri.EscapeDataString(toolFolder)}").ToString();
        }

        private static string BuildAbsoluteDownloadUrl(string updateServerUrl, string relativePath)
        {
            Uri root = BuildServerRoot(updateServerUrl);
            return new Uri(root, $"/download/{relativePath.Replace("+", "%2B", StringComparison.Ordinal)}").ToString();
        }

        private static bool IsVc2013RuntimeInstalled()
        {
            return File.Exists(Path.Combine(Environment.SystemDirectory, "msvcr120.dll"))
                && File.Exists(Path.Combine(Environment.SystemDirectory, "msvcp120.dll"));
        }

        private static bool IsValidLatestVersion(Version version)
        {
            return version.Major > 0 || version.Minor > 0 || version.Build > 0 || version.Revision > 0;
        }

        private sealed class ToolFileInfo
        {
            public string Name { get; init; } = string.Empty;
            public string RelativePath { get; init; } = string.Empty;
            public bool IsDirectory { get; init; }
            public long Size { get; init; }
            public DateTimeOffset ModifiedUtc { get; init; }

            public static ToolFileInfo? FromJson(JObject item)
            {
                string name = item["name"]?.ToString() ?? string.Empty;
                string relativePath = item["relative_path"]?.ToString() ?? item["path"]?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relativePath))
                    return null;

                DateTimeOffset modifiedUtc = DateTimeOffset.MinValue;
                if (!DateTimeOffset.TryParse(item["modified_iso"]?.ToString(), out modifiedUtc))
                    modifiedUtc = DateTimeOffset.MinValue;

                return new ToolFileInfo
                {
                    Name = name,
                    RelativePath = relativePath,
                    IsDirectory = item["is_dir"]?.Value<bool>() ?? false,
                    Size = item["size"]?.Value<long>() ?? 0,
                    ModifiedUtc = modifiedUtc,
                };
            }
        }

        private static string? FindCachedCvWindowsServicePackage(string downloadDir, Version version)
        {
            if (!Directory.Exists(downloadDir))
                return null;

            return Directory.GetFiles(downloadDir, $"CVWindowsService[{version}]*.zip", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        private sealed class CvwsPackageInfo
        {
            public CvwsPackageInfo(Version version, string downloadUrl)
            {
                Version = version;
                DownloadUrl = downloadUrl;
            }

            public Version Version { get; }

            public string DownloadUrl { get; }
        }

        private static async Task<CvwsPackageInfo> GetLatestCvWindowsServicePackageAsync(string updateServerUrl)
        {
            using HttpClient client = new()
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            foreach (string serverUrl in GetCvwsServerCandidates(updateServerUrl))
            {
                CvwsPackageInfo? packageInfo = await TryGetLatestCvWindowsServicePackageAsync(client, serverUrl).ConfigureAwait(false);
                if (packageInfo != null)
                    return packageInfo;
            }

            throw new InvalidOperationException("无法获取 CVWindowsService 最新版本号");
        }

        private static async Task<CvwsPackageInfo?> TryGetLatestCvWindowsServicePackageAsync(HttpClient client, string serverUrl)
        {
            Uri root = BuildServerRoot(serverUrl);

            string releasesUrl = BuildCvwsApiUrl(serverUrl, "releases");
            string? releasesText = await TryGetStringAsync(client, releasesUrl).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(releasesText) && TryParseCvwsReleases(releasesText, root, out CvwsPackageInfo? packageInfo))
                return packageInfo;

            string latestUrl = BuildCvwsApiUrl(serverUrl, "latest-version");
            string? latestText = await TryGetStringAsync(client, latestUrl).ConfigureAwait(false);
            if (TryParseCvwsVersion(latestText, out Version? version))
            {
                string downloadUrl = BuildCvwsApiUrl(serverUrl, $"download/{Uri.EscapeDataString(version.ToString())}");
                return new CvwsPackageInfo(version, downloadUrl);
            }

            return null;
        }

        private static IEnumerable<string> GetCvwsServerCandidates(string updateServerUrl)
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (string? candidate in new[] { updateServerUrl, MarketplaceConfig.DefaultServiceBaseUrl })
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                Uri root;
                try
                {
                    root = BuildServerRoot(candidate);
                }
                catch
                {
                    continue;
                }

                string value = root.ToString().TrimEnd('/');
                if (seen.Add(value))
                    yield return value;
            }
        }

        private static async Task<string?> TryGetStringAsync(HttpClient client, string url)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryParseCvwsReleases(string releasesText, Uri root, out CvwsPackageInfo? packageInfo)
        {
            packageInfo = null;

            try
            {
                JObject payload = JObject.Parse(releasesText);
                if (!TryParseCvwsVersion(payload["latestVersion"]?.ToString(), out Version? latestVersion))
                    return false;

                JArray? packages = payload["packages"] as JArray;
                JObject? package = packages?
                    .OfType<JObject>()
                    .Where(item => string.Equals(item["version"]?.ToString(), latestVersion.ToString(), StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => TryParseInt(item["suffix"]?.ToString()))
                    .FirstOrDefault();

                string? downloadUrl = package?["downloadUrl"]?.ToString();
                if (string.IsNullOrWhiteSpace(downloadUrl))
                    downloadUrl = BuildCvwsApiUrl(root.ToString(), $"download/{Uri.EscapeDataString(latestVersion.ToString())}");
                else
                    downloadUrl = BuildAbsoluteUrl(root, downloadUrl);

                packageInfo = new CvwsPackageInfo(latestVersion, downloadUrl);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseCvwsVersion(string? text, out Version version)
        {
            version = new Version();
            if (string.IsNullOrWhiteSpace(text))
                return false;

            string value = text.Trim();
            if (value.StartsWith('{'))
            {
                try
                {
                    JObject payload = JObject.Parse(value);
                    value = payload["version"]?.ToString()
                        ?? payload["latestVersion"]?.ToString()
                        ?? string.Empty;
                }
                catch
                {
                    return false;
                }
            }

            return Version.TryParse(value.Trim(), out version!);
        }

        private static int TryParseInt(string? text)
        {
            return int.TryParse(text, out int value) ? value : 0;
        }

        private static Uri BuildServerRoot(string updateServerUrl)
        {
            string serverUrl = string.IsNullOrWhiteSpace(updateServerUrl)
                ? MarketplaceConfig.DefaultServiceBaseUrl
                : updateServerUrl.Trim();

            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri? configuredUri))
                throw new InvalidOperationException($"服务更新地址格式不正确: {serverUrl}");

            return new Uri(configuredUri.GetLeftPart(UriPartial.Authority));
        }

        private static string BuildAbsoluteUrl(Uri root, string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri? absoluteUri))
                return absoluteUri.ToString();

            return new Uri(root, url).ToString();
        }

        private static string BuildCvwsApiUrl(string updateServerUrl, string relativePath)
        {
            Uri root = BuildServerRoot(updateServerUrl);
            return new Uri(root, $"/api/tool/cvwindowsservice/{relativePath.TrimStart('/')}").ToString();
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

        private static bool IsFullServicePackageZip(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var topLevelNames = archive.Entries
                    .Select(entry => GetTopLevelEntryName(entry.FullName))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                return topLevelNames.Contains("RegWindowsService")
                    && (topLevelNames.Contains("CVMainWindowsService_x64")
                        || topLevelNames.Contains("CVMainWindowsService_dev"));
            }
            catch
            {
                return false;
            }
        }

        private void InstallMqttFromExe(string exeFile)
        {
            ServiceManagerViewModel.Instance.MqttManager.InstallFromExe(exeFile, log.Info);
            Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
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
                        log.Info($"停止服务: {svc.ServiceName}");
                        ServiceHostWindowsServiceController
                            .ExecuteAsync(svc.ServiceName, ServiceHostServiceOperation.Stop, log.Info, svc.DisplayName)
                            .GetAwaiter()
                            .GetResult();
                    }
                }
                catch (Exception ex)
                {
                    log.Info($"停止服务失败: {svc.ServiceName}, {ex.Message}");
                }
                Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
            }
        }

        private void StopLegacyManagementToolProcesses()
        {
            foreach (var process in Process.GetProcessesByName("CVWinSMS"))
            {
                try
                {
                    log.Info($"关闭旧版服务管理工具进程: CVWinSMS, PID={process.Id}");
                    if (!process.CloseMainWindow())
                    {
                        process.Kill();
                    }
                    else if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    log.Info($"关闭 CVWinSMS 进程失败: PID={process.Id}, {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
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
                        log.Info($"启动服务: {svc.ServiceName}");
                        ServiceHostWindowsServiceController
                            .ExecuteAsync(svc.ServiceName, ServiceHostServiceOperation.Start, log.Info, svc.DisplayName)
                            .GetAwaiter()
                            .GetResult();
                    }
                }
                catch (Exception ex)
                {
                    log.Info($"启动服务失败: {svc.ServiceName}, {ex.Message}");
                }
                Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
            }

        }

        private void StartInstalledServicesAfterInstall()
        {
            var serviceManager = ServiceManagerViewModel.Instance;
            Application.Current.Dispatcher.Invoke(() => serviceManager.RefreshAll());

            serviceManager.MySqlManager.RefreshStatus(serviceManager.Services, serviceManager.Config.MySqlPort);
            if (serviceManager.MySqlManager.Config.IsInstalled && !serviceManager.MySqlManager.Config.IsRunning)
            {
                log.Info($"启动 MySQL 服务: {serviceManager.MySqlManager.Helper.ServiceName}");
                serviceManager.MySqlManager.StartViaServiceHostAsync(log.Info).GetAwaiter().GetResult();
            }

            serviceManager.MqttManager.RefreshStatus(serviceManager.Services);
            if (serviceManager.MqttManager.Config.IsInstalled && !serviceManager.MqttManager.Config.IsRunning)
            {
                log.Info($"启动 MQTT 服务: {serviceManager.MqttManager.Config.ServiceName}");
                serviceManager.MqttManager.StartViaServiceHostAsync(log.Info).GetAwaiter().GetResult();
            }

            StartPackagedServices();
            Application.Current.Dispatcher.Invoke(() => serviceManager.RefreshAll());
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
                    log.Info($"跳过服务（未找到可执行文件）: {svc.ServiceName}");
                    continue;
                }

                bool ok = ServiceHostWindowsServiceController
                    .InstallAsync(svc.ServiceName, exePath, log.Info, svc.DisplayName, startAfterInstall: false)
                    .GetAwaiter()
                    .GetResult();
                Application.Current.Dispatcher.Invoke(() => ServiceManagerViewModel.Instance.RefreshAll());
                log.Info(ok
                    ? $"服务安装成功: {svc.ServiceName}"
                    : $"服务安装失败: {svc.ServiceName}");
            }
        }

        private ArchiveServiceInstallState? UninstallArchiveServiceBeforePackageUpdate()
        {
            if (!WinServiceHelper.IsServiceExisted(ArchiveServiceName))
                return null;

            var state = new ArchiveServiceInstallState(
                WinServiceHelper.GetServiceInstallPath(ArchiveServiceName),
                WinServiceHelper.IsServiceRunning(ArchiveServiceName),
                WinServiceHelper.GetServiceStartType(ArchiveServiceName));

            log.Info($"检测到已有 {ArchiveServiceName}，安装前先卸载以释放服务文件");
            bool uninstalled = ServiceHostWindowsServiceController
                .UninstallAsync(ArchiveServiceName, log.Info, ArchiveServiceDisplayName)
                .GetAwaiter()
                .GetResult();
            if (!uninstalled)
            {
                throw new InvalidOperationException($"卸载 {ArchiveServiceName} 失败，已停止更新服务包以避免文件占用");
            }

            log.Info($"安装前已卸载服务: {ArchiveServiceName}");
            return state;
        }

        private void ReinstallArchiveServiceAfterPackageUpdate(string installRoot, ArchiveServiceInstallState? state)
        {
            if (state == null)
                return;

            string executablePath = Path.Combine(installRoot, "RegWindowsService", ArchiveServiceExecutableName);
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException($"原有 {ArchiveServiceName} 已卸载，但新服务包中未找到归档服务程序", executablePath);
            }

            log.Info($"重新安装服务: {ArchiveServiceName} => {executablePath}");
            bool installed = ServiceHostWindowsServiceController
                .InstallAsync(
                    ArchiveServiceName,
                    executablePath,
                    log.Info,
                    ArchiveServiceDisplayName,
                    startAfterInstall: state.WasRunning,
                    startType: state.StartType,
                    description: "归档服务")
                .GetAwaiter()
                .GetResult();
            if (!installed)
            {
                throw new InvalidOperationException($"重新安装 {ArchiveServiceName} 失败");
            }

            log.Info(state.WasRunning
                ? $"服务重新安装并恢复运行: {ArchiveServiceName}"
                : $"服务重新安装成功: {ArchiveServiceName}");
        }

        private void TryRestoreArchiveServiceAfterFailedPackageUpdate(string basePath, ArchiveServiceInstallState? state)
        {
            if (state == null || WinServiceHelper.IsServiceExisted(ArchiveServiceName))
                return;

            string currentExecutablePath = Path.Combine(ResolveServiceInstallRoot(basePath), "RegWindowsService", ArchiveServiceExecutableName);
            string? executablePath = new[] { currentExecutablePath, state.PreviousExecutablePath }
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                log.Info($"安装失败后无法恢复 {ArchiveServiceName}：归档服务程序已不存在");
                return;
            }

            log.Info($"安装失败，尝试恢复原有服务: {ArchiveServiceName}");
            bool restored = ServiceHostWindowsServiceController
                .InstallAsync(
                    ArchiveServiceName,
                    executablePath,
                    log.Info,
                    ArchiveServiceDisplayName,
                    startAfterInstall: state.WasRunning,
                    startType: state.StartType,
                    description: "归档服务")
                .GetAwaiter()
                .GetResult();
            log.Info(restored
                ? $"已恢复原有服务: {ArchiveServiceName}"
                : $"恢复原有服务失败: {ArchiveServiceName}");
        }

        private static bool WaitForServiceRemoval(string serviceName, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (!WinServiceHelper.IsServiceExisted(serviceName))
                    return true;

                System.Threading.Thread.Sleep(250);
            }

            return !WinServiceHelper.IsServiceExisted(serviceName);
        }

        private sealed record ArchiveServiceInstallState(string? PreviousExecutablePath, bool WasRunning, string StartType);

        private void CleanExistingServicePackageTargets(string zipPath, string basePath)
        {
            string fullBasePath = Path.GetFullPath(basePath);
            Directory.CreateDirectory(fullBasePath);

            using var archive = ZipFile.OpenRead(zipPath);
            var topLevelNames = archive.Entries
                .Select(entry => GetTopLevelEntryName(entry.FullName))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (string name in topLevelNames)
            {
                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    log.Info($"跳过异常安装包路径: {name}");
                    continue;
                }

                string targetPath = Path.GetFullPath(Path.Combine(fullBasePath, name));
                if (!IsPathInsideDirectory(targetPath, fullBasePath))
                {
                    log.Info($"跳过安装根目录外路径: {targetPath}");
                    continue;
                }

                try
                {
                    if (Directory.Exists(targetPath))
                    {
                        DeleteExistingPathWithRetry(targetPath);
                        log.Info($"已清理旧目录: {targetPath}");
                    }
                    else if (File.Exists(targetPath))
                    {
                        DeleteExistingPathWithRetry(targetPath);
                        log.Info($"已清理旧文件: {targetPath}");
                    }
                }
                catch (Exception ex)
                {
                    throw new IOException($"清理旧服务文件失败: {targetPath}. {ex.Message}", ex);
                }
            }
        }

        private void DeleteExistingPathWithRetry(string targetPath)
        {
            const int maxAttempts = 25;
            TimeSpan retryDelay = TimeSpan.FromMilliseconds(800);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (Directory.Exists(targetPath))
                    {
                        TryNormalizeFileAttributes(targetPath);
                        Directory.Delete(targetPath, true);
                    }
                    else if (File.Exists(targetPath))
                    {
                        TryNormalizeFileAttributes(targetPath);
                        File.Delete(targetPath);
                    }

                    return;
                }
                catch (Exception ex) when (IsRetryableCleanupException(ex) && attempt < maxAttempts)
                {
                    if (attempt == 1 || attempt % 5 == 0)
                    {
                        log.Info($"旧服务文件仍被占用，等待释放后重试({attempt}/{maxAttempts}): {targetPath}");
                    }

                    System.Threading.Thread.Sleep(retryDelay);
                }
            }
        }

        private static bool IsRetryableCleanupException(Exception ex)
        {
            return ex is IOException or UnauthorizedAccessException;
        }

        private static void TryNormalizeFileAttributes(string targetPath)
        {
            try
            {
                if (File.Exists(targetPath))
                {
                    File.SetAttributes(targetPath, FileAttributes.Normal);
                    return;
                }

                if (!Directory.Exists(targetPath))
                    return;

                foreach (string file in Directory.EnumerateFiles(targetPath, "*", SearchOption.AllDirectories))
                {
                    TrySetNormalAttributes(file);
                }

                foreach (string directory in Directory.EnumerateDirectories(targetPath, "*", SearchOption.AllDirectories)
                             .OrderByDescending(path => path.Length))
                {
                    TrySetNormalAttributes(directory);
                }

                TrySetNormalAttributes(targetPath);
            }
            catch
            {
            }
        }

        private static void TrySetNormalAttributes(string path)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
            catch
            {
            }
        }

        private static string GetTopLevelEntryName(string entryName)
        {
            string normalized = entryName.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            string top = normalized.Split('/')[0].Trim();
            return top is "." or ".." ? string.Empty : top;
        }

        private static bool IsPathInsideDirectory(string path, string directory)
        {
            string fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveServiceInstallRoot(string basePath)
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

        private bool ExecuteColorVisionAllSql(string basePath)
        {
            return ServiceManagerViewModel.Instance.MySqlManager.ExecuteColorVisionAllSql(basePath, log.Info);
        }

        private bool InitializeColorVisionDatabase(string basePath)
        {
            return ServiceManagerViewModel.Instance.MySqlManager.InitializeColorVisionDatabase(basePath, log.Info);
        }

        private bool InstallVc2013Runtime()
        {
            try
            {
                if (IsVc2013RuntimeInstalled())
                    return true;
                if (string.IsNullOrWhiteSpace(Vc2013InstallerPath) || !File.Exists(Vc2013InstallerPath))
                    throw new InvalidOperationException("请先选择或下载 VC++ 2013 x64 运行库安装程序");

                log.Info($"正在安装 VC++ 2013 x64 运行库: {Vc2013InstallerPath}");
                var startInfo = new ProcessStartInfo
                {
                    FileName = Vc2013InstallerPath,
                    Arguments = "/install /quiet /norestart",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(Vc2013InstallerPath) ?? string.Empty,
                };

                using Process? process = Process.Start(startInfo);
                if (process == null)
                    throw new InvalidOperationException("无法启动 VC++ 2013 安装程序");
                if (!process.WaitForExit(180000))
                {
                    process.Kill(true);
                    throw new TimeoutException("VC++ 2013 安装超时");
                }

                bool success = process.ExitCode is 0 or 3010 or 1638;
                log.Info(success ? $"VC++ 2013 x64 运行库安装完成，退出码: {process.ExitCode}" : $"VC++ 2013 x64 运行库安装失败，退出码: {process.ExitCode}");
                return success && IsVc2013RuntimeInstalled();
            }
            catch (Exception ex)
            {
                log.Info($"VC++ 2013 x64 运行库安装失败: {ex.Message}");
                return false;
            }
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
                            log.Info($"CommonDll 复制目标目录不存在，跳过: {target}");
                            continue;
                        }

                        int copiedCount = CopyDirectoryRecursive(commonDllDir, target);
                        log.Info($"已复制 CommonDll 到: {target}，文件数: {copiedCount}");
                    }

                    Directory.Delete(commonDllDir, true);
                    log.Info("安装后已删除 CommonDll 目录");
                }
                else
                {
                    log.Info($"未找到 CommonDll 目录: {commonDllDir}");
                }
            }
            catch (Exception ex)
            {
                log.Info($"删除 CommonDll 失败: {ex.Message}");
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
}
