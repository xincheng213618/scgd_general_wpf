namespace ColorVision.UI.Maintenance;

/// <summary>Owner-declared storage accounting. Preparing captures configuration only; scanning is read-only.</summary>
public interface IStorageUsageProvider
{
    void PrepareStorageScan();
    IEnumerable<StorageUsageCategory> GetStorageCategories();
}

/// <summary>Optional contribution from an already configured runtime object, such as a preprocessing action.</summary>
public interface IStorageUsageContributor
{
    /// <summary>Capture owned locations only. These read-only rules never imply cleanup permission.</summary>
    IEnumerable<StorageUsageRule> GetStorageUsageRules();
}

public abstract class StorageUsageCategory
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }
    public virtual IReadOnlyList<string> SourceDirectories => [];
    /// <summary>The explicit cleanup category owned by the same module; null means location-only management.</summary>
    public virtual string? ManagementCategoryId => null;
    /// <summary>Optional owner UI entry. The owner must enforce authorization again when invoked.</summary>
    public virtual Action? OpenManager => null;
    public virtual bool IsProtected => false;
    public abstract StorageUsageSnapshot Scan(CancellationToken cancellationToken);
}

/// <summary>A module-owned allowlist for usage only. This rule never grants deletion authority.</summary>
public sealed record StorageUsageRule(string RootPath, string SearchPattern = "*", bool Recursive = false, Func<string, bool>? IncludeFile = null);

public sealed record StorageUsageSnapshot(long Bytes, int Count, IReadOnlyList<FileMaintenanceEntry> Files,
    IReadOnlyList<string> Issues, bool IsCancelled = false);
