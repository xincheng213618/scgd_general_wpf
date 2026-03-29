#pragma warning disable CS8604
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Plugins;
using log4net;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Desktop.Plugins
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


        public Version LastVersion { get => _LastVersion; set { _LastVersion = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUpdate)); } }
        private Version _LastVersion;

        public bool HasUpdate => LastVersion != null && AssemblyVersion != null && LastVersion > AssemblyVersion;

        public RelayCommand DeleteCommand { get; set; }
        public RelayCommand UpdateCommand { get; set; }
        public RelayCommand OpenLocalPathCommand { get; set; }
        public RelayCommand ExtractPluginCommand { get; set; }

        public PluginInfoVM(PluginInfo pluginInfo)
        {
            PluginInfo = pluginInfo;
            Name = pluginInfo.Name;
            Description =pluginInfo.Description;
            PackageName = pluginInfo.Manifest.Id;
            AssemblyVersion = pluginInfo.AssemblyVersion;
            AssemblyBuildDate = pluginInfo.AssemblyBuildDate;

            // Icon is now lazy-loaded from PluginInfo, with fallback to default
            Icon = pluginInfo.Icon ?? new BitmapImage(new Uri($"pack://application:,,,/ColorVision.Themes;component/Assets/Image/{(ThemeManager.Current.CurrentUITheme == Theme.Dark ? "ColorVision1.ico" : "ColorVision.ico")}"));

            DeleteCommand = new RelayCommand(a => Delete());
            UpdateCommand = new RelayCommand(a => Update());
            OpenLocalPathCommand = new RelayCommand(a => OpenLocalPath());
            ExtractPluginCommand = new RelayCommand(a => ExtractPlugin());
            ContextMenu = new ContextMenu();

            if (PluginInfo.Enabled)
            {
                Task.Run(() => CheckVersion());
            }

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Delete, Command = ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Update, Command = UpdateCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "OpenLocalPath", Command = OpenLocalPathCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.ExtractPlugin, Command = ExtractPluginCommand });



        }

        /// <summary>
        /// Set version from an external batch check result (avoids redundant individual API calls).
        /// </summary>
        public void SetVersionFromBatchCheck(string versionString)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(versionString))
                    LastVersion = new Version(versionString.Trim());
            }
            catch (Exception ex)
            {
                log.Debug($"SetVersionFromBatchCheck failed for {PackageName}: {ex.Message}");
            }
        }


        public void OpenLocalPath()
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", PackageName);
            log.Info($"OpenLocalPath:{localPath}");
            PlatformHelper.OpenFolder(localPath);
        }

        public void ExtractPlugin()
        {
            // Use FolderBrowserDialog to select output folder
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = Resources.SelectExtractFolder,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string outputFolder = dialog.SelectedPath;

                // Check if the folder is empty or create new subfolder with plugin name
                if (Directory.Exists(outputFolder) && Directory.GetFileSystemEntries(outputFolder).Length > 0)
                {
                    // Folder is not empty, create a subfolder with plugin name
                    outputFolder = Path.Combine(outputFolder, PackageName ?? Name ?? "Plugin");
                }

                bool success = PluginExtractor.ExtractPlugin(PluginInfo, outputFolder);

                if (success)
                {
                    MessageBox.Show(
                        string.Format(Resources.PluginExtractSuccess, outputFolder),
                        Resources.ExtractPlugin,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Open the output folder
                    PlatformHelper.OpenFolder(outputFolder);
                }
                else
                {
                    MessageBox.Show(
                        Resources.PluginExtractFailed,
                        Resources.ExtractPlugin,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        public async void CheckVersion()
        {
            // Try marketplace API first
            try
            {
                var client = MarketplaceClient.GetInstance();
                string? version = await client.GetLatestVersionAsync(PackageName);
                if (!string.IsNullOrWhiteSpace(version))
                {
                    LastVersion = new Version(version.Trim());
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Debug($"Marketplace API check failed for {PackageName}, falling back to legacy: {ex.Message}");
            }

            // Fallback to legacy LATEST_RELEASE file
            string LatestReleaseUrl = PluginLoaderrConfig.Instance.PluginUpdatePath  + PackageName + "/LATEST_RELEASE";
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                var byteArray = System.Text.Encoding.ASCII.GetBytes(DownloadFileConfig.Instance.Authorization);
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                string versionString = await httpClient.GetStringAsync(LatestReleaseUrl);
                if (!string.IsNullOrWhiteSpace(versionString))
                    LastVersion = new Version(versionString.Trim());
            }
            catch (Exception ex)
            {
                log.Debug($"CheckVersion failed for {PackageName}: {ex.Message}");
            }
        }


        public async void Update()
        {
            if (!HasUpdate || LastVersion == null) return;

            if (MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.ConfirmUpdate, Name, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

            // Try to get expected hash from marketplace API for verification
            string? expectedHash = null;
            try
            {
                var client = MarketplaceClient.GetInstance();
                var detail = await client.GetPluginDetailAsync(PackageName);
                expectedHash = detail?.Versions?.FirstOrDefault(v => v.Version == LastVersion.ToString())?.FileHash;
            }
            catch (Exception ex)
            {
                log.Debug($"Could not fetch hash for {PackageName}: {ex.Message}");
            }

            // Check if the file already exists with matching hash (skip redundant download)
            string? existingFile = MarketplaceClient.GetExistingFileIfValid(downloadDir, PackageName, LastVersion.ToString(), expectedHash);
            if (existingFile != null)
            {
                log.Info($"Plugin {PackageName} v{LastVersion} already downloaded at {existingFile}, applying directly.");
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    PluginUpdater.UpdatePlugin(existingFile);
                });
                return;
            }

            // Use marketplace API URL with fallback to legacy URL
            string url = MarketplaceClient.GetInstance().GetDownloadUrl(PackageName, LastVersion.ToString());

            DownloadWindow.ShowInstance();
            Aria2cDownloadManager.GetInstance().AddDownload(url, downloadDir, DownloadFileConfig.Instance.Authorization, task =>
            {
                if (task.Status == DownloadStatus.Completed)
                {
                    // Verify hash if available
                    if (!string.IsNullOrEmpty(expectedHash) && !MarketplaceClient.VerifyFileHash(task.SavePath, expectedHash))
                    {
                        log.Error($"Hash mismatch for {PackageName} v{LastVersion}! Expected: {expectedHash}, Actual: {MarketplaceClient.ComputeFileHash(task.SavePath)}");
                        Application.Current?.Dispatcher.Invoke(() =>
                            MessageBox.Show(Application.Current.GetActiveWindow(),
                                $"下载的文件哈希校验失败，文件可能已损坏。\nExpected: {expectedHash}",
                                "Hash Verification Failed", MessageBoxButton.OK, MessageBoxImage.Error));
                        return;
                    }

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        PluginUpdater.UpdatePlugin(task.SavePath);
                    });
                }
                else
                {
                    log.Error($"Plugin download failed for {PackageName}: {task.ErrorMessage}");
                }
            });
        }


        public void Delete()
        {
            if (MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.ConfirmDeletePlugin, Name),"ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.No) return;

            PluginUpdater.DeletePlugin(PackageName);
        }
    }
}
