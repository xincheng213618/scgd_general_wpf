using ColorVision.UI;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace WindowsServicePlugin.ServiceManager
{
    /// <summary>
    /// 下载相关：服务包、MySQL、MQTT 的在线下载逻辑
    /// </summary>
    public partial class ServiceInstallViewModel
    {
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
                if (File.Exists(targetPath))
                {
                    ServicePackagePath = targetPath;
                    UpdateStatusText = $"文件已存在: {targetPath}";
                    AddLog($"文件已存在，跳过下载: {targetPath}");
                    return;
                }

                UpdateStatusText = $"发现新版本: {latest.Version}";
                AddLog($"发现新版本: {latest.Version}");

                var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
                if (service == null)
                {
                    UpdateStatusText = "下载失败：下载服务不可用";
                    AddLog("下载服务不可用");
                    return;
                }

                service.ShowDownloadWindow();
                var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                service.Download(latest.DownloadUrl, downloadDir, DownloadFileConfig.Instance.Authorization, path => tcs.TrySetResult(path));
                string? downloadedPath = await tcs.Task;

                if (downloadedPath == null)
                {
                    UpdateStatusText = "下载失败";
                    AddLog("下载失败");
                    return;
                }

                ServicePackagePath = downloadedPath;
                UpdateStatusText = $"下载完成: {downloadedPath}";
                AddLog($"下载完成: {downloadedPath}");
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

            const string fileName = "mysql-5.7.37-winx64.zip";
            string targetPath = Path.Combine(downloadDir, fileName);
            if (File.Exists(targetPath))
            {
                MySqlPackagePath = targetPath;
                AddLog($"文件已存在，跳过下载: {targetPath}");
                return;
            }

            var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
            if (service == null)
            {
                AddLog("下载服务不可用");
                return;
            }

            SetBusy(true, "正在下载 MySQL...");
            try
            {
                service.ShowDownloadWindow();
                foreach (string baseUrl in GetApiBaseCandidates(Config.UpdateServerUrl))
                {
                    string url = baseUrl.TrimEnd('/') + "/download/Tool/Mysql/" + fileName;
                    var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                    service.Download(url, downloadDir, DownloadFileConfig.Instance.Authorization, path => tcs.TrySetResult(path));
                    string? downloadedPath = await tcs.Task;
                    if (downloadedPath != null)
                    {
                        MySqlPackagePath = downloadedPath;
                        AddLog($"MySQL 下载完成: {downloadedPath}");
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

            const string fileName = "mosquitto-2.0.18-install-windows-x64.exe";
            string targetPath = Path.Combine(downloadDir, fileName);
            if (File.Exists(targetPath))
            {
                MqttInstallerPath = targetPath;
                AddLog($"文件已存在，跳过下载: {targetPath}");
                return;
            }

            var service = AssemblyHandler.GetInstance().LoadImplementations<IDownloadService>().FirstOrDefault();
            if (service == null)
            {
                AddLog("下载服务不可用");
                return;
            }

            SetBusy(true, "正在下载 MQTT...");
            try
            {
                service.ShowDownloadWindow();
                foreach (string baseUrl in GetApiBaseCandidates(Config.UpdateServerUrl))
                {
                    string url = baseUrl.TrimEnd('/') + "/download/Tool/MQTT/" + fileName;
                    var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                    service.Download(url, downloadDir, DownloadFileConfig.Instance.Authorization, path => tcs.TrySetResult(path));
                    string? downloadedPath = await tcs.Task;
                    if (downloadedPath != null)
                    {
                        MqttInstallerPath = downloadedPath;
                        AddLog($"MQTT 下载完成: {downloadedPath}");
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
                    string downloadRelativePath = string.Empty;
                    if (root.TryGetProperty("packages", out var packagesArray))
                    {
                        foreach (var pkg in packagesArray.EnumerateArray())
                        {
                            string pkgVersion = pkg.TryGetProperty("version", out var pv) ? pv.GetString() ?? string.Empty : string.Empty;
                            if (pkgVersion == latestVersion)
                            {
                                fileName = pkg.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? fileName : fileName;
                                downloadRelativePath = pkg.TryGetProperty("downloadUrl", out var du) ? du.GetString() ?? string.Empty : string.Empty;
                                break;
                            }
                        }
                    }

                    // Use the server-provided downloadUrl (contains the actual filename in path,
                    // matching the AutoUpdater pattern so Aria2c derives the correct filename).
                    // Fall back to the version-based API endpoint if not present.
                    string downloadUrl = !string.IsNullOrWhiteSpace(downloadRelativePath)
                        ? apiBaseUrl.TrimEnd('/') + downloadRelativePath
                        : apiBaseUrl.TrimEnd('/') + "/api/tool/cvwindowsservice/download/" + latestVersion;
                    return new ServicePackageInfo(version, fileName, downloadUrl);
                }
                catch
                {
                }
            }

            return null;
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
    }
}
