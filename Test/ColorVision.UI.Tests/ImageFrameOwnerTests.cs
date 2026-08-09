using ColorVision.Core;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageFrameOwnerTests
{
    [Fact]
    public void InvalidateKeepsRetiredFrameAliveUntilItsLastLeaseIsDisposed()
    {
        using ImageFrameStore store = new();
        ReleaseProbe first = new();
        ReleaseProbe second = new();

        ImageFrameLease firstLease = Assert.IsType<ImageFrameLease>(
            store.AcquireOrCreate(() => first.Image, first.Release));
        long firstRevision = firstLease.Revision;

        long secondRevision = store.Invalidate();
        ImageFrameLease secondLease = Assert.IsType<ImageFrameLease>(
            store.AcquireOrCreate(() => second.Image, second.Release));

        Assert.Equal(0, first.ReleaseCount);
        Assert.Equal(first.Pointer, firstLease.Image.pData);
        Assert.Equal(firstRevision, firstLease.Revision);
        Assert.False(store.IsCurrent(firstRevision));
        Assert.True(store.IsCurrent(secondRevision));
        Assert.Equal(secondRevision, secondLease.Revision);

        firstLease.Dispose();

        Assert.Equal(1, first.ReleaseCount);
        Assert.Equal(0, second.ReleaseCount);

        secondLease.Dispose();
        store.Dispose();

        Assert.Equal(1, first.ReleaseCount);
        Assert.Equal(1, second.ReleaseCount);
    }

    [Fact]
    public void LeaseDisposeIsIdempotentAndDisposedLeaseCannotExposeTheBuffer()
    {
        ReleaseProbe probe = new();
        using SourceImageFrame frame = new(probe.Image, revision: 17, probe.Release);
        ImageFrameLease lease = frame.Acquire();

        frame.Dispose();
        Assert.Equal(0, probe.ReleaseCount);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(1, probe.ReleaseCount);
        Assert.Throws<ObjectDisposedException>(() => _ = lease.Image);
    }

    [Fact]
    public void BorrowedHImageCannotReleaseTheOwnedFrameBuffer()
    {
        ReleaseProbe probe = new();
        using SourceImageFrame frame = new(probe.Image, revision: 23, probe.Release);
        using ImageFrameLease lease = frame.Acquire();

        HImage borrowed = lease.Image;
        Assert.True(borrowed.isDispose);

        borrowed.Dispose();

        Assert.Equal(0, probe.ReleaseCount);
        Assert.Equal(probe.Pointer, lease.Image.pData);

        frame.Dispose();
        Assert.Equal(0, probe.ReleaseCount);

        lease.Dispose();
        Assert.Equal(1, probe.ReleaseCount);
    }

    [Fact]
    public void InvalidateAndDisposeRejectResultsFromOlderRevisions()
    {
        ReleaseProbe first = new();
        ReleaseProbe second = new();
        ImageFrameStore store = new();

        using ImageFrameLease firstLease = Assert.IsType<ImageFrameLease>(
            store.AcquireOrCreate(() => first.Image, first.Release));
        long firstRevision = firstLease.Revision;

        long invalidatedRevision = store.Invalidate();

        Assert.True(invalidatedRevision > firstRevision);
        Assert.False(store.IsCurrent(firstLease.Revision));
        Assert.Null(store.AcquireOrCreate(() => null));
        Assert.Equal(0, first.ReleaseCount);

        using ImageFrameLease secondLease = Assert.IsType<ImageFrameLease>(
            store.AcquireOrCreate(() => second.Image, second.Release));
        long secondRevision = secondLease.Revision;

        Assert.Equal(invalidatedRevision, secondRevision);
        Assert.Equal(secondRevision, secondLease.Revision);
        Assert.True(store.IsCurrent(secondLease.Revision));

        store.Dispose();

        Assert.False(store.IsCurrent(secondLease.Revision));
        Assert.Throws<ObjectDisposedException>(() => store.AcquireOrCreate(() => null));
        Assert.Equal(0, first.ReleaseCount);
        Assert.Equal(0, second.ReleaseCount);

        firstLease.Dispose();
        secondLease.Dispose();

        Assert.Equal(1, first.ReleaseCount);
        Assert.Equal(1, second.ReleaseCount);
    }

    [Fact]
    public void RejectedLazyCandidateDoesNotAdvanceRevisionOrLeak()
    {
        using ImageFrameStore store = new();
        ReleaseProbe rejected = new();
        long revision = store.Revision;
        HImage invalidImage = rejected.Image;
        invalidImage.stride = -1;

        Assert.Throws<ArgumentException>(() =>
            store.AcquireOrCreate(() => invalidImage, rejected.Release));

        Assert.Equal(1, rejected.ReleaseCount);
        Assert.Equal(revision, store.Revision);
        Assert.True(store.IsCurrent(revision));
        Assert.Null(store.AcquireOrCreate(() => null));

        ReleaseProbe current = new();
        using ImageFrameLease lease = Assert.IsType<ImageFrameLease>(
            store.AcquireOrCreate(() => current.Image, current.Release));
        Assert.Equal(current.Pointer, lease.Image.pData);
        Assert.Equal(revision, lease.Revision);
    }

    [Fact]
    public void LazyAcquireTracksRevisionAndKeepsRetiredFrameAlive()
    {
        using ImageFrameStore store = new();
        ReleaseProbe first = new();
        ReleaseProbe second = new();

        using ImageFrameLease firstLease = Assert.IsType<ImageFrameLease>(
            store.AcquireOrCreate(() => first.Image, first.Release));
        long firstRevision = firstLease.Revision;

        long invalidatedRevision = store.Invalidate();

        Assert.True(invalidatedRevision > firstRevision);
        Assert.False(store.IsCurrent(firstRevision));
        Assert.Equal(first.Pointer, firstLease.Image.pData);
        Assert.Equal(0, first.ReleaseCount);

        using ImageFrameLease secondLease = Assert.IsType<ImageFrameLease>(
            store.AcquireOrCreate(() => second.Image, second.Release));

        Assert.Equal(invalidatedRevision, secondLease.Revision);
        Assert.True(store.IsCurrent(secondLease.Revision));

        firstLease.Dispose();
        Assert.Equal(1, first.ReleaseCount);

        store.Dispose();
        Assert.Equal(0, second.ReleaseCount);

        secondLease.Dispose();
        Assert.Equal(1, second.ReleaseCount);
    }

    [Fact]
    public async Task LazyCandidateIsReleasedWhenRevisionChangesDuringCreation()
    {
        using ImageFrameStore store = new();
        ReleaseProbe candidate = new();
        using ManualResetEventSlim factoryEntered = new(false);
        using ManualResetEventSlim allowFactoryToReturn = new(false);

        Task<ImageFrameLease?> acquire = Task.Run(() => store.AcquireOrCreate(
            () =>
            {
                factoryEntered.Set();
                allowFactoryToReturn.Wait();
                return candidate.Image;
            },
            candidate.Release));

        Assert.True(factoryEntered.Wait(TimeSpan.FromSeconds(5)));
        long invalidatedRevision = store.Invalidate();
        allowFactoryToReturn.Set();

        ImageFrameLease? lease = await acquire;

        Assert.Null(lease);
        Assert.Equal(1, candidate.ReleaseCount);
        Assert.Null(store.AcquireOrCreate(() => null));
        Assert.True(store.IsCurrent(invalidatedRevision));
    }

    [Fact]
    public async Task ConcurrentLazyAcquirePublishesOneFrameAndReleasesTheLosingCandidate()
    {
        using ImageFrameStore store = new();
        ReleaseProbe first = new();
        ReleaseProbe second = new();
        using CountdownEvent factoriesEntered = new(2);
        using ManualResetEventSlim allowFactoriesToReturn = new(false);

        Task<ImageFrameLease?> AcquireAsync(ReleaseProbe probe)
        {
            return Task.Run(() => store.AcquireOrCreate(
                () =>
                {
                    factoriesEntered.Signal();
                    allowFactoriesToReturn.Wait();
                    return probe.Image;
                },
                probe.Release));
        }

        Task<ImageFrameLease?> firstAcquire = AcquireAsync(first);
        Task<ImageFrameLease?> secondAcquire = AcquireAsync(second);
        Assert.True(factoriesEntered.Wait(TimeSpan.FromSeconds(5)));
        allowFactoriesToReturn.Set();

        ImageFrameLease?[] leases = await Task.WhenAll(firstAcquire, secondAcquire);
        ImageFrameLease firstLease = Assert.IsType<ImageFrameLease>(leases[0]);
        ImageFrameLease secondLease = Assert.IsType<ImageFrameLease>(leases[1]);

        Assert.Equal(firstLease.Revision, secondLease.Revision);
        Assert.Equal(firstLease.Image.pData, secondLease.Image.pData);

        ReleaseProbe winner = firstLease.Image.pData == first.Pointer ? first : second;
        ReleaseProbe loser = ReferenceEquals(winner, first) ? second : first;
        Assert.Equal(0, winner.ReleaseCount);
        Assert.Equal(1, loser.ReleaseCount);

        firstLease.Dispose();
        secondLease.Dispose();
        store.Dispose();

        Assert.Equal(1, winner.ReleaseCount);
        Assert.Equal(1, loser.ReleaseCount);
    }

    [Fact]
    public void WriteableBitmapSwitchAndInPlaceUpdateRetireThePreviousRevision()
    {
        RunOnStaThread(() =>
        {
            using ImageFrameStore store = new();
            WriteableBitmap firstBitmap = CreateGray8Bitmap(11);
            WriteableBitmap secondBitmap = CreateGray8Bitmap(22);

            store.Invalidate();
            using ImageFrameLease firstLease = Assert.IsType<ImageFrameLease>(
                store.AcquireOrCreate(() => firstBitmap.ToHImage()));
            long firstRevision = firstLease.Revision;

            store.Invalidate();
            using ImageFrameLease secondLease = Assert.IsType<ImageFrameLease>(
                store.AcquireOrCreate(() => secondBitmap.ToHImage()));
            long secondRevision = secondLease.Revision;

            Assert.False(store.IsCurrent(firstRevision));
            Assert.True(store.IsCurrent(secondRevision));
            Assert.Equal(11, Marshal.ReadByte(firstLease.Image.pData));
            Assert.Equal(22, Marshal.ReadByte(secondLease.Image.pData));

            secondBitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new byte[] { 33 }, 1, 0);
            store.Invalidate();
            using ImageFrameLease updatedLease = Assert.IsType<ImageFrameLease>(
                store.AcquireOrCreate(() => secondBitmap.ToHImage()));

            Assert.False(store.IsCurrent(secondRevision));
            Assert.True(store.IsCurrent(updatedLease.Revision));
            Assert.Equal(22, Marshal.ReadByte(secondLease.Image.pData));
            Assert.Equal(33, Marshal.ReadByte(updatedLease.Image.pData));
        });
    }

    [Fact]
    public async Task ConcurrentAcquireAndInvalidateNeverReleasesAnActivelyReadFrame()
    {
        using ImageFrameStore store = new();
        ConcurrentDictionary<IntPtr, ReleaseProbe> probes = new();
        ReleaseProbe initial = AddProbe(probes);
        using (ImageFrameLease initialLease = Assert.IsType<ImageFrameLease>(
            store.AcquireOrCreate(() => initial.Image, initial.Release)))
        {
        }

        using ManualResetEventSlim start = new(false);
        int writerCompleted = 0;
        int acquireCount = 0;
        Task[] readers = Enumerable.Range(0, 4)
            .Select(_ => Task.Run(() =>
            {
                start.Wait();
                while (Volatile.Read(ref writerCompleted) == 0)
                {
                    using ImageFrameLease? lease = store.AcquireOrCreate(() => null);
                    if (lease == null)
                    {
                        continue;
                    }

                    HImage image = lease.Image;
                    ReleaseProbe probe = probes[image.pData];
                    probe.EnterReader();
                    try
                    {
                        Assert.Equal(probe.Pointer, lease.Image.pData);
                        Assert.False(probe.IsReleased);
                        Thread.SpinWait(64);
                        Assert.False(probe.IsReleased);
                        Interlocked.Increment(ref acquireCount);
                    }
                    finally
                    {
                        probe.ExitReader();
                    }
                }
            }))
            .ToArray();

        Task writer = Task.Run(() =>
        {
            start.Wait();
            try
            {
                for (int index = 0; index < 2_000; index++)
                {
                    ReleaseProbe probe = AddProbe(probes);
                    store.Invalidate();
                    using ImageFrameLease lease = Assert.IsType<ImageFrameLease>(
                        store.AcquireOrCreate(() => probe.Image, probe.Release));
                    if ((index & 31) == 0)
                    {
                        Thread.Yield();
                    }
                }
            }
            finally
            {
                Volatile.Write(ref writerCompleted, 1);
            }
        });

        start.Set();
        await writer;
        await Task.WhenAll(readers);
        store.Dispose();

        Assert.True(acquireCount > 0);
        Assert.All(probes.Values, probe =>
        {
            Assert.Equal(0, probe.ActiveReaders);
            Assert.Equal(1, probe.ReleaseCount);
        });
    }

    [Fact]
    public async Task DisposeRacingWithAcquireAndPublishDoesNotLeakOrDoubleRelease()
    {
        for (int iteration = 0; iteration < 200; iteration++)
        {
            ImageFrameStore store = new();
            ReleaseProbe initial = new();
            ReleaseProbe replacement = new();
            using (ImageFrameLease initialLease = Assert.IsType<ImageFrameLease>(
                store.AcquireOrCreate(() => initial.Image, initial.Release)))
            {
            }
            using ManualResetEventSlim start = new(false);
            ImageFrameLease? acquiredLease = null;

            Task acquire = Task.Run(() =>
            {
                start.Wait();
                try
                {
                    acquiredLease = store.AcquireOrCreate(() => null);
                    if (acquiredLease != null)
                    {
                        Assert.Contains(
                            acquiredLease.Image.pData,
                            new[] { initial.Pointer, replacement.Pointer });
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Disposal won the race.
                }
            });
            Task publish = Task.Run(() =>
            {
                start.Wait();
                ImageFrameLease? replacementLease = null;
                int factoryCalled = 0;
                try
                {
                    store.Invalidate();
                    replacementLease = store.AcquireOrCreate(
                        () =>
                        {
                            Volatile.Write(ref factoryCalled, 1);
                            return replacement.Image;
                        },
                        replacement.Release);
                }
                catch (ObjectDisposedException)
                {
                    if (Volatile.Read(ref factoryCalled) == 0)
                    {
                        replacement.Release(replacement.Image);
                    }
                }
                finally
                {
                    replacementLease?.Dispose();
                }
            });
            Task dispose = Task.Run(() =>
            {
                start.Wait();
                store.Dispose();
            });

            start.Set();
            await Task.WhenAll(acquire, publish, dispose);
            acquiredLease?.Dispose();
            store.Dispose();

            Assert.Equal(1, initial.ReleaseCount);
            Assert.Equal(1, replacement.ReleaseCount);
        }
    }

    private static ReleaseProbe AddProbe(ConcurrentDictionary<IntPtr, ReleaseProbe> probes)
    {
        ReleaseProbe probe = new();
        Assert.True(probes.TryAdd(probe.Pointer, probe));
        return probe;
    }

    private static WriteableBitmap CreateGray8Bitmap(byte value)
    {
        WriteableBitmap bitmap = new(1, 1, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 1, 1), new[] { value }, 1, 0);
        return bitmap;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class ReleaseProbe
    {
        private static long nextPointer;
        private int activeReaders;
        private int releaseCount;

        public ReleaseProbe()
        {
            Pointer = new IntPtr(Interlocked.Increment(ref nextPointer));
            Image = new HImage
            {
                rows = 4,
                cols = 4,
                channels = 1,
                depth = 8,
                stride = 4,
                isDispose = false,
                pData = Pointer,
            };
        }

        public HImage Image { get; }

        public IntPtr Pointer { get; }

        public int ActiveReaders => Volatile.Read(ref activeReaders);

        public int ReleaseCount => Volatile.Read(ref releaseCount);

        public bool IsReleased => ReleaseCount != 0;

        public void EnterReader()
        {
            Assert.False(IsReleased);
            Interlocked.Increment(ref activeReaders);
            Assert.False(IsReleased);
        }

        public void ExitReader()
        {
            Assert.True(Interlocked.Decrement(ref activeReaders) >= 0);
        }

        public void Release(HImage image)
        {
            Assert.Equal(Pointer, image.pData);
            Assert.False(image.isDispose);
            Assert.Equal(0, ActiveReaders);
            Assert.Equal(0, Interlocked.Exchange(ref releaseCount, 1));
        }
    }
}
