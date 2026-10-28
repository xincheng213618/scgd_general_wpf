using ColorVision.Common.MVVM;
using ColorVision.UI.Maintenance;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace ColorVision.Settings.Maintenance;

public sealed class StorageUsageItem : ViewModelBase
{
    internal StorageUsageCategory Category { get; }
    public string Id => Category.Id;
    public string Title => Category.Title;
    public string Description => Category.Description;
    public bool IsProtected => Category.IsProtected;
    public string? ManagementCategoryId => Category.ManagementCategoryId;
    public IReadOnlyList<string> SourceDirectories { get; }
    public Brush Color { get; }
    public long Bytes { get; private set; }
    public int Count => Files.Count;
    public double Share { get; private set; }
    public bool IsScanned { get; private set; }
    public IReadOnlyList<FileMaintenanceEntry> Files { get; private set; } = [];
    public string SizeText => IsScanned ? StorageCleanupItem.FormatBytes(Bytes) : MaintenanceText.Get("Updating");
    public bool CanManage => IsScanned && (Files.Count > 0 || SourceDirectories.Count > 0 || Category.OpenManager != null);

    internal StorageUsageItem(StorageUsageCategory category, Brush color)
    {
        Category = category;
        Color = color;
        SourceDirectories = category.SourceDirectories.Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal void SetFiles(IReadOnlyList<FileMaintenanceEntry> files)
    {
        Files = files;
        Bytes = files.Sum(file => file.Length);
        IsScanned = true;
        OnPropertyChanged(nameof(Files)); OnPropertyChanged(nameof(Bytes)); OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(IsScanned)); OnPropertyChanged(nameof(SizeText)); OnPropertyChanged(nameof(CanManage));
    }

    internal void SetShare(long total)
    {
        Share = total > 0 ? (double)Bytes / total : 0;
        OnPropertyChanged(nameof(Share));
    }
}

/// <summary>Accounts for owner-declared files only. Usage snapshots never grant cleanup authority.</summary>
public sealed class StorageOverviewViewModel : ViewModelBase
{
    private static readonly string[] Colors = ["#E9B72C", "#A26AE7", "#EC8346", "#32AC75", "#4D9BD6", "#8C91A0", "#D66E9C"];
    private readonly IStorageUsageProvider[] _providers;
    private CancellationTokenSource? _cancellation;
    private bool _isBusy;
    private string _status = MaintenanceText.NotScanned;
    public ObservableCollection<StorageUsageItem> Items { get; } = [];
    public List<string> Issues { get; } = [];
    public Task ScanTask { get; private set; } = Task.CompletedTask;
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsIdle)); } }
    public bool IsIdle => !IsBusy;
    public bool IsComplete { get; private set; }
    public bool HasItems => Items.Count > 0;
    public IEnumerable<StorageUsageItem> ManagedSources => Items.Where(item => !item.Id.StartsWith("Application.Storage.", StringComparison.Ordinal));
    public bool HasManagedSources => ManagedSources.Any();
    public long TotalBytes => Items.Sum(item => item.Bytes);
    public int TotalCount => Items.Sum(item => item.Count);
    public string Summary => string.Format(MaintenanceText.Get("UsedStorage"), StorageCleanupItem.FormatBytes(TotalBytes));
    public string PathText => string.Join(Environment.NewLine, Items.SelectMany(item => item.SourceDirectories).Distinct(StringComparer.OrdinalIgnoreCase));
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public int ScanVersion { get; private set; }

    public StorageOverviewViewModel(IEnumerable<IStorageUsageProvider> providers) => _providers = providers.ToArray();

    public Task ScanAsync()
    {
        if (IsBusy) return ScanTask;
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        IsBusy = true;
        IsComplete = false;
        ScanTask = ScanCoreAsync(_cancellation.Token);
        return ScanTask;
    }

    private async Task ScanCoreAsync(CancellationToken cancellationToken)
    {
        Items.Clear();
        Issues.Clear();
        Status = MaintenanceText.Get("Updating");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Providers capture dispatcher-owned configuration here, before scanning on a worker.
            foreach (var provider in _providers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    provider.PrepareStorageScan();
                    foreach (var category in provider.GetStorageCategories())
                    {
                        if (string.IsNullOrWhiteSpace(category.Id) || !ids.Add(category.Id))
                            throw new InvalidOperationException($"Duplicate or empty storage category: {category.Id}");
                        var color = (SolidColorBrush)new BrushConverter().ConvertFromInvariantString(Colors[Items.Count % Colors.Length])!;
                        color.Freeze();
                        Items.Add(new StorageUsageItem(category, color));
                    }
                }
                catch (Exception ex) { Issues.Add($"{provider.GetType().Name}: {ex.Message}"); }
            }
            NotifyUsage();
            foreach (var item in Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    StorageUsageSnapshot snapshot = await Task.Run(() => item.Category.Scan(cancellationToken), cancellationToken);
                    if (snapshot.IsCancelled) throw new OperationCanceledException(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    Issues.AddRange(snapshot.Issues);
                    if (snapshot.Bytes > 0 && snapshot.Files.Count == 0)
                        Issues.Add($"{item.Title}: {MaintenanceText.Get("UsageManifestMissing")}");
                    var files = new List<FileMaintenanceEntry>();
                    foreach (var file in snapshot.Files)
                    {
                        try
                        {
                            string path = Path.GetFullPath(file.FullPath);
                            if (file.Length >= 0 && seenFiles.Add(path)) files.Add(file with { FullPath = path });
                        }
                        catch (Exception ex) { Issues.Add($"{item.Title}: {ex.Message}"); }
                    }
                    item.SetFiles(files);
                    NotifyUsage();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { Issues.Add($"{item.Title}: {ex.Message}"); }
            }
            IsComplete = Issues.Count == 0;
            Status = Issues.Count == 0 ? MaintenanceText.Get("UsageUpdated") : string.Format(MaintenanceText.Get("UsagePartial"), Issues.Count);
        }
        catch (OperationCanceledException) { Status = MaintenanceText.Get("UsageCancelled"); }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(IsComplete));
            NotifyUsage();
        }
    }

    private void NotifyUsage()
    {
        long total = TotalBytes;
        foreach (var item in Items) item.SetShare(total);
        ScanVersion++;
        OnPropertyChanged(nameof(TotalBytes)); OnPropertyChanged(nameof(TotalCount)); OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasItems)); OnPropertyChanged(nameof(PathText)); OnPropertyChanged(nameof(ScanVersion));
        OnPropertyChanged(nameof(ManagedSources)); OnPropertyChanged(nameof(HasManagedSources));
    }

    public void Cancel() => _cancellation?.Cancel();
}
