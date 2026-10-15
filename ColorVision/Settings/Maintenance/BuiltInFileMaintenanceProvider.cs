using ColorVision.Solution.MultiImageViewer;
using ColorVision.UI;
using ColorVision.UI.Maintenance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Settings.Maintenance;

public sealed class BuiltInFileMaintenanceProvider : IFileMaintenanceProvider
{
    private readonly StorageMaintenanceConfig _config;
    private readonly Func<int, int, int, IEnumerable<MaintenanceFileCleanupRule>> _rulesFactory;
    private readonly Func<StorageCacheSnapshot> _scanThumbnails;
    private readonly Func<StorageCacheSnapshot, StorageCacheCleanupResult> _clearThumbnails;
    private readonly Func<string, string>? _protectionNotice;
    private MaintenanceFileCleanupRule[] _rules = [];

    public BuiltInFileMaintenanceProvider() : this(ConfigService.Instance.GetRequiredService<StorageMaintenanceConfig>(),
        StorageMaintenanceCatalog.CreateRules, ScanThumbnails, ClearThumbnails, StorageMaintenanceCatalog.GetProtectionNotice) { }

    internal BuiltInFileMaintenanceProvider(StorageMaintenanceConfig config,
        Func<int, int, int, IEnumerable<MaintenanceFileCleanupRule>> rulesFactory,
        Func<StorageCacheSnapshot> scanThumbnails, Func<StorageCacheSnapshot, StorageCacheCleanupResult> clearThumbnails,
        Func<string, string>? protectionNotice)
    {
        _config = config; _rulesFactory = rulesFactory; _scanThumbnails = scanThumbnails;
        _clearThumbnails = clearThumbnails; _protectionNotice = protectionNotice;
    }

    public void PrepareScan() => _rules = _rulesFactory(_config.LogRetentionDays, _config.TemporaryRetentionDays, _config.PackageRetentionDays).ToArray();

    public IEnumerable<FileMaintenanceCategory> GetCategories() => new FileMaintenanceCategory[]
    {
        new Category(this, "logs", MaintenanceText.Logs, MaintenanceText.LogsDescription, 30, true),
        new Category(this, "temp", MaintenanceText.Temporary, MaintenanceText.TemporaryDescription, 7, true),
        new Category(this, "thumbnails", MaintenanceText.Thumbnails, MaintenanceText.ThumbnailsDescription, null, true),
        new Category(this, "cie-cache", MaintenanceText.Cie, MaintenanceText.CieDescription, null, true),
        new Category(this, "packages", MaintenanceText.Packages, MaintenanceText.PackagesDescription, 30, false),
    };

    private static StorageCacheSnapshot ScanThumbnails()
    {
        var snapshot = ThumbnailCacheManager.ScanCacheForMaintenance();
        if (!string.IsNullOrEmpty(snapshot.Error)) throw new InvalidOperationException(snapshot.Error);
        return new(snapshot.SizeBytes, snapshot.EntryCount, snapshot);
    }

    private static StorageCacheCleanupResult ClearThumbnails(StorageCacheSnapshot snapshot)
    {
        if (snapshot.Token is not ThumbnailCacheMaintenanceSnapshot token) return new(0, 0, snapshot.Count, MaintenanceText.ScanFirst);
        var result = ThumbnailCacheManager.ClearCacheForMaintenance(token);
        return new(result.ReleasedBytes, result.DeletedEntryCount, result.RequiresRescan ? snapshot.Count : 0,
            result.Succeeded || result.RequiresRescan ? null : result.Message,
            result.Succeeded || result.RequiresRescan ? result.Message : null);
    }

    private sealed class Category(BuiltInFileMaintenanceProvider owner, string id, string title, string description, int? retention, bool selected) : FileMaintenanceCategory
    {
        public override string Id => id;
        public override string Title => title;
        public override string Description => description;
        public override int? DefaultRetentionDays => retention;
        public override bool DefaultSelected => selected;
        public override IReadOnlyList<string> SourceDirectories => id switch
        {
            "thumbnails" => [ThumbnailCacheManager.DirectoryPath],
            "packages" => [Environments.DirPackageCache],
            _ => owner._rules.Where(rule => rule.Id == id).Select(rule => rule.RootPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        };
        public override string ProtectionNotice => owner._protectionNotice?.Invoke(id) ?? string.Empty;

        public override FileMaintenanceSnapshot Scan(int retentionDays, FileMaintenanceSnapshot? previous, CancellationToken cancellationToken)
        {
            if (id != "thumbnails")
                return FileMaintenanceSnapshot.FromFiles(MaintenanceFileCleanup.Scan(owner._rules.Where(rule => rule.Id == id), cancellationToken, previous?.Token as MaintenanceFileCleanupScanResult));
            cancellationToken.ThrowIfCancellationRequested();
            StorageCacheSnapshot snapshot = owner._scanThumbnails();
            cancellationToken.ThrowIfCancellationRequested();
            return new(snapshot.Bytes, snapshot.Count, snapshot, [], []);
        }

        public override FileMaintenanceCleanupResult Cleanup(FileMaintenanceSnapshot confirmed, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (confirmed.Token is MaintenanceFileCleanupScanResult files)
                return FileMaintenanceCleanupResult.FromFiles(MaintenanceFileCleanup.Cleanup(files, cancellationToken));
            if (confirmed.Token is not StorageCacheSnapshot thumbnail) throw new InvalidOperationException(MaintenanceText.ScanFirst);
            StorageCacheCleanupResult result = owner._clearThumbnails(thumbnail);
            return new(result.DeletedBytes, result.DeletedCount, result.SkippedCount, result.Error == null ? 0 : 1,
                new[] { result.Error, result.Notice }.OfType<string>().ToArray());
        }
    }
}
