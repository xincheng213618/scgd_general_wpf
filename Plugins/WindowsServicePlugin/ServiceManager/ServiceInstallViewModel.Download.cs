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
    }
}
