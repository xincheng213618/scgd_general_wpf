using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Plugins;
using log4net;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Desktop.Marketplace
{
    public enum MarketplaceVersionBadgeKind
    {
        Latest,
        Installed,
        Current,
        Archive,
    }

    public sealed class MarketplaceVersionBadgeItem
    {
        public required string Text { get; init; }
        public required MarketplaceVersionBadgeKind Kind { get; init; }
    }

    public sealed class MarketplaceDetailRow
    {
        public required string Label { get; init; }
        public required string Value { get; init; }
    }

    public sealed class MarketplaceVersionItemViewModel
    {
        public required MarketplacePluginVersionInfo VersionInfo { get; init; }
        public required string VersionText { get; init; }
        public required string SummaryText { get; init; }
        public string? ChangeLog { get; init; }
        public string? HashText { get; init; }
        public required string InstallText { get; init; }
        public List<MarketplaceVersionBadgeItem> Badges { get; init; } = new();
        public required AsyncRelayCommand InstallCommand { get; init; }
        public required AsyncRelayCommand DownloadCommand { get; init; }
        public bool HasChangeLog => !string.IsNullOrWhiteSpace(ChangeLog);
        public bool HasHash => !string.IsNullOrWhiteSpace(HashText);
    }

    public sealed class MarketplaceVersionGroup
    {
        public required string Title { get; init; }
        public List<MarketplaceVersionItemViewModel> Items { get; init; } = new();
    }

    /// <summary>
    /// A lightweight view model for displaying marketplace plugin details in the right panel
    /// when a marketplace (remote) plugin is selected that is not installed locally.
    /// </summary>
    public class MarketplaceDetailContext : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceDetailContext));
        private static readonly CompositeFormat RequiresFormat = CompositeFormat.Parse(Resources.MarketplaceRequiresFormat);
        private static readonly CompositeFormat InstalledVersionFormat = CompositeFormat.Parse(Resources.MarketplaceInstalledVersionFormat);
        private static readonly CompositeFormat UpdateVersionFormat = CompositeFormat.Parse(Resources.MarketplaceUpdateVersionFormat);
        private static readonly CompositeFormat DownloadCountFormat = CompositeFormat.Parse(Resources.MarketplaceDownloadCountFormat);
        private readonly PluginInfoVM? _installedPlugin;
        private readonly MarketplacePackageDownloadService _packageDownloadService = MarketplacePackageDownloadService.GetInstance();
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
        public List<MarketplaceDetailRow> DetailRows { get; private set; } = new();
        public List<MarketplaceVersionGroup> VersionGroups { get; private set; } = new();

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
            : string.Format(null, RequiresFormat, RequiresVersion);

        public string HeaderRightSecondary => string.Join("  ·  ", new[]
        {
            string.IsNullOrWhiteSpace(Author) ? null : Author,
            string.Format(null, DownloadCountFormat, TotalDownloads),
        }.Where(item => !string.IsNullOrWhiteSpace(item))!);

        public string InstalledBadgeText => IsInstalled
            ? (string.IsNullOrWhiteSpace(InstalledVersion) ? Resources.MarketplaceStatusInstalled : string.Format(null, InstalledVersionFormat, InstalledVersion))
            : string.Empty;

        public Visibility InstalledBadgeVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility UpdateBadgeVisibility => HasUpdate ? Visibility.Visible : Visibility.Collapsed;
        public string UpdateBadgeText => HasUpdate && !string.IsNullOrWhiteSpace(LatestVersion)
            ? string.Format(null, UpdateVersionFormat, LatestVersion)
            : Resources.MarketplaceUpdateAvailable;

        public Visibility OpenLocalPathVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ExtractPluginVisibility => IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PrimaryActionVisibility => !string.IsNullOrWhiteSpace(LatestVersion) && (!IsInstalled || HasUpdate)
            ? Visibility.Visible
            : Visibility.Collapsed;
        public Visibility DownloadLatestVisibility => string.IsNullOrWhiteSpace(LatestVersion) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility OpenProjectUrlVisibility => string.IsNullOrWhiteSpace(Url) ? Visibility.Collapsed : Visibility.Visible;

        public List<MarketplacePluginVersionInfo> CurrentVersions => OrderVersions(Versions.Where(v => !string.Equals(v.Source, "archive", StringComparison.OrdinalIgnoreCase)));
        public List<MarketplacePluginVersionInfo> ArchivedVersionsOrdered => OrderVersions(ArchivedVersions);
    public bool HasVersions => VersionGroups.Count > 0;

        public bool IsInstalled => _installedPlugin != null;
        public bool HasUpdate => CompareVersions(LatestVersion, InstalledVersion) > 0;
        public string PrimaryActionText => HasUpdate ? Resources.Update : Resources.Install;

        public ICommand InstallCommand { get; set; }
        public ICommand DownloadCommand { get; set; }
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
            DetailRows = BuildDetailRows();
            VersionGroups = BuildVersionGroups();

            InstallCommand = new AsyncRelayCommand(_ => InstallLatestFromMarketplaceAsync(), _ => !string.IsNullOrWhiteSpace(LatestVersion), logger: log);
            DownloadCommand = new AsyncRelayCommand(_ => DownloadLatestFromMarketplaceAsync(), _ => !string.IsNullOrWhiteSpace(LatestVersion), logger: log);
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

        private List<MarketplaceDetailRow> BuildDetailRows()
        {
            var rows = new List<MarketplaceDetailRow>();

            AddDetailRow(rows, Resources.MarketplacePluginId, PackageName);
            AddDetailRow(rows, Resources.MarketplaceLatestVersion, LatestVersion);
            AddDetailRow(rows, Resources.MarketplaceInstalledVersion, InstalledVersion);
            AddDetailRow(rows, Resources.MarketplaceStatus, IsInstalled
                ? (HasUpdate ? Resources.MarketplaceStatusInstalledUpdate : Resources.MarketplaceStatusInstalled)
                : Resources.MarketplaceStatusMarketplaceOnly);
            AddDetailRow(rows, Resources.MarketplaceRequires, RequiresVersion);
            AddDetailRow(rows, Resources.MarketplaceAuthor, Author);
            AddDetailRow(rows, Resources.MarketplaceCategory, Category);
            AddDetailRow(rows, Resources.MarketplaceDownloads, TotalDownloads.ToString());
            AddDetailRow(rows, Resources.MarketplaceUpdated, UpdatedAt == default ? string.Empty : UpdatedAt.ToString("yyyy/MM/dd HH:mm:ss"));
            AddDetailRow(rows, Resources.MarketplaceProjectUrl, Url);
            AddDetailRow(rows, Resources.MarketplaceDescription, Description);
            AddDetailRow(rows, Resources.MarketplaceCurrentPackages, CurrentPackageCount.ToString());
            AddDetailRow(rows, Resources.MarketplaceHistoryPackages, HistoricalPackageCount.ToString());

            return rows;
        }

        private List<MarketplaceVersionGroup> BuildVersionGroups()
        {
            var groups = new List<MarketplaceVersionGroup>();

            List<MarketplaceVersionItemViewModel> currentItems = BuildVersionItems(CurrentVersions);
            if (currentItems.Count > 0)
            {
                groups.Add(new MarketplaceVersionGroup
                {
                    Title = Resources.MarketplaceCurrentVersions,
                    Items = currentItems,
                });
            }

            List<MarketplaceVersionItemViewModel> archiveItems = BuildVersionItems(ArchivedVersionsOrdered);
            if (archiveItems.Count > 0)
            {
                groups.Add(new MarketplaceVersionGroup
                {
                    Title = Resources.MarketplaceArchivedVersions,
                    Items = archiveItems,
                });
            }

            return groups;
        }

        private List<MarketplaceVersionItemViewModel> BuildVersionItems(IEnumerable<MarketplacePluginVersionInfo> versions)
        {
            return versions.Select(CreateVersionItem).ToList();
        }

        private MarketplaceVersionItemViewModel CreateVersionItem(MarketplacePluginVersionInfo version)
        {
            return new MarketplaceVersionItemViewModel
            {
                VersionInfo = version,
                VersionText = $"v{version.Version}",
                SummaryText = BuildVersionSummary(version),
                ChangeLog = string.IsNullOrWhiteSpace(version.ChangeLog) ? null : version.ChangeLog,
                HashText = string.IsNullOrWhiteSpace(version.FileHash)
                    ? null
                    : $"{Resources.MarketplaceVersionSha256Label}: {version.FileHash}",
                InstallText = GetInstallLabel(version),
                Badges = BuildBadges(version),
                InstallCommand = new AsyncRelayCommand(_ => InstallVersionAsync(version), logger: log),
                DownloadCommand = new AsyncRelayCommand(_ => DownloadVersionAsync(version), logger: log),
            };
        }

        private List<MarketplaceVersionBadgeItem> BuildBadges(MarketplacePluginVersionInfo version)
        {
            var badges = new List<MarketplaceVersionBadgeItem>();

            if (string.Equals(version.Version, LatestVersion, StringComparison.OrdinalIgnoreCase))
            {
                badges.Add(new MarketplaceVersionBadgeItem
                {
                    Text = Resources.MarketplaceLatest,
                    Kind = MarketplaceVersionBadgeKind.Latest,
                });
            }

            if (string.Equals(version.Version, InstalledVersion, StringComparison.OrdinalIgnoreCase))
            {
                badges.Add(new MarketplaceVersionBadgeItem
                {
                    Text = Resources.Installed,
                    Kind = MarketplaceVersionBadgeKind.Installed,
                });
            }

            if (!string.IsNullOrWhiteSpace(version.Source))
            {
                bool isArchive = version.Source.Equals("archive", StringComparison.OrdinalIgnoreCase);
                badges.Add(new MarketplaceVersionBadgeItem
                {
                    Text = isArchive ? Resources.MarketplaceArchive : Resources.MarketplaceCurrent,
                    Kind = isArchive ? MarketplaceVersionBadgeKind.Archive : MarketplaceVersionBadgeKind.Current,
                });
            }

            return badges;
        }

        private static string BuildVersionSummary(MarketplacePluginVersionInfo version)
        {
            return string.Join("  ·  ", new[]
            {
                version.FileSize > 0 ? FormatFileSize(version.FileSize) : null,
                version.CreatedAt == default ? null : version.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
                string.IsNullOrWhiteSpace(version.RequiresVersion) ? null : string.Format(null, RequiresFormat, version.RequiresVersion),
                version.DownloadCount > 0 ? string.Format(null, DownloadCountFormat, version.DownloadCount) : null,
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

        private static List<MarketplacePluginVersionInfo> OrderVersions(IEnumerable<MarketplacePluginVersionInfo> versions)
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

        private async Task InstallLatestFromMarketplaceAsync()
        {
            MarketplacePluginVersionInfo? versionInfo = FindLatestVersionInfo();

            if (versionInfo == null)
                return;

            await InstallVersionAsync(versionInfo);
        }

        private async Task DownloadLatestFromMarketplaceAsync()
        {
            MarketplacePluginVersionInfo? versionInfo = FindLatestVersionInfo();

            if (versionInfo == null)
                return;

            await DownloadVersionAsync(versionInfo);
        }

        private Task InstallVersionAsync(MarketplacePluginVersionInfo versionInfo)
        {
            return ExecutePackageActionAsync(versionInfo, applyAfterDownload: true);
        }

        private Task DownloadVersionAsync(MarketplacePluginVersionInfo versionInfo)
        {
            return ExecutePackageActionAsync(versionInfo, applyAfterDownload: false);
        }

        private async Task ExecutePackageActionAsync(MarketplacePluginVersionInfo versionInfo, bool applyAfterDownload)
        {
            if (string.IsNullOrEmpty(PackageName) || string.IsNullOrEmpty(versionInfo.Version))
                return;

            MarketplacePackageRequest request = new()
            {
                PluginId = PackageName,
                Version = versionInfo.Version,
                ExpectedHash = versionInfo.FileHash,
            };

            if (applyAfterDownload)
            {
                if (!ConfirmInstall(versionInfo))
                    return;

                await _packageDownloadService.InstallPackageAsync(request);
            }
            else
            {
                await _packageDownloadService.OpenDownloadedPackageFolderAsync(request);
            }
        }

        private bool ConfirmInstall(MarketplacePluginVersionInfo versionInfo)
        {
            string title = string.Join(" ", new[]
            {
                Name ?? PackageName,
                string.IsNullOrWhiteSpace(versionInfo.Version) ? null : $"v{versionInfo.Version}",
            }.Where(item => !string.IsNullOrWhiteSpace(item)));

            return MessageBox.Show(
                Application.Current.GetActiveWindow(),
                Properties.Resources.ConfirmUpdate,
                string.IsNullOrWhiteSpace(title) ? "ColorVision" : title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        private MarketplacePluginVersionInfo? FindLatestVersionInfo()
        {
            return CurrentVersions.FirstOrDefault(v => string.Equals(v.Version, LatestVersion, StringComparison.OrdinalIgnoreCase))
                ?? Versions.FirstOrDefault(v => string.Equals(v.Version, LatestVersion, StringComparison.OrdinalIgnoreCase))
                ?? ArchivedVersions.FirstOrDefault(v => string.Equals(v.Version, LatestVersion, StringComparison.OrdinalIgnoreCase))
                ?? CurrentVersions.FirstOrDefault()
                ?? Versions.FirstOrDefault()
                ?? ArchivedVersions.FirstOrDefault();
        }

        private static void AddDetailRow(List<MarketplaceDetailRow> rows, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            rows.Add(new MarketplaceDetailRow
            {
                Label = label,
                Value = value,
            });
        }
    }
}
