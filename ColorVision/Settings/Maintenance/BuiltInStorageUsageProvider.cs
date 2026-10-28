using ColorVision.Solution.MultiImageViewer;
using ColorVision.ImageEditor.Cie;
using ColorVision.UI;
using ColorVision.UI.Maintenance;
using log4net;
using log4net.Appender;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ColorVision.Settings.Maintenance;

public sealed class BuiltInStorageUsageProvider : IStorageUsageProvider
{
    private readonly Dictionary<string, List<StorageUsageRule>> _rules = new(StringComparer.Ordinal);
    private readonly List<string> _temporaryIssues = [];

    public void PrepareStorageScan()
    {
        _rules.Clear();
        _temporaryIssues.Clear();
        // Count active files too; cleanup protection and retention remain in the cleanup provider.
        string temporaryRoot = Path.GetTempPath();
        foreach (string pattern in new[] { "ColorVision_Diagnostics_*.zip", "ColorVision_Screenshot_*.png", "ColorVision_ProcessDump_*.dmp", "ColorVision_ProcessDump_*.txt" })
            Add("temp", new(temporaryRoot, pattern));
        try
        {
            foreach (string prefix in StorageMaintenanceCatalog.UpdateDirectoryPrefixes)
                foreach (string directory in Directory.EnumerateDirectories(temporaryRoot, prefix + "*", SearchOption.TopDirectoryOnly))
                    if (StorageMaintenanceCatalog.IsKnownUpdateDirectoryName(Path.GetFileName(directory))) Add("temp", new(directory, Recursive: true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _temporaryIssues.Add($"{temporaryRoot}: {ex.Message}"); }
        AddPackages("Application/Full", ["ColorVision-*.exe"]);
        AddPackages("Application/Incremental", ["ColorVision-Update-*.cvx"]);
        AddPackages("Plugins", ["*.cvxp", "*.zip"]);
        AddPackages("Tools", ["*.exe", "*.msi", "*.zip", "*.7z"]);
        string cieRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ColorVision", "ImageEditor", "CieCache");
        foreach (var kind in Enum.GetValues<CieDiagramKind>())
            Add("cie-cache", new(cieRoot, $"{kind}_v*.png", IncludeFile: path => StorageMaintenanceCatalog.IsCieCacheName(Path.GetFileName(path), kind)));
        foreach (var appender in LogManager.GetAllRepositories().SelectMany(repository => repository.GetAppenders()).OfType<RollingFileAppender>())
        {
            if (string.IsNullOrWhiteSpace(appender.File)) continue;
            string active = Path.GetFullPath(appender.File);
            string? directory = Path.GetDirectoryName(active);
            if (directory == null) continue;
            string activeName = Path.GetFileName(active);
            Add("logs", new(directory, IncludeFile: path => string.Equals(path, active, StringComparison.OrdinalIgnoreCase)
                || StorageMaintenanceCatalog.IsRollingArchiveName(Path.GetFileName(path), activeName, appender.DatePattern, appender.StaticLogFileName, appender.PreserveLogFileNameExtension)));
        }
        Add("thumbnails", new(ThumbnailCacheManager.DirectoryPath, Recursive: true));

        void AddPackages(string child, string[] patterns)
        {
            foreach (string pattern in patterns) Add("packages", new(Path.Combine(Environments.DirPackageCache, child), pattern));
        }
    }

    private void Add(string id, StorageUsageRule rule)
    {
        if (!_rules.TryGetValue(id, out var rules)) _rules[id] = rules = [];
        rules.Add(rule);
    }

    public IEnumerable<StorageUsageCategory> GetStorageCategories() =>
    [
        new Category(this, "logs", MaintenanceText.Logs, MaintenanceText.LogsDescription),
        new Category(this, "temp", MaintenanceText.Temporary, MaintenanceText.TemporaryDescription),
        new Category(this, "thumbnails", MaintenanceText.Thumbnails, MaintenanceText.ThumbnailsDescription),
        new Category(this, "cie-cache", MaintenanceText.Cie, MaintenanceText.CieDescription),
        new Category(this, "packages", MaintenanceText.Packages, MaintenanceText.PackagesDescription),
    ];

    private sealed class Category(BuiltInStorageUsageProvider owner, string id, string title, string description) : StorageUsageCategory
    {
        public override string Id => "Application.Storage." + id;
        public override string Title => title;
        public override string Description => description;
        public override string? ManagementCategoryId => id;
        public override IReadOnlyList<string> SourceDirectories => Rules.Select(rule => rule.RootPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        private IEnumerable<StorageUsageRule> Rules => owner._rules.TryGetValue(id, out var rules) ? rules : [];
        public override StorageUsageSnapshot Scan(CancellationToken cancellationToken)
        {
            var result = StorageUsageScanner.Scan(Rules, cancellationToken);
            return id == "temp" && owner._temporaryIssues.Count > 0
                ? result with { Issues = result.Issues.Concat(owner._temporaryIssues).Distinct().ToArray() } : result;
        }
    }
}
