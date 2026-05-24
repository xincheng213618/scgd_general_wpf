using ColorVision.Common.MVVM;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Plugins;
using log4net;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Desktop.Marketplace
{
  
    public class MarketplaceWindowConfig : WindowConfig
    {
        public static MarketplaceWindowConfig Instance => ConfigService.Instance.GetRequiredService<MarketplaceWindowConfig>();

        /// <summary>
        /// 是否自动更新插件
        /// </summary>
        /// 
        [ConfigSetting(Order = 999)]
        [DisplayName("CheckPluginUpdates")]
        public bool IsAutoUpdate { get => _IsAutoUpdate; set { _IsAutoUpdate = value; OnPropertyChanged(); } }
        private bool _IsAutoUpdate = true;
    }


    public class MarketplaceManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceManager));
        private static MarketplaceManager _instance;
        private static readonly object _locker = new();
        public static MarketplaceManager GetInstance() { lock (_locker) { _instance ??= new MarketplaceManager(); return _instance; } }
        public ObservableCollection<PluginInfoVM> Plugins { get; private set; } = new ObservableCollection<PluginInfoVM>();
        public static MarketplaceWindowConfig Config => MarketplaceWindowConfig.Instance;
        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand InstallPackageCommand { get; set; }
        public RelayCommand DownloadPackageCommand { get; set; }
        public RelayCommand OpenViewDllViersionCommand { get; set; }
        public RelayCommand RestartCommand { get; set; }
        public RelayCommand RefreshVersionsCommand { get; set; }

        // 在 PluginLoader 类中添加
        public RelayCommand UpdateAllCommand { get; set; }

        public int UpdateAvailableCount { get => _UpdateAvailableCount; set { _UpdateAvailableCount = value; OnPropertyChanged(); } }
        private int _UpdateAvailableCount;
        private readonly SemaphoreSlim _refreshVersionsLock = new(1, 1);

        public MarketplaceManager()
        {
            log.Info(UI.Properties.Resources.CheckingForAdditionalProjects);

            foreach (var item in UI.Plugins.PluginLoader.Config.Plugins)
            {
                if (item.Value.Manifest != null)
                {
                    // skipIndividualCheck=true: batch check will provide versions
                    PluginInfoVM info = new PluginInfoVM(item.Value, skipIndividualCheck: true);
                    info.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(PluginInfoVM.HasUpdate))
                        {
                            UpdateAvailableCount = Plugins.Count(p => p.HasUpdate);
                        }
                    };
                    Plugins.Add(info);
                }
            }

            _ = RefreshVersionsAsync();
 
            InstallPackageCommand = new RelayCommand(a => InstallPackage());
            DownloadPackageCommand = new RelayCommand(a => DownloadPackage());
            EditConfigCommand = new RelayCommand(a => new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            OpenViewDllViersionCommand = new RelayCommand(a => OpenViewDllViersion());
            RestartCommand = new RelayCommand(a => Restart());
            RefreshVersionsCommand = new RelayCommand(async a => await RefreshVersionsAsync());
            UpdateAllCommand = new RelayCommand(a => UpdateAll());
        }

        public async Task RefreshVersionsAsync()
        {
            await _refreshVersionsLock.WaitAsync();
            try
            {
                var enabledPlugins = Plugins.Where(p => p.PluginInfo.Enabled && !string.IsNullOrWhiteSpace(p.PackageName)).ToList();
                if (enabledPlugins.Count == 0)
                {
                    UpdateAvailableCount = 0;
                    return;
                }

                var client = MarketplaceClient.GetInstance();
                Dictionary<string, string?> versions = new(StringComparer.OrdinalIgnoreCase);

                try
                {
                    versions = await client.BatchVersionCheckAsync(enabledPlugins.Select(p => p.PackageName!).ToList());
                }
                catch (Exception ex)
                {
                    log.Warn($"Batch version check failed, falling back to individual checks: {ex.Message}");
                }

                var fallbackTasks = new List<Task>();
                foreach (var plugin in enabledPlugins)
                {
                    if (versions.TryGetValue(plugin.PackageName!, out string? version) && !string.IsNullOrWhiteSpace(version))
                    {
                        plugin.SetVersionFromBatchCheck(version);
                    }
                    else
                    {
                        fallbackTasks.Add(plugin.CheckVersionAsync());
                    }
                }

                if (fallbackTasks.Count > 0)
                {
                    await Task.WhenAll(fallbackTasks);
                }

                UpdateAvailableCount = Plugins.Count(p => p.HasUpdate);
            }
            catch (Exception ex)
            {
                log.Warn($"RefreshVersionsAsync failed: {ex.Message}");
            }
            finally
            {
                _refreshVersionsLock.Release();
            }
        }

        public async Task<CombinedPluginUpdatePlan> BuildCombinedUpdatePlanAsync(Version hostVersion)
        {
            await RefreshVersionsAsync();

            CombinedPluginUpdatePlan plan = new();
            var pluginsToCheck = Plugins
                .Where(plugin => plugin.PluginInfo.Enabled && plugin.HasUpdate && !string.IsNullOrWhiteSpace(plugin.PackageName))
                .ToList();

            if (pluginsToCheck.Count == 0)
                return plan;

            MarketplaceClient client = MarketplaceClient.GetInstance();
            foreach (var plugin in pluginsToCheck)
            {
                MarketplacePluginVersionInfo? candidate = null;
                try
                {
                    MarketplacePluginDetail? detail = await client.GetPluginDetailAsync(plugin.PackageName!);
                    candidate = SelectCompatibleVersion(plugin, detail, hostVersion);
                }
                catch (Exception ex)
                {
                    log.Warn($"BuildCombinedUpdatePlanAsync failed for {plugin.PackageName}: {ex.Message}");
                }

                candidate ??= CreateFallbackCandidate(plugin);

                if (candidate != null)
                {
                    plan.Updates.Add(new CombinedPluginUpdateItem
                    {
                        Plugin = plugin,
                        VersionInfo = candidate,
                    });
                }
                else
                {
                    plan.SkippedIncompatiblePlugins.Add(plugin.Name ?? plugin.PackageName ?? "Unknown");
                }
            }

            return plan;
        }

        public bool StartCombinedUpdate(CombinedPluginUpdatePlan plan, string? restartArguments = null, Action? noRestartAction = null)
        {
            if (plan.Updates.Count == 0)
            {
                noRestartAction?.Invoke();
                return false;
            }

            string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");
            var manager = Aria2cDownloadManager.GetInstance();
            var client = MarketplaceClient.GetInstance();

            int totalCount = plan.Updates.Count;
            int completedCount = 0;
            var completedPaths = new ConcurrentBag<string>();
            object lockObj = new();

            List<CombinedPluginUpdateItem> pluginsNeedingDownload = new();
            foreach (var item in plan.Updates)
            {
                string version = item.VersionInfo.Version;
                string? expectedHash = item.VersionInfo.FileHash;
                string? existingFile = MarketplaceClient.GetExistingFileIfValid(downloadDir, item.Plugin.PackageName!, version, expectedHash);
                if (existingFile != null)
                {
                    log.Info($"Plugin {item.Plugin.PackageName} v{version} already exists at {existingFile}, skipping download.");
                    completedPaths.Add(existingFile);
                    lock (lockObj)
                    {
                        completedCount++;
                    }
                }
                else
                {
                    pluginsNeedingDownload.Add(item);
                }
            }

            if (pluginsNeedingDownload.Count == 0)
            {
                var cachedPaths = completedPaths.ToArray();
                if (cachedPaths.Length > 0)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        PluginUpdater.UpdatePluginWithRestartArguments(restartArguments, cachedPaths);
                    });
                    return true;
                }

                noRestartAction?.Invoke();
                return false;
            }

            DownloadWindow.ShowInstance();

            foreach (var item in pluginsNeedingDownload)
            {
                string version = item.VersionInfo.Version;
                string? expectedHash = item.VersionInfo.FileHash;
                string url = client.GetDownloadUrl(item.Plugin.PackageName!, version);
                string expectedFileName = $"{item.Plugin.PackageName}-{version}.cvxp";

                manager.AddDownload(url, downloadDir, DownloadFileConfig.Instance.Authorization, task =>
                {
                    if (task.Status == DownloadStatus.Completed)
                    {
                        if (!string.IsNullOrWhiteSpace(expectedHash) && !MarketplaceClient.VerifyFileHash(task.SavePath, expectedHash))
                        {
                            log.Error($"Combined update hash mismatch for {item.Plugin.PackageName} v{version}.");
                        }
                        else
                        {
                            completedPaths.Add(task.SavePath);
                        }
                    }
                    else
                    {
                        log.Error($"Combined update download failed for {item.Plugin.PackageName}: {task.ErrorMessage}");
                    }

                    int current;
                    lock (lockObj)
                    {
                        completedCount++;
                        current = completedCount;
                    }

                    if (current == totalCount)
                    {
                        var downloadedPaths = completedPaths.ToArray();
                        if (downloadedPaths.Length > 0)
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                PluginUpdater.UpdatePluginWithRestartArguments(restartArguments, downloadedPaths);
                            });
                        }
                        else
                        {
                            noRestartAction?.Invoke();
                        }
                    }
                }, expectedFileName);
            }

            return true;
        }

        public void UpdateAll()
        {
            var pluginsToUpdate = Plugins.Where(p => p.PluginInfo.Enabled && p.HasUpdate).ToList();
            
            if (pluginsToUpdate.Count == 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "没有可更新的插件", "插件管理", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(Application.Current.GetActiveWindow(), 
                $"检测到 {pluginsToUpdate.Count} 个插件有可用更新，是否全部更新？", 
                "一键更新", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");
            var manager = Aria2cDownloadManager.GetInstance();
            string auth = DownloadFileConfig.Instance.Authorization;
            var client = MarketplaceClient.GetInstance();

            // Track all download tasks and their completed file paths
            int totalCount = pluginsToUpdate.Count;
            int completedCount = 0;
            var completedPaths = new System.Collections.Concurrent.ConcurrentBag<string>();
            object lockObj = new();

            // First, check which files already exist (skip redundant downloads)
            var pluginsNeedingDownload = new List<PluginInfoVM>();
            foreach (var plugin in pluginsToUpdate)
            {
                string? existingFile = MarketplaceClient.GetExistingFileIfValid(downloadDir, plugin.PackageName, plugin.LastVersion.ToString(), null);
                if (existingFile != null)
                {
                    log.Info($"Plugin {plugin.PackageName} v{plugin.LastVersion} already exists at {existingFile}, skipping download.");
                    completedPaths.Add(existingFile);
                    lock (lockObj) { completedCount++; }
                }
                else
                {
                    pluginsNeedingDownload.Add(plugin);
                }
            }

            if (pluginsNeedingDownload.Count == 0)
            {
                // All files already exist, apply updates directly
                var paths = completedPaths.ToArray();
                if (paths.Length > 0)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        PluginUpdater.UpdatePlugin(paths);
                    });
                }
                return;
            }

            DownloadWindow.ShowInstance();

            foreach (var plugin in pluginsNeedingDownload)
            {
                string url = client.GetDownloadUrl(plugin.PackageName, plugin.LastVersion.ToString());
                string expectedFileName = $"{plugin.PackageName}-{plugin.LastVersion}.cvxp";

                manager.AddDownload(url, downloadDir, auth, task =>
                {
                    if (task.Status == DownloadStatus.Completed)
                    {
                        completedPaths.Add(task.SavePath);
                    }
                    else
                    {
                        log.Error($"UpdateAll: Download failed for {plugin.PackageName}: {task.ErrorMessage}");
                    }

                    int current;
                    lock (lockObj)
                    {
                        completedCount++;
                        current = completedCount;
                    }

                    // When all downloads have finished (success or failure), apply updates in batch
                    if (current == totalCount)
                    {
                        var paths = completedPaths.ToArray();
                        if (paths.Length > 0)
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                PluginUpdater.UpdatePlugin(paths);
                            });
                        }
                        else
                        {
                            log.Warn("UpdateAll: All downloads failed, no plugins to update.");
                        }
                    }
                }, expectedFileName);
            }
        }

        private static MarketplacePluginVersionInfo? CreateFallbackCandidate(PluginInfoVM plugin)
        {
            if (plugin.LastVersion == null)
                return null;

            if (plugin.AssemblyVersion != null && plugin.LastVersion <= plugin.AssemblyVersion)
                return null;

            return new MarketplacePluginVersionInfo
            {
                Version = plugin.LastVersion.ToString(),
            };
        }

        private static MarketplacePluginVersionInfo? SelectCompatibleVersion(PluginInfoVM plugin, MarketplacePluginDetail? detail, Version hostVersion)
        {
            if (detail == null)
                return null;

            var versions = detail.Versions
                .Concat(detail.ArchivedVersions)
                .OrderByDescending(version => ParseVersion(version.Version) ?? new Version())
                .ThenByDescending(version => version.CreatedAt);

            foreach (var versionInfo in versions)
            {
                Version? candidateVersion = ParseVersion(versionInfo.Version);
                if (candidateVersion == null)
                    continue;

                if (plugin.AssemblyVersion != null && candidateVersion <= plugin.AssemblyVersion)
                    continue;

                string? requiresVersion = versionInfo.RequiresVersion ?? detail.RequiresVersion;
                if (IsCompatibleWithHostVersion(requiresVersion, hostVersion))
                    return versionInfo;
            }

            return null;
        }

        private static bool IsCompatibleWithHostVersion(string? requiresVersion, Version hostVersion)
        {
            if (string.IsNullOrWhiteSpace(requiresVersion))
                return true;

            return Version.TryParse(requiresVersion.Trim(), out var requiredVersion)
                ? hostVersion >= requiredVersion
                : true;
        }

        private static Version? ParseVersion(string? value)
        {
            return Version.TryParse(value, out var version) ? version : null;
        }

        public void Restart()
        {
            ConfigService.Instance.SaveConfigs();

            Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"), "-c MenuPluginManager");

            Application.Current.Shutdown();
        }

        public void OpenViewDllViersion()
        {
            new ViewDllVersionsWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public string SearchName { get => _SearchName; set { _SearchName = value; OnPropertyChanged(); }}
        private string _SearchName;
        public async void DownloadPackage()
        {
            Version version;
            var client = MarketplaceClient.GetInstance();

            // Try marketplace API first
            try
            {
                string? versionString = await client.GetLatestVersionAsync(SearchName);
                version = !string.IsNullOrWhiteSpace(versionString) ? new Version(versionString.Trim()) : new Version();
            }
            catch
            {
                version = new Version();
            }

            // Fallback to legacy LATEST_RELEASE if API failed
            if (version == new Version())
            {
                string LatestReleaseUrl = MarketplaceConfig.BuildLegacyPluginUrl($"{SearchName}/LATEST_RELEASE");
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient();
                    var byteArray = System.Text.Encoding.ASCII.GetBytes(DownloadFileConfig.Instance.Authorization);
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                    string versionString = await httpClient.GetStringAsync(LatestReleaseUrl);
                    version = new Version(versionString.Trim());
                }
                catch
                {
                    version = new Version();
                }
            }

            if (version == new Version())
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(UI.Properties.Resources.ProjectNotFound, SearchName));
                return;
            }
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(UI.Properties.Resources.FoundProjectDownloadConfirm, SearchName, $"{ColorVision.UI.Properties.Resources.Version}{version}"), "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

                    // Check if file already exists, skip redundant download
                    string? existingFile = MarketplaceClient.GetExistingFileIfValid(downloadDir, SearchName, version.ToString(), null);
                    if (existingFile != null)
                    {
                        log.Info($"Plugin {SearchName} v{version} already downloaded at {existingFile}, applying directly.");
                        PluginUpdater.UpdatePlugin(existingFile);
                        return;
                    }

                    string url = client.GetDownloadUrl(SearchName, version.ToString());
                    string expectedFileName = $"{SearchName}-{version}.cvxp";

                    DownloadWindow.ShowInstance();
                    Aria2cDownloadManager.GetInstance().AddDownload(url, downloadDir, DownloadFileConfig.Instance.Authorization, task =>
                    {
                        if (task.Status == DownloadStatus.Completed)
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                PluginUpdater.UpdatePlugin(task.SavePath);
                            });
                        }
                        else
                        {
                            log.Error($"DownloadPackage failed for {SearchName}: {task.ErrorMessage}");
                        }
                    }, expectedFileName);
                };
            });
        }


        public static void InstallPackage()
        {
            // 打开文件选择对话框
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Plugin Packages (*.cvxp;*.zip)|*.cvxp;*.zip|Plugin Package (*.cvxp)|*.cvxp|Zip Archive (*.zip)|*.zip|All Files (*.*)|*.*",
                FilterIndex = 1,
                Title = "Select a Update file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedZipPath = openFileDialog.FileName;
                PluginUpdater.UpdatePlugin(selectedZipPath);
            }
        }
    }
}
