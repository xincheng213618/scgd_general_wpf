using ColorVision.Algorithms;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmImageBufferTests
{
    [Fact]
    public void RetainReadOnlySharesStorageAndPreservesImageLayout()
    {
        using AlgorithmImageBuffer source = new(2, 2, 16, AlgorithmImageFormat.Bgr48,
            Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(), 123, 117);

        using AlgorithmImageBuffer retained = source.RetainReadOnly();

        Assert.NotSame(source, retained);
        Assert.True(source.Data.Equals(retained.Data), "Read-only retention must share the same pixel storage.");
        Assert.Equal(source.StorageId, retained.StorageId);
        Assert.Equal((source.Width, source.Height, source.Stride, source.Format, source.DpiX, source.DpiY),
            (retained.Width, retained.Height, retained.Stride, retained.Format, retained.DpiX, retained.DpiY));
    }

    [Fact]
    public void BufferRetentionAndLeaseHaveIndependentDisposableLifetimes()
    {
        byte[] expected = [1, 2, 3, 4];
        using AlgorithmImageBuffer source = new(2, 2, 2, AlgorithmImageFormat.Gray8, expected);
        using AlgorithmImageBuffer retained = source.RetainReadOnly();
        using AlgorithmImageBufferLease lease = retained.AcquireReadOnlyLease();

        lease.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = lease.Data);
        Assert.Equal(expected, source.Data.ToArray());
        Assert.Equal(expected, retained.Data.ToArray());

        using AlgorithmImageBufferLease survivingLease = retained.AcquireReadOnlyLease();
        retained.Dispose();
        Assert.Throws<ObjectDisposedException>(() => retained.RetainReadOnly());
        Assert.Equal(expected, source.Data.ToArray());
        Assert.Equal(expected, survivingLease.Data.ToArray());

        using AlgorithmImageBuffer survivingBuffer = source.RetainReadOnly();
        source.Dispose();
        Assert.Throws<ObjectDisposedException>(() => source.RetainReadOnly());
        Assert.Throws<ObjectDisposedException>(() => source.AcquireReadOnlyLease());
        Assert.Throws<ObjectDisposedException>(() => source.Clone());
        Assert.Throws<ObjectDisposedException>(() => _ = source.Data);
        Assert.Equal(expected, survivingBuffer.Data.ToArray());
        Assert.Equal(expected, survivingLease.Data.ToArray());

        survivingBuffer.Dispose();
        Assert.Equal(expected, survivingLease.Data.ToArray());
    }

    [Fact]
    public void CloneStillOwnsAnIndependentPixelCopy()
    {
        using AlgorithmImageBuffer source = new(2, 2, 2, AlgorithmImageFormat.Gray8, [1, 2, 3, 4], 123, 117);
        using AlgorithmImageBuffer retained = source.RetainReadOnly();

        using AlgorithmImageBuffer clone = retained.Clone();

        Assert.False(source.Data.Equals(clone.Data), "Clone must continue to copy pixel storage.");
        Assert.NotEqual(source.StorageId, clone.StorageId);
        Assert.Equal(source.Data.ToArray(), clone.Data.ToArray());
        Assert.Equal((source.Width, source.Height, source.Stride, source.Format, source.DpiX, source.DpiY),
            (clone.Width, clone.Height, clone.Stride, clone.Format, clone.DpiX, clone.DpiY));
        source.Dispose();
        retained.Dispose();
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, clone.Data.ToArray());
    }

    [Fact]
    public void RewrappingAnArrayCreatesANewSnapshotIdentity()
    {
        byte[] pixels = [1, 2, 3, 4];
        using AlgorithmImageBuffer original = new(2, 2, 2, AlgorithmImageFormat.Gray8, pixels);
        Guid oldId = original.StorageId;
        original.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = original.StorageId);
        pixels[0] = 9;
        using AlgorithmImageBuffer replacement = new(2, 2, 2, AlgorithmImageFormat.Gray8, pixels);
        Assert.NotEqual(oldId, replacement.StorageId);
    }
}
