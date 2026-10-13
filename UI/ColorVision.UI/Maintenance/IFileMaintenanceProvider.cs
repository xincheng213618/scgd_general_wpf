namespace ColorVision.UI.Maintenance;

/// <summary>Discovered like other UI extensions. Each owner supplies its own file allowlist and cleanup.</summary>
public interface IFileMaintenanceProvider
{
    IEnumerable<FileMaintenanceCategory> GetCategories();
    // Capture UI-owned paths and settings before background scanning starts. Do not scan or create storage here.
    void PrepareScan();
}

public abstract class FileMaintenanceCategory
{
    public abstract string Id { get; }
    public abstract string Title { get; }
    public abstract string Description { get; }
    public virtual int? DefaultRetentionDays => null;
    public virtual bool DefaultSelected => false;
    public virtual IReadOnlyList<string> SourceDirectories => [];
    public virtual string ProtectionNotice => string.Empty;
    public abstract FileMaintenanceSnapshot Scan(int retentionDays, FileMaintenanceSnapshot? previous, CancellationToken cancellationToken);
    public abstract FileMaintenanceCleanupResult Cleanup(FileMaintenanceSnapshot confirmed, CancellationToken cancellationToken);
}

public sealed record FileMaintenanceEntry(string FullPath, long Length, DateTime LastWriteTimeUtc);

/// <summary>The token belongs to the provider; incomplete scans must never become deletion proposals.</summary>
public sealed record FileMaintenanceSnapshot(long Bytes, int Count, object Token,
    IReadOnlyList<FileMaintenanceEntry> Files, IReadOnlyList<string> Issues, bool IsCancelled = false, int ReusedFileCount = 0)
{
    public static FileMaintenanceSnapshot FromFiles(MaintenanceFileCleanupScanResult scan) => new(
        scan.TotalBytes, scan.Files.Count, scan,
        scan.Files.Select(file => new FileMaintenanceEntry(file.FullPath, file.Length, file.LastWriteTimeUtc)).ToArray(),
        scan.Issues.Select(issue => $"{issue.FullPath} — {issue.Message}").ToArray(), scan.IsCancelled, scan.ReusedFileCount);
}

public sealed record FileMaintenanceCleanupResult(long DeletedBytes, int DeletedCount, int SkippedCount, int FailedCount, IReadOnlyList<string> Issues)
{
    public static FileMaintenanceCleanupResult FromFiles(MaintenanceFileCleanupResult result) => new(
        result.DeletedBytes, result.DeletedFileCount, result.SkippedFileCount, result.FailedFileCount,
        result.Issues.Select(issue => $"{issue.FullPath} — {issue.Message}").ToArray());
}
