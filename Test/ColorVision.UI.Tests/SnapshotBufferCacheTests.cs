using ColorVision.Engine.Services.Caches;
using ColorVision.ImageEditor.Output;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class SnapshotBufferCacheTests
{
    [Fact]
    public void ScopedReleasePreservesOtherViewsAndRetiresOnlyExistingLeases()
    {
        WpfTestHost.Invoke(() =>
        {
            using SnapshotImageBufferPool first = new();
            using SnapshotImageBufferPool second = new();
            first.SetSourceName("same-title");
            second.SetSourceName("same-title");
            WriteableBitmap source = CreateSource();
            using SnapshotImageBufferLease active = first.Capture(source);
            using (first.Capture(source)) { }
            using (second.Capture(source)) { }
            long bytes = source.PixelWidth * source.PixelHeight * 4;
            SnapshotBufferCacheEntry before = first.GetSnapshot();
            Assert.Equal(bytes, before.ActiveBytes);
            Assert.Equal(bytes, before.IdleBytes);
            Assert.Equal(1, before.ActiveCount);

            SnapshotBufferReleaseResult result = SnapshotBufferCache.Release(first.Id);
            Assert.Equal(bytes, result.ReleasedBytes);
            Assert.Equal(bytes, result.PendingReleaseBytes);
            Assert.Equal(bytes, second.GetSnapshot().IdleBytes);
            Assert.Equal(37, Marshal.ReadByte(active.Image.pData));

            // A new capture must survive the return of an older, retired generation.
            using (first.Capture(source)) { }
            active.Dispose();
            active.Dispose();
            Assert.Equal(0, first.GetSnapshot().ActiveBytes);
            Assert.Equal(0, first.GetSnapshot().PendingReleaseBytes);
            Assert.Equal(0, first.GetSnapshot().ActiveCount);
            Assert.Equal(bytes, first.GetSnapshot().IdleBytes);
            using (first.Capture(source)) { }
            Assert.Equal(1, first.GetSnapshot().HitCount);
            Assert.Contains(SnapshotBufferCache.GetSnapshot(), entry => entry.Id == first.Id);
        });
    }

    [Fact]
    public async Task ModuleReleaseAllIncludesActiveExportsAndClosedViewsUntilTheirReturn()
    {
        using SnapshotImageBufferPool first = new();
        using SnapshotImageBufferPool second = new();
        using SnapshotImageBufferLease lease = WpfTestHost.Invoke(() => first.Capture(CreateSource()));
        WpfTestHost.Invoke(() => { using (second.Capture(CreateSource())) { } });
        first.Dispose();
        Assert.True(Assert.Single(SnapshotBufferCache.GetSnapshot(), entry => entry.Id == first.Id).IsClosed);
        SnapshotCacheModule module = new();
        CacheEntrySnapshot entry = Assert.Single(module.GetSnapshot().Entries, item => item.SourceId == first.Id);
        Assert.Equal(64UL, entry.ActiveBytes);
        Assert.Equal(64UL, entry.PendingReleaseBytes);
        Assert.Equal(1UL, entry.ActiveReferences);

        CacheModuleReleaseResult result = Assert.Single(await CacheManagerService.ReleaseAllAsync([module]));
        Assert.True(result.Succeeded);
        Assert.True(result.ReleasedBytes >= 64);
        Assert.Equal(0, second.GetSnapshot().IdleBytes);
        Assert.Equal(37, Marshal.ReadByte(lease.Image.pData));
        lease.Dispose();
        Assert.DoesNotContain(SnapshotBufferCache.GetSnapshot(), item => item.Id == first.Id);
        Assert.Equal(0, first.GetSnapshot().IdleBytes);
        WpfTestHost.Invoke(() => Assert.Throws<ObjectDisposedException>(() => first.Capture(CreateSource())));
    }

    private static WriteableBitmap CreateSource()
    {
        WriteableBitmap source = new(4, 4, 96, 96, PixelFormats.Bgra32, null);
        byte[] pixels = new byte[64];
        pixels[0] = 37;
        source.WritePixels(new(0, 0, 4, 4), pixels, 16, 0);
        return source;
    }
}
