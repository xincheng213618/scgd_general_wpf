#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI.Properties;
using log4net;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Plugins
{
    public class PluginInfoVM:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginInfoVM));

        public ContextMenu ContextMenu { get; set; }
        public string? PackageName { get; set; }
        public string Description { get; set; }

        public string? Name { get; set; }

        public ImageSource? Icon { get; set; }

        public Version? AssemblyVersion { get; set; }
        public DateTime? AssemblyBuildDate { get; set; }
        public string? AssemblyName { get; set; }
        public string? AssemblyPath { get; set; }
        public string? AssemblyCulture { get; set; }
        public string? AssemblyPublicKeyToken { get; set; }

        public PluginInfo PluginInfo { get; set; }


        public Version LastVersion { get => _LastVersion; set { _LastVersion = value; OnPropertyChanged(); } }
        private Version _LastVersion;

        public RelayCommand DeleteCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }
        public RelayCommand OpenLocalPathCommand { get; set; }

        DownloadFile DownloadFile { get; set; }
        public PluginInfoVM(PluginInfo pluginInfo)
        {
            PluginInfo = pluginInfo;
            Name = pluginInfo.Name;
            Description =pluginInfo.Description;
            PackageName = pluginInfo.Manifest.Id;
            AssemblyVersion = pluginInfo.AssemblyVersion;
            AssemblyBuildDate = pluginInfo.AssemblyBuildDate;

            Icon = pluginInfo.Icon ??= new BitmapImage(new Uri($"pack://application:,,,/ColorVision.Themes;component/Assets/Image/{(ThemeManager.Current.CurrentUITheme == Theme.Dark ? "ColorVision1.ico" : "ColorVision.ico")}"));

            DeleteCommand = new RelayCommand(a => Delete());
            UpdateCommand = new RelayCommand(a => Update());
            OpenLocalPathCommand = new RelayCommand(a => OpenLocalPath());
            ContextMenu = new ContextMenu();

            DownloadFile = new DownloadFile();
            DownloadFile.DownloadTile = Resources.Update + Name ;

            if (PluginInfo.Enabled)
            {
                Task.Run(() => CheckVersion());
            }

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Delete, Command = ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Update, Command = UpdateCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "OpenLocalPath", Command = OpenLocalPathCommand });



        }


        public void OpenLocalPath()
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", PackageName);
            log.Info($"OpenLocalPath:{localPath}");
            PlatformHelper.OpenFolder(localPath);
        }

        public async void CheckVersion()
        {
            string LatestReleaseUrl = PluginLoaderrConfig.Instance.PluginUpdatePath  + PackageName + "/LATEST_RELEASE";
            LastVersion = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
        }


        public async void Update()
        {
            string LatestReleaseUrl = PluginLoaderrConfig.Instance.PluginUpdatePath + PackageName + "/LATEST_RELEASE";
            Version version = await DownloadFile.GetLatestVersionNumber(LatestReleaseUrl);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.ConfirmUpdate, Name, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    string downloadPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + $"ColorVision\\{PackageName}-{version}.zip";
                    string url = $"{PluginLoaderrConfig.Instance.PluginUpdatePath}{PackageName}/{PackageName}-{version}.zip";
                    WindowUpdate windowUpdate = new WindowUpdate(DownloadFile) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
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
                            PluginUpdater.UpdatePlugin(downloadPath);
                        });
                    });

                };

            });
        }


        public void Delete()
        {
            if (MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.ConfirmDeletePlugin, Name), Resources.PluginManagerWindow, MessageBoxButton.YesNo) == MessageBoxResult.No) return;

            PluginUpdater.DeletePlugin(PackageName);
        }
    }
}
