using ColorVision.Common.MVVM;
using ColorVision.UI.Maintenance;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
    public bool HasRetention { get; }
    public IReadOnlyList<int> RetentionOptions { get; } = new[] { 7, 14, 30, 90, 180, 0 };
    public bool HasRetentionPeriod => RetentionDays > 0;
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
            OnPropertyChanged(nameof(HasRetentionPeriod));
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
    internal FileMaintenanceSnapshot? Snapshot { get; private set; }
    internal FileMaintenanceSnapshot? PreviousSnapshot { get; private set; }
    public IReadOnlyList<FileMaintenanceEntry> Files => Snapshot?.Files ?? Array.Empty<FileMaintenanceEntry>();
    public IReadOnlyList<string> SourceDirectories { get; private set; } = Array.Empty<string>();
    public bool CanOpenLocation => SourceDirectories.Any(Directory.Exists);
    internal event Action? Changed;

    public StorageCleanupItem(string id, string title, string description, int retentionDays, bool selected, bool hasRetention = true)
    {
        Id = id; Title = title; Description = description;
        _retentionDays = RetentionOptions.Contains(retentionDays) ? retentionDays : 30;
        _isSelected = selected;
        HasRetention = hasRetention;
    }

    internal void SetScan(FileMaintenanceSnapshot snapshot)
    {
        if (snapshot.IsCancelled) { Invalidate(); return; }
        Snapshot = PreviousSnapshot = snapshot;
        FileScan = snapshot.Token as MaintenanceFileCleanupScanResult;
        CacheScan = snapshot.Token as StorageCacheSnapshot;
        SetStatistics(snapshot.Bytes, snapshot.Count, true);
    }

    internal void SetSourceDirectories(IEnumerable<string> directories)
    {
        SourceDirectories = directories.Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        OnPropertyChanged(nameof(SourceDirectories));
        OnPropertyChanged(nameof(CanOpenLocation));
    }

    public void Invalidate()
    {
        Snapshot = null;
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
    private readonly IFileMaintenanceProvider[] _providers;
    private readonly Dictionary<string, FileMaintenanceCategory> _categories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IFileMaintenanceProvider> _categoryOwners = new(StringComparer.Ordinal);
    private readonly List<string> _discoveryIssues = new();
    private readonly StorageMaintenanceConfig _config;
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
        : this(config, [new BuiltInFileMaintenanceProvider(config, rulesFactory, scanThumbnails, clearThumbnails, protectionNotice)]) { }

    public StorageMaintenanceViewModel(StorageMaintenanceConfig config, IEnumerable<IFileMaintenanceProvider> providers)
    {
        _config = config; _providers = providers.ToArray();
        foreach (var provider in _providers)
        {
            try
            {
                foreach (FileMaintenanceCategory category in provider.GetCategories())
                {
                    if (string.IsNullOrWhiteSpace(category.Id) || _categories.ContainsKey(category.Id))
                        throw new InvalidOperationException($"Duplicate or empty file maintenance category: {category.Id}");
                    _categories.Add(category.Id, category);
                    _categoryOwners.Add(category.Id, provider);
                    int retention = category.Id switch
                    {
                        "logs" => config.LogRetentionDays,
                        "temp" => config.TemporaryRetentionDays,
                        "packages" => config.PackageRetentionDays,
                        _ => config.FileRetentionDays.TryGetValue(category.Id, out int value) ? value : category.DefaultRetentionDays ?? 30,
                    };
                    Items.Add(new(category.Id, category.Title, category.Description, retention, category.DefaultSelected, category.DefaultRetentionDays.HasValue));
                }
            }
            catch (Exception ex) { _discoveryIssues.Add($"{provider.GetType().Name}: {ex.Message}"); }
        }
        foreach (var item in Items) item.Changed += OnItemChanged;
        Issues.AddRange(_discoveryIssues);
    }

    private void OnItemChanged()
    {
        foreach (var item in Items.Where(item => item.HasRetention))
        {
            switch (item.Id)
            {
                case "logs": _config.LogRetentionDays = item.RetentionDays; break;
                case "temp": _config.TemporaryRetentionDays = item.RetentionDays; break;
                case "packages": _config.PackageRetentionDays = item.RetentionDays; break;
                default: _config.FileRetentionDays[item.Id] = item.RetentionDays; break;
            }
        }
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
        Issues.AddRange(_discoveryIssues);
        try
        {
            // Capture application-owned rule paths on the UI thread; workers do not enumerate UI collections.
            var failedProviders = new HashSet<IFileMaintenanceProvider>();
            foreach (var provider in _providers)
            {
                try { provider.PrepareScan(); }
                catch (Exception ex) { failedProviders.Add(provider); Issues.Add($"{provider.GetType().Name}: {ex.Message}"); }
            }
            foreach (var item in Items)
            {
                if (cancellation.IsCancellationRequested) break;
                item.Invalidate();
                if (failedProviders.Contains(_categoryOwners[item.Id])) continue;
                Status = $"{MaintenanceText.Scanning} {item.Title}";
                NotifySelection();
                try
                {
                    FileMaintenanceCategory category = _categories[item.Id];
                    item.SetSourceDirectories(category.SourceDirectories);
                    item.Notice = category.ProtectionNotice;
                    var scan = await Task.Run(() => category.Scan(item.RetentionDays, item.PreviousSnapshot, cancellation.Token), cancellation.Token);
                    if (cancellation.IsCancellationRequested || scan.IsCancelled) break;
                    item.SetScan(scan);
                    Issues.AddRange(scan.Issues.Select(issue => $"{item.Title}: {issue}"));
                    NotifySelection();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { Issues.Add($"{item.Title}: {ex.Message}"); }
            }
            Status = cancellation.IsCancellationRequested ? MaintenanceText.Cancelled : Issues.Count > 0
                ? string.Format(MaintenanceText.Failed, Issues[0]) : MaintenanceText.ScanComplete;
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
                    if (item.Snapshot is { } scan)
                    {
                        var result = await Task.Run(() => _categories[item.Id].Cleanup(scan, cancellation.Token));
                        bytes += result.DeletedBytes; deleted += result.DeletedCount;
                        skipped += result.SkippedCount; failed += result.FailedCount;
                        Issues.AddRange(result.Issues.Select(issue => $"{item.Title}: {issue}"));
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
