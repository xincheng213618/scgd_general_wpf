using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using log4net;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Plugins
{
    public class PluginWindowConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                 new ConfigSettingMetadata
                {
                    Name = Properties.Resources.CheckPluginUpdates,
                    Description = Properties.Resources.CheckPluginUpdates,
                    Order = 999,
                    Type = ConfigSettingType.Bool,
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

        private DownloadFile DownloadFile { get; set; }
        // 在 PluginLoader 类中添加
        public RelayCommand UpdateAllCommand { get; set; }

        public int UpdateAvailableCount { get => _UpdateAvailableCount; set { _UpdateAvailableCount = value; OnPropertyChanged(); } }
        private int _UpdateAvailableCount;

        public PluginManager()
        {
            log.Info(Properties.Resources.CheckingForAdditionalProjects);

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
 
            OpenStoreCommand = new RelayCommand(a => OpenStore());
            InstallPackageCommand = new RelayCommand(a => InstallPackage());
            DownloadPackageCommand = new RelayCommand(a => DownloadPackage());
            DownloadFile = new DownloadFile();
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
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                foreach (var plugin in pluginsToUpdate)
                {
                    try
                    {
                        // 异步调用，避免阻塞UI
                        Application.Current.Dispatcher.InvokeAsync(() => plugin.Update());
                    }
                    catch (Exception ex)
                    {
                        log.Error($"更新插件 {plugin.Name} 时发生错误: {ex.Message}", ex);
                    }
                }
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
        }

        public string SearchName { get => _SearchName; set { _SearchName = value; OnPropertyChanged(); }}
        private string _SearchName;
        public async void DownloadPackage()
        {
            string LatestReleaseUrl = PluginLoaderrConfig.Instance.PluginUpdatePath + SearchName + "/LATEST_RELEASE";
            DownloadFile.DownloadTile = Properties.Resources.Download + SearchName;
            Version version = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
            if (version == new Version())
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.ProjectNotFound, SearchName));
                return;
            }
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.FoundProjectDownloadConfirm, SearchName, $"{ColorVision.UI.Properties.Resources.Version}{version}"), "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\{SearchName}-{version}.cvxp";
                    string url = $"{PluginLoaderrConfig.Instance.PluginUpdatePath}{SearchName}/{SearchName}-{version}.cvxp";
                    WindowUpdate windowUpdate = new WindowUpdate(DownloadFile) { Owner =Application.Current.GetActiveWindow(), WindowStartupLocation =WindowStartupLocation.CenterOwner };
                    if (File.Exists(downloadPath))
                    {
                        File.Delete(downloadPath);
                    }
                    if (!File.Exists(downloadPath))
                    {
                        windowUpdate.Show();
                    }
                    Task.Run(async () =>
                    {
                        if (!File.Exists(downloadPath))
                        {
                            CancellationTokenSource _cancellationTokenSource = new();
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                windowUpdate.Show();
                            });
                            await DownloadFile.Download(url, downloadPath, _cancellationTokenSource.Token);
                        }
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            windowUpdate.Close();
                        });

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            PluginUpdater.UpdatePlugin( downloadPath);
                        });

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
