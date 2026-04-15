using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Plugins;
using log4net;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Desktop.Marketplace
{
    /// <summary>
    /// A lightweight view model for displaying marketplace plugin details in the right panel
    /// when a marketplace (remote) plugin is selected that is not installed locally.
    /// </summary>
    public class MarketplaceDetailContext : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceDetailContext));
        private readonly PluginInfoVM? _installedPlugin;
        private ImageSource? _icon = MarketplaceClient.GetDefaultPluginIcon();

        public string? Name { get; set; }
        public string? PackageName { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Category { get; set; }
        public string? LatestVersion { get; set; }
        public string? RequiresVersion { get; set; }
        public long TotalDownloads { get; set; }
        public string? Readme { get; set; }
        public string? ChangeLog { get; set; }
        public List<MarketplacePluginVersionInfo> Versions { get; set; } = new();
        public List<MarketplacePluginVersionInfo> ArchivedVersions { get; set; } = new();
        public string? IconUrl { get; set; }
        public string? Url { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int CurrentPackageCount { get; set; }
        public int HistoricalPackageCount { get; set; }

        public ImageSource? Icon
        {
            get => _icon;
            private set
            {
                _icon = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Not a locally installed plugin — PluginInfo is null.
        /// </summary>
        public PluginInfo? PluginInfo => _installedPlugin?.PluginInfo;
        public Version? AssemblyVersion => Version.TryParse(LatestVersion, out var version) ? version : null;
        public DateTime? AssemblyBuildDate => UpdatedAt == default ? null : UpdatedAt;
        public string? InstalledVersion => _installedPlugin?.AssemblyVersion?.ToString();
        public bool HasLocalPlugin => _installedPlugin != null;

        public string HeaderMetaText => string.Join("  ·  ", new[]
        {
            string.IsNullOrWhiteSpace(PackageName) ? null : PackageName,
            string.IsNullOrWhiteSpace(LatestVersion) ? null : $"v{LatestVersion}",
            UpdatedAt == default ? null : UpdatedAt.ToString("yyyy/MM/dd HH:mm:ss"),
        }.Where(item => !string.IsNullOrWhiteSpace(item))!);

        public string HeaderRightPrimary => string.IsNullOrWhiteSpace(RequiresVersion)
            ? (string.IsNullOrWhiteSpace(Category) ? string.Empty : Category)
            : $"Requires {RequiresVersion}";

        public string HeaderRightSecondary => string.Join("  ·  ", new[]
        {
            string.IsNullOrWhiteSpace(Author) ? null : Author,
            TotalDownloads > 0 ? $"⬇ {TotalDownloads}" : "0 download",
        }.Where(item => !string.IsNullOrWhiteSpace(item))!);

        public string InstalledBadgeText => IsInstalled
            ? (string.IsNullOrWhiteSpace(InstalledVersion) ? "Installed" : $"Installed v{InstalledVersion}")
            : string.Empty;

        public Visibility InstalledBadgeVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UpdateBadgeVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        public string UpdateBadgeText => HasUpdate && !string.IsNullOrWhiteSpace(LatestVersion)
            ? $"Update v{LatestVersion}"
            : "Update available";

        public Visibility OpenLocalPathVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ExtractPluginVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PrimaryActionVisibility => !string.IsNullOrWhiteSpace(LatestVersion) && (!IsInstalled || HasUpdate)
            ? Visibility.Visible
            : Visibility.Collapsed;
        public Visibility DownloadLatestVisibility => string.IsNullOrWhiteSpace(LatestVersion) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility OpenProjectUrlVisibility => string.IsNullOrWhiteSpace(Url) ? Visibility.Collapsed : Visibility.Visible;

        public IEnumerable<MarketplacePluginVersionInfo> CurrentVersions => OrderVersions(Versions.Where(v => !string.Equals(v.Source, "archive", StringComparison.OrdinalIgnoreCase)));
        public IEnumerable<MarketplacePluginVersionInfo> ArchivedVersionsOrdered => OrderVersions(ArchivedVersions);
        public bool HasVersions => Versions.Count > 0 || ArchivedVersions.Count > 0;

        public bool IsInstalled => _installedPlugin != null;
        public bool HasUpdate => CompareVersions(LatestVersion, InstalledVersion) > 0;
        public string PrimaryActionText => HasUpdate ? Resources.Update : Resources.Install;

        public RelayCommand InstallCommand { get; set; }
        public RelayCommand DownloadCommand { get; set; }
        public RelayCommand OpenProjectUrlCommand { get; set; }
        public ICommand? OpenLocalPathCommand => _installedPlugin?.OpenLocalPathCommand;
        public ICommand? ExtractPluginCommand => _installedPlugin?.ExtractPluginCommand;

        public MarketplaceDetailContext(MarketplacePluginDetail detail, PluginInfoVM? installedPlugin = null)
        {
            _installedPlugin = installedPlugin;
            Name = detail.Name;
            PackageName = detail.PluginId;
            Description = detail.Description;
            Author = detail.Author;
            Category = detail.Category;
            LatestVersion = detail.LatestVersion ?? detail.Versions.FirstOrDefault()?.Version;
            RequiresVersion = detail.RequiresVersion ?? detail.Versions.FirstOrDefault()?.RequiresVersion;
            TotalDownloads = detail.TotalDownloads;
            Readme = detail.Readme;
            ChangeLog = detail.Changelog ?? detail.Versions.FirstOrDefault()?.ChangeLog;
            Versions = detail.Versions;
            ArchivedVersions = detail.ArchivedVersions;
            IconUrl = detail.IconUrl;
            Url = detail.Url;
            UpdatedAt = detail.UpdatedAt;
            CurrentPackageCount = detail.CurrentPackageCount > 0 ? detail.CurrentPackageCount : Versions.Count(v => !string.Equals(v.Source, "archive", StringComparison.OrdinalIgnoreCase));
            HistoricalPackageCount = detail.HistoricalPackageCount > 0 ? detail.HistoricalPackageCount : ArchivedVersions.Count;

            InstallCommand = new RelayCommand(a => InstallLatestFromMarketplace(), a => !string.IsNullOrWhiteSpace(LatestVersion));
            DownloadCommand = new RelayCommand(a => DownloadLatestFromMarketplace(), a => !string.IsNullOrWhiteSpace(LatestVersion));
            OpenProjectUrlCommand = new RelayCommand(a => OpenProjectUrl(), a => !string.IsNullOrWhiteSpace(Url));
        }

        public async Task InitializeAsync()
        {
            Icon = await MarketplaceClient.GetPluginIconAsync(IconUrl);
        }

        private void OpenProjectUrl()
        {
            if (string.IsNullOrWhiteSpace(Url))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Url,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                log.Error($"OpenProjectUrl failed for {PackageName}: {ex.Message}");
            }
        }

        public void PopulateDetailInfo(StackPanel detailInfo, ListView dependentsListView)
        {
            detailInfo.Children.Clear();
            dependentsListView.ItemsSource = null;

            void AddLine(string label, string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return;

                var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 3, 5, 3) };
                panel.Children.Add(new TextBlock
                {
                    Text = label + ": ",
                    FontWeight = FontWeights.SemiBold,
                    Width = 120,
                });
                panel.Children.Add(new TextBlock
                {
                    Text = value,
                    TextWrapping = TextWrapping.Wrap,
                });
                detailInfo.Children.Add(panel);
            }

            void AddVersionGroup(string title, IEnumerable<MarketplacePluginVersionInfo> versions)
            {
                var list = versions.ToList();
                if (list.Count == 0)
                    return;

                detailInfo.Children.Add(new TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(5, 10, 5, 5),
                });

                foreach (var version in list)
                {
                    detailInfo.Children.Add(CreateVersionCard(version));
                }
            }

            AddLine("Plugin ID", PackageName);
            AddLine("Latest Version", LatestVersion);
            AddLine("Installed Version", InstalledVersion);
            AddLine("Status", IsInstalled ? (HasUpdate ? "Installed · update available" : "Installed") : "Marketplace only");
            AddLine("Requires", RequiresVersion);
            AddLine("Author", Author);
            AddLine("Category", Category);
            AddLine("Downloads", TotalDownloads.ToString());
            AddLine("Updated", UpdatedAt == default ? string.Empty : UpdatedAt.ToString("yyyy/MM/dd HH:mm:ss"));
            AddLine("Project URL", Url);
            AddLine("Description", Description);
            AddLine("Current Packages", CurrentPackageCount.ToString());
            AddLine("History Packages", HistoricalPackageCount.ToString());

            AddVersionGroup("Current Versions", CurrentVersions);
            AddVersionGroup("Archived Versions", ArchivedVersionsOrdered);
        }

        private Border CreateVersionCard(MarketplacePluginVersionInfo version)
        {
            var card = new Border
            {
                Margin = new Thickness(12, 4, 5, 8),
                Padding = new Thickness(10),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gainsboro,
                Background = Brushes.Transparent,
            };

            var content = new StackPanel();
            card.Child = content;

            var header = new DockPanel();
            content.Children.Add(header);

            var title = new TextBlock
            {
                Text = $"v{version.Version}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };
            DockPanel.SetDock(title, Dock.Left);
            header.Children.Add(title);

            var badges = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            DockPanel.SetDock(badges, Dock.Right);
            header.Children.Add(badges);

            if (string.Equals(version.Version, LatestVersion, StringComparison.OrdinalIgnoreCase))
                badges.Children.Add(CreateBadge("Latest", Brushes.ForestGreen));
            if (string.Equals(version.Version, InstalledVersion, StringComparison.OrdinalIgnoreCase))
                badges.Children.Add(CreateBadge("Installed", Brushes.SteelBlue));
            if (!string.IsNullOrWhiteSpace(version.Source))
                badges.Children.Add(CreateBadge(version.Source.Equals("archive", StringComparison.OrdinalIgnoreCase) ? "Archive" : "Current", Brushes.DimGray));

            content.Children.Add(new TextBlock
            {
                Text = BuildVersionSummary(version),
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
            });

            if (!string.IsNullOrWhiteSpace(version.ChangeLog))
            {
                content.Children.Add(new TextBlock
                {
                    Text = version.ChangeLog,
                    Margin = new Thickness(0, 6, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 90,
                });
            }

            var actions = new WrapPanel
            {
                Margin = new Thickness(0, 8, 0, 0),
            };
            actions.Children.Add(CreateActionButton(GetInstallLabel(version), () => InstallVersion(version)));
            actions.Children.Add(CreateActionButton(Resources.Download, () => DownloadVersion(version)));
            content.Children.Add(actions);

            if (!string.IsNullOrWhiteSpace(version.FileHash))
            {
                content.Children.Add(new TextBlock
                {
                    Text = $"SHA256: {version.FileHash}",
                    Margin = new Thickness(0, 8, 0, 0),
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            return card;
        }

        private static Border CreateBadge(string text, Brush background)
        {
            return new Border
            {
                Background = background,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(4, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                }
            };
        }

        private static Button CreateActionButton(string text, Action action)
        {
            return new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(10, 4, 10, 4),
                MinWidth = 84,
                Command = new RelayCommand(_ => action()),
            };
        }

        private string BuildVersionSummary(MarketplacePluginVersionInfo version)
        {
            return string.Join("  ·  ", new[]
            {
                string.IsNullOrWhiteSpace(version.Version) ? null : $"Version {version.Version}",
                version.FileSize > 0 ? FormatFileSize(version.FileSize) : null,
                version.CreatedAt == default ? null : version.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
                string.IsNullOrWhiteSpace(version.RequiresVersion) ? null : $"Requires {version.RequiresVersion}",
                version.DownloadCount > 0 ? $"⬇ {version.DownloadCount}" : null,
            }.Where(item => !string.IsNullOrWhiteSpace(item))!);
        }

        private string GetInstallLabel(MarketplacePluginVersionInfo version)
        {
            if (string.Equals(version.Version, InstalledVersion, StringComparison.OrdinalIgnoreCase))
                return Resources.Install;
            if (string.Equals(version.Version, LatestVersion, StringComparison.OrdinalIgnoreCase) && HasUpdate)
                return Resources.Update;
            return Resources.Install;
        }

        private static IEnumerable<MarketplacePluginVersionInfo> OrderVersions(IEnumerable<MarketplacePluginVersionInfo> versions)
        {
            return versions
                .OrderByDescending(version => ParseVersion(version.Version) ?? new Version())
                .ThenByDescending(version => version.CreatedAt)
                .ToList();
        }

        private static Version? ParseVersion(string? value)
        {
            return Version.TryParse(value, out var version) ? version : null;
        }

        private static int CompareVersions(string? left, string? right)
        {
            var leftVersion = ParseVersion(left);
            var rightVersion = ParseVersion(right);
            if (leftVersion != null && rightVersion != null)
                return leftVersion.CompareTo(rightVersion);
            if (leftVersion != null)
                return 1;
            if (rightVersion != null)
                return -1;
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatFileSize(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB"];
            double size = bytes;
            int index = 0;
            while (size >= 1024 && index < units.Length - 1)
            {
                size /= 1024;
                index++;
            }
            return $"{size:0.##} {units[index]}";
        }

        private void InstallLatestFromMarketplace()
        {
            var versionInfo = CurrentVersions.FirstOrDefault(v => string.Equals(v.Version, LatestVersion, StringComparison.OrdinalIgnoreCase))
                ?? Versions.FirstOrDefault(v => string.Equals(v.Version, LatestVersion, StringComparison.OrdinalIgnoreCase))
                ?? ArchivedVersions.FirstOrDefault(v => string.Equals(v.Version, LatestVersion, StringComparison.OrdinalIgnoreCase));

            if (versionInfo == null)
                return;

            InstallVersion(versionInfo);
        }

        private void DownloadLatestFromMarketplace()
        {
            var versionInfo = CurrentVersions.FirstOrDefault(v => string.Equals(v.Version, LatestVersion, StringComparison.OrdinalIgnoreCase))
                ?? Versions.FirstOrDefault(v => string.Equals(v.Version, LatestVersion, StringComparison.OrdinalIgnoreCase))
                ?? ArchivedVersions.FirstOrDefault(v => string.Equals(v.Version, LatestVersion, StringComparison.OrdinalIgnoreCase));

            if (versionInfo == null)
                return;

            DownloadVersion(versionInfo);
        }

        private void InstallVersion(MarketplacePluginVersionInfo versionInfo)
        {
            DownloadVersion(versionInfo, applyAfterDownload: true);
        }

        private void DownloadVersion(MarketplacePluginVersionInfo versionInfo, bool applyAfterDownload = false)
        {
            if (string.IsNullOrEmpty(PackageName) || string.IsNullOrEmpty(versionInfo.Version))
                return;

            string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

            // Check if file already exists
            string? expectedHash = versionInfo.FileHash;
            string? existingFile = MarketplaceClient.GetExistingFileIfValid(downloadDir, PackageName, versionInfo.Version, expectedHash);
            if (existingFile != null)
            {
                log.Info($"Plugin {PackageName} v{versionInfo.Version} already exists, using cached file.");
                if (applyAfterDownload)
                {
                    Application.Current?.Dispatcher.Invoke(() => PluginUpdater.UpdatePlugin(existingFile));
                }
                else
                {
                    PlatformHelper.OpenFolder(Path.GetDirectoryName(existingFile));
                }
                return;
            }

            var client = MarketplaceClient.GetInstance();
            string url = client.GetDownloadUrl(PackageName, versionInfo.Version);
            string expectedFileName = $"{PackageName}-{versionInfo.Version}.cvxp";

            DownloadWindow.ShowInstance();
            Aria2cDownloadManager.GetInstance().AddDownload(url, downloadDir, DownloadFileConfig.Instance.Authorization, task =>
            {
                if (task.Status == DownloadStatus.Completed)
                {
                    // Verify hash if available
                    if (!string.IsNullOrEmpty(expectedHash) && !MarketplaceClient.VerifyFileHash(task.SavePath, expectedHash))
                    {
                        log.Error($"Hash mismatch for {PackageName} v{versionInfo.Version}! Expected: {expectedHash}");
                        Application.Current?.Dispatcher.Invoke(() =>
                            MessageBox.Show(Application.Current.GetActiveWindow(),
                                $"下载的文件哈希校验失败，文件可能已损坏。\nExpected: {expectedHash}",
                                "Hash Verification Failed", MessageBoxButton.OK, MessageBoxImage.Error));
                        return;
                    }

                    if (applyAfterDownload)
                    {
                        Application.Current?.Dispatcher.Invoke(() => PluginUpdater.UpdatePlugin(task.SavePath));
                    }
                    else
                    {
                        PlatformHelper.OpenFolder(Path.GetDirectoryName(task.SavePath));
                    }
                }
                else
                {
                    log.Error($"Marketplace package download failed for {PackageName} v{versionInfo.Version}: {task.ErrorMessage}");
                }
            }, expectedFileName);
        }
    }
}
