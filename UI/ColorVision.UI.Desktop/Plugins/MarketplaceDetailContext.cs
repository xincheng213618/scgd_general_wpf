using ColorVision.Common.MVVM;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Plugins;
using log4net;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Desktop.Plugins
{
    /// <summary>
    /// A lightweight view model for displaying marketplace plugin details in the right panel
    /// when a marketplace (remote) plugin is selected that is not installed locally.
    /// </summary>
    public class MarketplaceDetailContext : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceDetailContext));

        public string? Name { get; set; }
        public string? PackageName { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Category { get; set; }
        public string? LatestVersion { get; set; }
        public long TotalDownloads { get; set; }
        public string? Readme { get; set; }
        public List<MarketplacePluginVersionInfo> Versions { get; set; } = new();
        public string? IconUrl { get; set; }

        /// <summary>
        /// Not a locally installed plugin — PluginInfo is null.
        /// </summary>
        public PluginInfo? PluginInfo => null;
        public Version? AssemblyVersion => null;
#pragma warning disable CA1822 // Data-binding properties must be instance members
        public DateTime? AssemblyBuildDate => null;
#pragma warning restore CA1822

        public bool IsInstalled => PluginManager.GetInstance().Plugins.Any(p => p.PackageName == PackageName);

        public RelayCommand InstallCommand { get; set; }

        public MarketplaceDetailContext(MarketplacePluginDetail detail)
        {
            Name = detail.Name;
            PackageName = detail.PluginId;
            Description = detail.Description;
            Author = detail.Author;
            Category = detail.Category;
            LatestVersion = detail.Versions.FirstOrDefault()?.Version;
            TotalDownloads = detail.TotalDownloads;
            Readme = detail.Readme;
            Versions = detail.Versions;
            IconUrl = detail.IconUrl;

            InstallCommand = new RelayCommand(a => InstallFromMarketplace());
        }

        private void InstallFromMarketplace()
        {
            if (string.IsNullOrEmpty(PackageName) || string.IsNullOrEmpty(LatestVersion))
                return;

            string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

            // Check if file already exists
            string? existingFile = MarketplaceClient.GetExistingFileIfValid(downloadDir, PackageName, LatestVersion, null);
            if (existingFile != null)
            {
                log.Info($"Plugin {PackageName} v{LatestVersion} already exists, applying directly.");
                Application.Current?.Dispatcher.Invoke(() => PluginUpdater.UpdatePlugin(existingFile));
                return;
            }

            var client = MarketplaceClient.GetInstance();

            // Try to get hash for verification
            string? expectedHash = Versions.FirstOrDefault(v => v.Version == LatestVersion)?.FileHash;

            string url = client.GetDownloadUrl(PackageName, LatestVersion);

            DownloadWindow.ShowInstance();
            Aria2cDownloadManager.GetInstance().AddDownload(url, downloadDir, DownloadFileConfig.Instance.Authorization, task =>
            {
                if (task.Status == DownloadStatus.Completed)
                {
                    // Verify hash if available
                    if (!string.IsNullOrEmpty(expectedHash) && !MarketplaceClient.VerifyFileHash(task.SavePath, expectedHash))
                    {
                        log.Error($"Hash mismatch for {PackageName} v{LatestVersion}! Expected: {expectedHash}");
                        Application.Current?.Dispatcher.Invoke(() =>
                            MessageBox.Show(Application.Current.GetActiveWindow(),
                                $"下载的文件哈希校验失败，文件可能已损坏。\nExpected: {expectedHash}",
                                "Hash Verification Failed", MessageBoxButton.OK, MessageBoxImage.Error));
                        return;
                    }

                    Application.Current?.Dispatcher.Invoke(() => PluginUpdater.UpdatePlugin(task.SavePath));
                }
                else
                {
                    log.Error($"Install from marketplace failed for {PackageName}: {task.ErrorMessage}");
                }
            });
        }
    }
}
