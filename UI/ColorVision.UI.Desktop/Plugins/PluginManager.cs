using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Plugins;
using log4net;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Desktop.Plugins
{
    public class PluginWindowConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                 new ConfigSettingMetadata
                {
                    Order = 999,
                    BindingName =nameof(PluginWindowConfig.IsAutoUpdate),
                    Source = PluginWindowConfig.Instance,
                }
            };
        }
    }

  
    public class PluginWindowConfig : WindowConfig
    {
        public static PluginWindowConfig Instance => ConfigService.Instance.GetRequiredService<PluginWindowConfig>();

        /// <summary>
        /// 是否自动更新插件
        /// </summary>
        /// 
        [DisplayName("CheckPluginUpdates")]
        public bool IsAutoUpdate { get => _IsAutoUpdate; set { _IsAutoUpdate = value; OnPropertyChanged(); } }
        private bool _IsAutoUpdate = true;
    }


    public class PluginManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginManager));
        private static PluginManager _instance;
        private static readonly object _locker = new();
        public static PluginManager GetInstance() { lock (_locker) { _instance ??= new PluginManager(); return _instance; } }
        public ObservableCollection<PluginInfoVM> Plugins { get; private set; } = new ObservableCollection<PluginInfoVM>();
        public static PluginWindowConfig Config => PluginWindowConfig.Instance;
        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand OpenStoreCommand { get;  set; }
        public RelayCommand InstallPackageCommand { get; set; }
        public RelayCommand DownloadPackageCommand { get; set; }
        public RelayCommand OpenViewDllViersionCommand { get; set; }
        public RelayCommand RestartCommand { get; set; }

        // 在 PluginLoader 类中添加
        public RelayCommand UpdateAllCommand { get; set; }

        public int UpdateAvailableCount { get => _UpdateAvailableCount; set { _UpdateAvailableCount = value; OnPropertyChanged(); } }
        private int _UpdateAvailableCount;

        public PluginManager()
        {
            log.Info(UI.Properties.Resources.CheckingForAdditionalProjects);

            foreach (var item in UI.Plugins.PluginLoader.Config.Plugins)
            {
                if (item.Value.Manifest != null)
                {
                    PluginInfoVM info = new PluginInfoVM(item.Value);
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

            // Use batch version check via marketplace API for efficiency
            Task.Run(async () =>
            {
                try
                {
                    var client = MarketplaceClient.GetInstance();
                    var pluginIds = Plugins.Where(p => p.PluginInfo.Enabled && !string.IsNullOrEmpty(p.PackageName))
                                          .Select(p => p.PackageName!).ToList();
                    if (pluginIds.Count > 0)
                    {
                        var versions = await client.BatchVersionCheckAsync(pluginIds);
                        foreach (var plugin in Plugins)
                        {
                            if (plugin.PackageName != null && versions.TryGetValue(plugin.PackageName, out string? ver) && !string.IsNullOrEmpty(ver))
                            {
                                plugin.SetVersionFromBatchCheck(ver);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Debug($"Batch version check failed, individual checks will continue: {ex.Message}");
                }
            });
 
            OpenStoreCommand = new RelayCommand(a => OpenStore());
            InstallPackageCommand = new RelayCommand(a => InstallPackage());
            DownloadPackageCommand = new RelayCommand(a => DownloadPackage());
            EditConfigCommand = new RelayCommand(a => new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            OpenViewDllViersionCommand = new RelayCommand(a => OpenViewDllViersion());
            RestartCommand = new RelayCommand(a => Restart());
            UpdateAllCommand = new RelayCommand(a => UpdateAll());
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
                });
            }
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
                string LatestReleaseUrl = PluginLoaderrConfig.Instance.PluginUpdatePath + SearchName + "/LATEST_RELEASE";
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
                    });
                };
            });
        }


        public static void InstallPackage()
        {
            // 打开文件选择对话框
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Files (*.zip)|*.zip;plugin(*.cvxp)|*.cvxp",
                Title = "Select a Update file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedZipPath = openFileDialog.FileName;
                PluginUpdater.UpdatePlugin(selectedZipPath);
            }
        }
        public static void OpenStore()
        {
            PlatformHelper.Open(PluginLoaderrConfig.Instance.PluginUpdatePath);
        }
    }
}
