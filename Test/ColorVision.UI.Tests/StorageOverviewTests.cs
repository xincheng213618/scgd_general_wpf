using ColorVision.Settings.Maintenance;
using ColorVision.UI.Maintenance;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class StorageOverviewTests
{
    [Fact]
    public void ConstructionDoesNotPrepareOrScanModuleStorage()
    {
        var category = new Category("protected", _ => new(0, 0, [], []));
        var provider = new Provider(category);
        var model = new StorageOverviewViewModel([provider]);

        Assert.Equal(0, provider.Preparations);
        Assert.Equal(0, category.Scans);
        Assert.Empty(model.Items);
        Assert.False(model.IsBusy);
        Assert.False(model.IsComplete);
    }

    [Fact]
    public async Task OccupancyIncludesProtectedFilesAndDeduplicatesOverlappingCategories()
    {
        string root = FixtureRoot();
        var first = new Category("protected", _ => Snapshot(File(root, "shared.dat", 200))) { IsProtectedValue = true };
        var second = new Category("outputs", _ => Snapshot(File(root, "SHARED.dat", 200), File(root, "output.dat", 100)));
        var model = new StorageOverviewViewModel([new Provider(first), new Provider(second)]);

        await model.ScanAsync();

        Assert.Equal(300, model.TotalBytes);
        Assert.Equal(2, model.TotalCount);
        Assert.Equal(new long[] { 200, 100 }, model.Items.Select(item => item.Bytes));
        Assert.True(model.Items[0].IsProtected);
        Assert.Equal(1d, model.Items.Sum(item => item.Share), 12);
        Assert.True(model.IsComplete);
        Assert.All(model.Items, item => Assert.True(item.IsScanned));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task FailedPreparationDropsItsPreviousStatisticsWithoutHidingHealthyProviders()
    {
        string root = FixtureRoot();
        var failing = new Provider(new Category("failing", _ => Snapshot(File(root, "first.dat", 20))));
        var healthy = new Provider(new Category("healthy", _ => Snapshot(File(root, "second.dat", 30))));
        var model = new StorageOverviewViewModel([failing, healthy]);
        await model.ScanAsync();
        Assert.Equal(50, model.TotalBytes);

        failing.FailPrepare = true;
        await model.ScanAsync();

        Assert.Equal(30, model.TotalBytes);
        Assert.Equal("healthy", Assert.Single(model.Items).Id);
        Assert.Contains(model.Issues, issue => issue.Contains("fixture-prepare-error", StringComparison.Ordinal));
        Assert.False(model.IsComplete);
        Assert.False(model.IsBusy);
    }

    [Fact]
    public async Task ByteOnlySnapshotsCannotPretendToBeDeduplicatedFileOccupancy()
    {
        var model = new StorageOverviewViewModel([new Provider(new Category("opaque", _ => new(500, 5, [], [])))]);

        await model.ScanAsync();

        Assert.Equal(0, model.TotalBytes);
        Assert.NotEmpty(model.Issues);
        Assert.False(model.IsComplete);
    }

    [Fact]
    public async Task EmptyModuleStorageCanOfferItsManagerWithoutExecutingItDuringDiscoveryOrScan()
    {
        int opened = 0;
        var category = new Category("resources", _ => new(0, 0, [], []))
        {
            IsProtectedValue = true,
            OpenManagerValue = () => opened++
        };
        var model = new StorageOverviewViewModel([new Provider(category)]);

        await model.ScanAsync();

        StorageUsageItem item = Assert.Single(model.Items);
        Assert.True(item.IsScanned);
        Assert.Equal(0, item.Bytes);
        Assert.True(item.CanManage);
        Assert.True(item.IsProtected);
        Assert.Equal(0, opened);
    }

    [Fact]
    public async Task RescanReplacesTheManifestInsteadOfAccumulatingOldFiles()
    {
        string root = FixtureRoot();
        long bytes = 10;
        var model = new StorageOverviewViewModel([new Provider(new Category("output", _ => Snapshot(File(root, "result.dat", bytes))))]);
        await model.ScanAsync();
        int version = model.ScanVersion;
        Assert.Equal(10, model.TotalBytes);

        bytes = 25;
        await model.ScanAsync();

        Assert.Equal(25, model.TotalBytes);
        Assert.Equal(1, model.TotalCount);
        Assert.Single(model.Items);
        Assert.True(model.ScanVersion > version);
    }

    [Fact]
    public async Task CancellationRetainsOnlyCompletedReadonlyStatisticsAndCannotManageTheUnfinishedCategory()
    {
        string root = FixtureRoot();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var complete = new Category("complete", _ => Snapshot(File(root, "complete.dat", 10)));
        var blocked = new Category("blocked", token =>
        {
            entered.SetResult();
            if (!token.WaitHandle.WaitOne(TimeSpan.FromSeconds(10))) throw new TimeoutException("The cancellation request was not received.");
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Cancelled scans must not publish files.");
        });
        var model = new StorageOverviewViewModel([new Provider(complete, blocked)]);
        Task scan = model.ScanAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        model.Cancel();
        await scan.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(10, model.TotalBytes);
        Assert.True(model.Items[0].IsScanned);
        Assert.False(model.Items[1].IsScanned);
        Assert.False(model.Items[1].CanManage);
        Assert.False(model.IsComplete);
        Assert.False(model.IsBusy);
        Assert.Equal(MaintenanceText.Get("UsageCancelled"), model.Status);
    }

    private static string FixtureRoot() => Path.Combine(Path.GetTempPath(), "ColorVisionStorageOverviewTests-" + Guid.NewGuid().ToString("N"));
    private static FileMaintenanceEntry File(string root, string name, long bytes) => new(Path.Combine(root, name), bytes, DateTime.UnixEpoch);
    private static StorageUsageSnapshot Snapshot(params FileMaintenanceEntry[] files) => new(files.Sum(file => file.Length), files.Length, files, []);

    private sealed class Provider(params Category[] categories) : IStorageUsageProvider
    {
        internal int Preparations { get; private set; }
        internal bool FailPrepare { get; set; }
        public void PrepareStorageScan()
        {
            Preparations++;
            if (FailPrepare) throw new IOException("fixture-prepare-error");
        }
        public IEnumerable<StorageUsageCategory> GetStorageCategories() => categories;
    }

    private sealed class Category(string id, Func<CancellationToken, StorageUsageSnapshot> scan) : StorageUsageCategory
    {
        public override string Id => id;
        public override string Title => id;
        public override string Description => "Isolated storage accounting fixture.";
        public override bool IsProtected => IsProtectedValue;
        public override Action? OpenManager => OpenManagerValue;
        internal bool IsProtectedValue { get; init; }
        internal Action? OpenManagerValue { get; init; }
        internal int Scans { get; private set; }
        public override StorageUsageSnapshot Scan(CancellationToken cancellationToken) { Scans++; return scan(cancellationToken); }
    }
}
