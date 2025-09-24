#pragma warning disable CS8604
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
                    Name ="检测插件更新",
                    Description =  "检测插件更新",
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


    public class PluginManagerV:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginManagerV));
        private static PluginManagerV _instance;
        private static readonly object _locker = new();
        public static PluginManagerV GetInstance() { lock (_locker) { _instance ??= new PluginManagerV(); return _instance; } }
        public ObservableCollection<PluginInfoVM> Plugins { get; private set; } = new ObservableCollection<PluginInfoVM>();
        public static PluginWindowConfig Config => PluginWindowConfig.Instance;
        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand OpenStoreCommand { get;  set; }
        public RelayCommand InstallPackageCommand { get; set; }
        public RelayCommand DownloadPackageCommand { get; set; }
        public RelayCommand OpenViewDllViersionCommand { get; set; }
        public RelayCommand RestartCommand { get; set; }

        private DownloadFile DownloadFile { get; set; }
        // 在 PluginManager 类中添加
        public RelayCommand UpdateAllCommand { get; set; }

        public PluginManagerV()
        {
            log.Info("正在检索是否存在附加项目");

            foreach (var item in UI.Plugins.PluginManager.Config.Plugins)
            {
                if (item.Value.Manifest != null)
                {
                    PluginInfoVM info = new PluginInfoVM(item.Value);
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
            foreach (var plugin in Plugins)
            {
                // 可根据实际需求判断是否已启用
                if (plugin.PluginInfo.Enabled)
                {

                    // 建议异步调用，避免阻塞UI
                    Application.Current.Dispatcher.InvokeAsync(() => plugin.Update());
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
            new ViewDllVersionsWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public string SearchName { get => _SearchName; set { _SearchName = value; OnPropertyChanged(); }}
        private string _SearchName;
        public async void DownloadPackage()
        {
            string LatestReleaseUrl = PluginManagerConfig.Instance.PluginUpdatePath + SearchName + "/LATEST_RELEASE";
            DownloadFile.DownloadTile = "下载" + SearchName;
            Version version = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
            if (version == new Version())
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"找到不到项目{SearchName}");
                return;
            }
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), $"找到项目{SearchName}，{ColorVision.UI.Properties.Resources.Version}{version}，是否下载", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\{SearchName}-{version}.zip";
                    string url = $"{PluginManagerConfig.Instance.PluginUpdatePath}{SearchName}/{SearchName}-{version}.zip";
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
                Filter = "ZIP Files (*.zip)|*.zip",
                Title = "Select a ZIP file"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedZipPath = openFileDialog.FileName;
                PluginUpdater.UpdatePlugin(selectedZipPath);
            }
        }
        public static void OpenStore()
        {
            PlatformHelper.Open(PluginManagerConfig.Instance.PluginUpdatePath);
        }
    }
}
