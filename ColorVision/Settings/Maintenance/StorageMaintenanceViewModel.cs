using ColorVision.Common.MVVM;
using ColorVision.UI.Maintenance;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Settings.Maintenance;

public sealed record StorageCacheSnapshot(long Bytes, int Count, object? Token);
public sealed record StorageCacheCleanupResult(long DeletedBytes, int DeletedCount, int SkippedCount, string? Error, string? Notice = null);

public sealed class StorageCleanupItem : ViewModelBase
{
    public string Id { get; }
    public string Title { get; }
    public string Description { get; }
    private string _notice = string.Empty;
    public string Notice { get => _notice; internal set { _notice = value; OnPropertyChanged(); } }
    public bool HasRetention => Id is "logs" or "temp" or "packages";
    public IReadOnlyList<int> RetentionOptions { get; } = new[] { 7, 14, 30, 90, 180 };
    private int _retentionDays;
    public int RetentionDays
    {
        get => _retentionDays;
        set
        {
            if (!RetentionOptions.Contains(value) || _retentionDays == value) return;
            _retentionDays = value;
            Invalidate();
            OnPropertyChanged();
            Changed?.Invoke();
        }
    }
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); Changed?.Invoke(); } }
    public bool IsScanned { get; private set; }
    public long Bytes { get; private set; }
    public int Count { get; private set; }
    public string SizeText => IsScanned ? $"{FormatBytes(Bytes)} · {Count:N0} {MaintenanceText.Files}" : MaintenanceText.NotScanned;
    public bool CanClean => IsScanned && Count > 0;
    internal MaintenanceFileCleanupScanResult? FileScan { get; private set; }
    internal StorageCacheSnapshot? CacheScan { get; private set; }
    internal event Action? Changed;

    public StorageCleanupItem(string id, string title, string description, int retentionDays, bool selected)
    {
        Id = id; Title = title; Description = description;
        _retentionDays = RetentionOptions.Contains(retentionDays) ? retentionDays : 30;
        _isSelected = selected;
    }

    internal void SetScan(MaintenanceFileCleanupScanResult scan)
    {
        FileScan = scan; CacheScan = null;
        SetStatistics(scan.TotalBytes, scan.Files.Count, !scan.IsCancelled);
    }

    internal void SetScan(StorageCacheSnapshot scan)
    {
        CacheScan = scan; FileScan = null;
        SetStatistics(scan.Bytes, scan.Count, true);
    }

    public void Invalidate()
    {
        FileScan = null; CacheScan = null;
        SetStatistics(0, 0, false);
    }

    private void SetStatistics(long bytes, int count, bool scanned)
    {
        Bytes = bytes; Count = count; IsScanned = scanned;
        OnPropertyChanged(nameof(SizeText)); OnPropertyChanged(nameof(CanClean)); OnPropertyChanged(nameof(IsScanned));
        Changed?.Invoke();
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}

public sealed class StorageMaintenanceViewModel : ViewModelBase
{
    private static readonly SemaphoreSlim OperationGate = new(1, 1);
    private readonly Func<int, int, int, IEnumerable<MaintenanceFileCleanupRule>> _rulesFactory;
    private readonly Func<StorageCacheSnapshot> _scanThumbnails;
    private readonly Func<StorageCacheSnapshot, StorageCacheCleanupResult> _clearThumbnails;
    private readonly StorageMaintenanceConfig _config;
    private readonly Func<string, string>? _protectionNotice;
    private CancellationTokenSource? _cancellation;
    private bool _busy;
    private string _status = MaintenanceText.ScanFirst;
    public ObservableCollection<StorageCleanupItem> Items { get; } = new();
    public List<string> Issues { get; } = new();
    public bool IsBusy { get => _busy; private set { _busy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsIdle)); NotifySelection(); } }
    public bool IsIdle => !IsBusy;
    public bool CanCleanSelected => !IsBusy && Items.Any(item => item.IsSelected && item.CanClean);
    public bool HasIssues => Issues.Count > 0;
    public string Summary => $"{StorageCleanupItem.FormatBytes(Items.Where(item => item.IsSelected).Sum(item => item.Bytes))}";
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

    public StorageMaintenanceViewModel(StorageMaintenanceConfig config,
        Func<int, int, int, IEnumerable<MaintenanceFileCleanupRule>> rulesFactory,
        Func<StorageCacheSnapshot> scanThumbnails,
        Func<StorageCacheSnapshot, StorageCacheCleanupResult> clearThumbnails,
        Func<string, string>? protectionNotice = null)
    {
        _config = config; _rulesFactory = rulesFactory;
        _scanThumbnails = scanThumbnails; _clearThumbnails = clearThumbnails;
        _protectionNotice = protectionNotice;
        Items.Add(new("logs", MaintenanceText.Logs, MaintenanceText.LogsDescription, config.LogRetentionDays, true));
        Items.Add(new("temp", MaintenanceText.Temporary, MaintenanceText.TemporaryDescription, config.TemporaryRetentionDays, true));
        Items.Add(new("thumbnails", MaintenanceText.Thumbnails, MaintenanceText.ThumbnailsDescription, 7, true));
        Items.Add(new("cie-cache", MaintenanceText.Cie, MaintenanceText.CieDescription, 7, true));
        Items.Add(new("packages", MaintenanceText.Packages, MaintenanceText.PackagesDescription, config.PackageRetentionDays, false));
        foreach (var item in Items) item.Changed += OnItemChanged;
    }

    private void OnItemChanged()
    {
        _config.LogRetentionDays = Items[0].RetentionDays;
        _config.TemporaryRetentionDays = Items[1].RetentionDays;
        _config.PackageRetentionDays = Items[4].RetentionDays;
        NotifySelection();
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(CanCleanSelected)); OnPropertyChanged(nameof(Summary)); OnPropertyChanged(nameof(HasIssues));
    }

    public void Cancel() => _cancellation?.Cancel();

    public async Task ScanAsync()
    {
        if (IsBusy || !await OperationGate.WaitAsync(0)) return;
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsBusy = true; Issues.Clear(); Status = MaintenanceText.Scanning;
        foreach (var item in Items) item.Invalidate();
        try
        {
            // Capture application-owned rule paths on the UI thread; workers do not enumerate UI collections.
            var rules = _rulesFactory(Items[0].RetentionDays, Items[1].RetentionDays, Items[4].RetentionDays).ToArray();
            foreach (var item in Items)
            {
                if (cancellation.IsCancellationRequested) break;
                try
                {
                    item.Notice = _protectionNotice?.Invoke(item.Id) ?? string.Empty;
                    if (item.Id == "thumbnails")
                        item.SetScan(await Task.Run(_scanThumbnails, cancellation.Token));
                    else
                    {
                        var scan = await Task.Run(() => MaintenanceFileCleanup.Scan(rules.Where(rule => rule.Id == item.Id), cancellation.Token));
                        item.SetScan(scan);
                        Issues.AddRange(scan.Issues.Select(issue => $"{item.Title}: {issue.FullPath} — {issue.Message}"));
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { Issues.Add($"{item.Title}: {ex.Message}"); }
            }
            Status = cancellation.IsCancellationRequested ? MaintenanceText.Cancelled : MaintenanceText.ScanComplete;
        }
        catch (Exception ex) { Status = string.Format(MaintenanceText.Failed, ex.Message); Issues.Add(ex.Message); }
        finally { _cancellation = null; IsBusy = false; OperationGate.Release(); }
    }

    public async Task CleanupAsync(IReadOnlyList<StorageCleanupItem> confirmedItems)
    {
        if (IsBusy || confirmedItems.Count == 0 || !await OperationGate.WaitAsync(0)) return;
        // Only rows belonging to this model and still holding their confirmed scan may be executed.
        if (confirmedItems.Any(item => !Items.Contains(item) || !item.CanClean)) { OperationGate.Release(); return; }
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsBusy = true; Issues.Clear(); Status = MaintenanceText.Cleaning;
        long bytes = 0;
        int deleted = 0, skipped = 0, failed = 0;
        try
        {
            foreach (var item in confirmedItems)
            {
                if (cancellation.IsCancellationRequested) break;
                try
                {
                    if (item.CacheScan is { } cache)
                    {
                        var result = await Task.Run(() => _clearThumbnails(cache));
                        bytes += result.DeletedBytes; deleted += result.DeletedCount; skipped += result.SkippedCount;
                        if (result.Error != null) { failed++; Issues.Add($"{item.Title}: {result.Error}"); }
                        if (result.Notice != null) Issues.Add($"{item.Title}: {result.Notice}");
                    }
                    else if (item.FileScan is { } scan)
                    {
                        var result = await Task.Run(() => MaintenanceFileCleanup.Cleanup(scan, cancellation.Token));
                        bytes += result.DeletedBytes; deleted += result.DeletedFileCount;
                        skipped += result.SkippedFileCount; failed += result.FailedFileCount;
                        Issues.AddRange(result.Issues.Select(issue => $"{item.Title}: {issue.FullPath} — {issue.Message}"));
                    }
                }
                catch (Exception ex) { failed++; Issues.Add($"{item.Title}: {ex.Message}"); }
                item.Invalidate();
            }
            Status = string.Format(MaintenanceText.Result, StorageCleanupItem.FormatBytes(bytes), deleted, skipped, failed);
            if (cancellation.IsCancellationRequested) Status += " " + MaintenanceText.Cancelled;
        }
        finally { _cancellation = null; IsBusy = false; OperationGate.Release(); }
    }
}
