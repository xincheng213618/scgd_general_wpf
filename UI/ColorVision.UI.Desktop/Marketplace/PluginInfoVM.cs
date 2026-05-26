#pragma warning disable CS8604
#pragma warning disable CA1822,CA1863
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Plugins;
using log4net;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Desktop.Marketplace
{
    public class PluginInfoVM:ViewModelBase
    {  
        private static readonly ILog log = LogManager.GetLogger(typeof(PluginInfoVM));
        private readonly MarketplacePackageDownloadService _packageDownloadService = MarketplacePackageDownloadService.GetInstance();

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

        public string HeaderMetaText => string.Join("  ·  ", new[]
        {
            PackageName,
            AssemblyVersion?.ToString(),
            AssemblyBuildDate?.ToString("yyyy/MM/dd HH:mm:ss"),
        }.Where(item => !string.IsNullOrWhiteSpace(item))!);

        public string? HeaderRightPrimary => PluginInfo?.Manifest?.DllName;
        public string HeaderRightSecondary => string.Empty;

        public string InstalledBadgeText => string.IsNullOrWhiteSpace(AssemblyVersion?.ToString())
            ? Resources.Installed
            : string.Format(Resources.MarketplaceInstalledVersionFormat, AssemblyVersion);
        public Visibility InstalledBadgeVisibility => Visibility.Visible;
        public Visibility UpdateBadgeVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        public string UpdateBadgeText => HasUpdate && LastVersion != null
            ? string.Format(Resources.MarketplaceUpdateVersionFormat, LastVersion)
            : string.Empty;

        public Visibility OpenLocalPathVisibility => Visibility.Visible;
        public Visibility ExtractPluginVisibility => Visibility.Visible;
        public Visibility InstallVisibility => Visibility.Collapsed;
        public Visibility PrimaryActionVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DownloadLatestVisibility => Visibility.Collapsed;
        public Visibility OpenProjectUrlVisibility => Visibility.Collapsed;
        public string PrimaryActionText => Resources.Update;

        public PluginInfo PluginInfo { get; set; }


        public Version LastVersion
        {
            get => _LastVersion;
            set
            {
                _LastVersion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUpdate));
                OnPropertyChanged(nameof(UpdateBadgeVisibility));
                OnPropertyChanged(nameof(UpdateBadgeText));
                OnPropertyChanged(nameof(PrimaryActionVisibility));
            }
        }
        private Version _LastVersion;

        public bool HasUpdate => LastVersion != null && AssemblyVersion != null && LastVersion > AssemblyVersion;

        public RelayCommand DeleteCommand { get; set; }
        public ICommand UpdateCommand { get; set; }
        public RelayCommand OpenLocalPathCommand { get; set; }
        public RelayCommand ExtractPluginCommand { get; set; }
        public ICommand? DownloadCommand => null;
        public ICommand? InstallCommand => null;
        public ICommand? OpenProjectUrlCommand => null;

        public PluginInfoVM(PluginInfo pluginInfo, bool skipIndividualCheck = false)
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
            UpdateCommand = new AsyncRelayCommand(_ => UpdateAsync(), logger: log);
            OpenLocalPathCommand = new RelayCommand(a => OpenLocalPath());
            ExtractPluginCommand = new RelayCommand(a => ExtractPlugin());
            ContextMenu = new ContextMenu();

            if (PluginInfo.Enabled && !skipIndividualCheck)
            {
                Task.Run(CheckVersionAsync);
            }

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Delete, Command = ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.Update, Command = UpdateCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Resources.MarketplaceOpenLocalPath, Command = OpenLocalPathCommand });
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

        public async Task CheckVersionAsync()
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
            string LatestReleaseUrl = MarketplaceConfig.BuildLegacyPluginUrl($"{PackageName}/LATEST_RELEASE");
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


        public async Task UpdateAsync()
        {
            if (!HasUpdate || LastVersion == null) return;

            if (MessageBox.Show(Application.Current.GetActiveWindow(), Properties.Resources.ConfirmUpdate, Name, MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            string? expectedHash = await _packageDownloadService.ResolveExpectedHashAsync(PackageName!, LastVersion.ToString());
            await _packageDownloadService.InstallPackageAsync(new MarketplacePackageRequest
            {
                PluginId = PackageName!,
                Version = LastVersion.ToString(),
                ExpectedHash = expectedHash,
            });
        }


        public void Delete()
        {
            if (MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.ConfirmDeletePlugin, Name),"ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.No) return;

            PluginUpdater.DeletePlugin(PackageName);
        }
    }
}
