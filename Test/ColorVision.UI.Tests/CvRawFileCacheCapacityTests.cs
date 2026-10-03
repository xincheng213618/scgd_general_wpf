using ColorVision.FileIO;
using System.IO;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class CvRawFileCacheCapacityTests
{
    [Fact]
    public void SingleEntryLimitReusesBufferWhenOpeningAnotherFile()
    {
        using CacheFiles files = new(maximumEntries: 1);
        files.ReadAndAssert(files.A);
        CVFileReadCacheSnapshot first = CVFileReadCache.GetSnapshot();

        files.ReadAndAssert(files.B);
        CVFileReadCacheSnapshot second = CVFileReadCache.GetSnapshot();

        Assert.Equal(1, second.MaximumEntries);
        Assert.Equal(files.B, Assert.Single(second.Entries).FilePath);
        Assert.Equal(first.CapacityBytes, second.CapacityBytes);
        Assert.Equal(first.AllocationCount, second.AllocationCount);
        Assert.Equal(0, second.ActiveReaders);
    }

    [Fact]
    public void TwoEntriesRetainBothFilesAndEvictLeastRecentlyReadFile()
    {
        using CacheFiles files = new(maximumEntries: 2);
        files.ReadAndAssert(files.A);
        files.ReadAndAssert(files.B);
        AssertPaths(files.A, files.B);
        CVFileReadCacheSnapshot beforeHit = CVFileReadCache.GetSnapshot();

        files.ReadAndAssert(files.A);

        Assert.Equal(beforeHit.HitCount + 1, CVFileReadCache.GetSnapshot().HitCount);
        Assert.Equal(beforeHit.AllocationCount, CVFileReadCache.GetSnapshot().AllocationCount);
        files.ReadAndAssert(files.C);
        AssertPaths(files.A, files.C);
        files.ReadAndAssert(files.A);
        Assert.Equal(beforeHit.HitCount + 2, CVFileReadCache.GetSnapshot().HitCount);
    }

    [Fact]
    public void ShrinkingLimitKeepsBorrowedBytesValidAndFreesRetiredEntryWhenReaderReturns()
    {
        using CacheFiles files = new(maximumEntries: 2);
        files.ReadAndAssert(files.A);
        files.ReadAndAssert(files.B);
        using Stream firstReader = CVFileReadCache.OpenRead(files.A);
        using Stream secondReader = CVFileReadCache.OpenRead(files.B);
        long capacity = CVFileReadCache.GetSnapshot().CapacityBytes;

        CVFileReadCache.MaximumEntries = 1;

        CVFileReadCacheSnapshot pending = CVFileReadCache.GetSnapshot();
        Assert.Equal(2, pending.ActiveReaders);
        Assert.Equal(capacity, pending.CapacityBytes);
        CVFileReadCacheEntrySnapshot retired = Assert.Single(pending.Entries, entry => entry.IsRetired);
        Assert.Equal(1, retired.ActiveReaders);
        Assert.Equal(files.B, Assert.Single(pending.Entries, entry => !entry.IsRetired).FilePath);
        files.AssertContent(files.A, firstReader);
        firstReader.Dispose();

        CVFileReadCacheSnapshot completed = CVFileReadCache.GetSnapshot();
        Assert.Equal(capacity - retired.CapacityBytes, completed.CapacityBytes);
        Assert.Equal(1, completed.ActiveReaders);
        Assert.False(Assert.Single(completed.Entries).IsRetired);
        files.AssertContent(files.B, secondReader);
    }

    [Fact]
    public void DisablingFreesIdleEntriesAndKeepsBorrowedEntryUntilItsReaderReturns()
    {
        using CacheFiles files = new(maximumEntries: 2);
        files.ReadAndAssert(files.A);
        files.ReadAndAssert(files.B);
        using Stream reader = CVFileReadCache.OpenRead(files.A);

        CVFileReadCache.IsEnabled = false;

        CVFileReadCacheSnapshot pending = CVFileReadCache.GetSnapshot();
        Assert.False(pending.IsEnabled);
        Assert.True(Assert.Single(pending.Entries).IsRetired);
        Assert.Equal(1, pending.ActiveReaders);
        files.AssertContent(files.A, reader);
        reader.Dispose();
        CVFileReadCacheSnapshot completed = CVFileReadCache.GetSnapshot();
        Assert.Equal(0, completed.CapacityBytes);
        Assert.Empty(completed.Entries);
        files.ReadAndAssert(files.B);
        Assert.Equal(0, CVFileReadCache.GetSnapshot().CapacityBytes);
        Assert.Equal(completed.AllocationCount, CVFileReadCache.GetSnapshot().AllocationCount);
    }

    [Fact]
    public void BusyEntryBypassesAnotherFileWithoutAllocatingBeyondLimit()
    {
        using CacheFiles files = new(maximumEntries: 1);
        using Stream reader = CVFileReadCache.OpenRead(files.A);
        CVFileReadCacheSnapshot before = CVFileReadCache.GetSnapshot();

        files.ReadAndAssert(files.B);

        CVFileReadCacheSnapshot after = CVFileReadCache.GetSnapshot();
        Assert.Equal(before.CapacityBytes, after.CapacityBytes);
        Assert.Equal(before.AllocationCount, after.AllocationCount);
        Assert.Equal(files.A, Assert.Single(after.Entries).FilePath);
        Assert.Equal(1, after.ActiveReaders);
        files.AssertContent(files.A, reader);
    }

    [Fact]
    public void RewritingOneFileAndAppendingMetadataKeepOtherEntryAndCachedBytesCorrect()
    {
        using CacheFiles files = new(maximumEntries: 2);
        files.ReadAndAssert(files.A);
        files.ReadAndAssert(files.B);
        byte[] originalB = File.ReadAllBytes(files.B);
        CVFileReadCacheEntrySnapshot beforeB = Assert.Single(CVFileReadCache.GetSnapshot().Entries, entry => entry.FilePath == files.B);

        using (CVCIEFile rewritten = CacheFiles.CreateRaw(71)) Assert.True(CVFileUtil.WriteCVRaw(files.A, rewritten));
        CVFileMetadata.SetProperty(files.A, "cache-test", 12, [7, 8, 9]);

        AssertPaths(files.A, files.B);
        files.ReadAndAssert(files.A);
        files.ReadAndAssert(files.B);
        Assert.Equal(originalB, File.ReadAllBytes(files.B));
        CVFileReadCacheSnapshot snapshot = CVFileReadCache.GetSnapshot();
        CVFileReadCacheEntrySnapshot afterA = Assert.Single(snapshot.Entries, entry => entry.FilePath == files.A);
        CVFileReadCacheEntrySnapshot afterB = Assert.Single(snapshot.Entries, entry => entry.FilePath == files.B);
        Assert.Equal(new FileInfo(files.A).Length, afterA.ContentBytes);
        Assert.Equal(beforeB.CapacityBytes, afterB.CapacityBytes);
        Assert.Equal(beforeB.ContentBytes, afterB.ContentBytes);
        Assert.Equal(new byte[] { 7, 8, 9 }, CVFileMetadata.Read(files.A)["cache-test"].Value);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task SlowDiskOpenOrReadDoesNotBlockOtherFilesAndRetiredLoadStaysReadable(bool pauseOpen, bool disableCache)
    {
        using CacheFiles files = new(maximumEntries: 2);
        files.ReadAndAssert(files.A);
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim resume = new(false);
        void Pause() { entered.Set(); resume.Wait(); }
        using ControlledReadStream source = new(File.ReadAllBytes(files.B), pauseOpen ? () => { } : Pause);
        Task<Stream> load = Task.Run(() => OpenWithSource(files.B, () =>
        {
            if (pauseOpen) Pause();
            return source;
        }));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            await Task.Run(() =>
            {
                Assert.Null(CVFileReadCache.GetCachedLength(files.B));
                files.ReadAndAssert(files.A);
                using CVCIEFile raw = CacheFiles.CreateRaw(91);
                Assert.True(CVFileUtil.WriteCVRaw(files.C, raw, CVFileSaveMode.MemoryOnly));
                CVFileMetadata.SetProperty(files.C, "during-load", 1, [2, 3]);
                Assert.Equal(new byte[] { 2, 3 }, CVFileMetadata.Read(files.C)["during-load"].Value);
                using Stream retained = CVFileReadCache.OpenRead(files.C);
                if (disableCache) CVFileReadCache.IsEnabled = false;
                else CVFileReadCache.MaximumEntries = 1;
            }).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            resume.Set();
            using Stream read = await load.WaitAsync(TimeSpan.FromSeconds(10));
            files.AssertContent(files.B, read);
        }
        if (disableCache) Assert.Empty(CVFileReadCache.GetSnapshot().Entries);
        else AssertPaths(files.C);
        Assert.Equal(0, CVFileReadCache.GetSnapshot().ActiveReaders);
    }

    [Theory]
    [InlineData(CVFileSaveMode.Synchronous)]
    [InlineData(CVFileSaveMode.MemoryOnly)]
    public async Task SameFileWriterWaitsForDiskLoadAndPreservesOriginalReader(CVFileSaveMode mode)
    {
        using CacheFiles files = new(maximumEntries: 1);
        byte[] original = File.ReadAllBytes(files.A);
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim resume = new(false);
        using ControlledReadStream source = new(original, () => { entered.Set(); resume.Wait(); });
        Task<Stream> load = Task.Run(() => OpenWithSource(files.A, () => source));
        TaskCompletionSource written = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread writer = new(() =>
        {
            try
            {
                using CVCIEFile raw = CacheFiles.CreateRaw(91);
                Assert.True(CVFileUtil.WriteCVRaw(files.A, raw, mode));
                written.SetResult();
            }
            catch (Exception ex) { written.SetException(ex); }
        }) { IsBackground = true };
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            writer.Start();
            Assert.True(SpinWait.SpinUntil(() => written.Task.IsCompleted
                || (writer.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(10)));
            Assert.False(written.Task.IsCompleted);
            Assert.Null(CVFileReadCache.GetCachedLength(files.A));
        }
        finally
        {
            resume.Set();
            using Stream read = await load.WaitAsync(TimeSpan.FromSeconds(10));
            await written.Task.WaitAsync(TimeSpan.FromSeconds(10));
            using MemoryStream copy = new();
            read.CopyTo(copy);
            Assert.Equal(original, copy.ToArray());
        }
        Assert.True(CVFileUtil.ReadCVRaw(files.A, out CVCIEFile saved));
        using (saved) Assert.Equal(Enumerable.Repeat((byte)91, 6), saved.Data);
        Assert.Equal(0, CVFileReadCache.GetSnapshot().ActiveReaders);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedDiskFillDoesNotLeavePinnedOrPartiallyPublishedCache(bool outOfMemory)
    {
        using CacheFiles files = new(maximumEntries: 1);
        byte[] original = File.ReadAllBytes(files.A);
        using ControlledReadStream source = new(original, () =>
        {
            if (outOfMemory) throw new OutOfMemoryException();
            throw new IOException("Simulated read failure.");
        });
        if (outOfMemory)
        {
            using Stream fallback = OpenWithSource(files.A, () => source);
            Assert.Same(source, fallback);
            Assert.Equal(0, fallback.Position);
        }
        else
        {
            TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => OpenWithSource(files.A, () => source));
            Assert.IsType<IOException>(failure.InnerException);
        }
        Assert.Null(CVFileReadCache.GetCachedLength(files.A));
        Assert.Empty(CVFileReadCache.GetSnapshot().Entries);
        files.ReadAndAssert(files.B);
    }

    private static Stream OpenWithSource(string path, Func<Stream> openFile)
        => (Stream)typeof(CVFileReadCache).GetMethod("OpenReadCore", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [path, true, openFile])!;

    private sealed class ControlledReadStream(byte[] bytes, Action beforeCopy) : MemoryStream(bytes)
    {
        public override void CopyTo(Stream destination, int bufferSize)
        {
            beforeCopy();
            base.CopyTo(destination, bufferSize);
        }
    }

    private static void AssertPaths(params string[] paths)
        => Assert.Equal(paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
            CVFileReadCache.GetSnapshot().Entries.Select(entry => entry.FilePath).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

    private sealed class CacheFiles : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"cvraw-capacity-{Guid.NewGuid():N}");
        private readonly bool previousEnabled = CVFileReadCache.IsEnabled;
        private readonly int previousMaximumEntries = CVFileReadCache.MaximumEntries;

        public string A { get; }
        public string B { get; }
        public string C { get; }

        public CacheFiles(int maximumEntries)
        {
            Directory.CreateDirectory(root);
            A = Path.Combine(root, "a.cvraw");
            B = Path.Combine(root, "b.cvraw");
            C = Path.Combine(root, "c.cvraw");
            CVFileReadCache.Release();
            CVFileReadCache.IsEnabled = false;
            using (CVCIEFile raw = CreateRaw(11)) Assert.True(CVFileUtil.WriteCVRaw(A, raw));
            using (CVCIEFile raw = CreateRaw(31)) Assert.True(CVFileUtil.WriteCVRaw(B, raw));
            using (CVCIEFile raw = CreateRaw(51)) Assert.True(CVFileUtil.WriteCVRaw(C, raw));
            CVFileReadCache.MaximumEntries = maximumEntries;
            CVFileReadCache.IsEnabled = true;
        }

        public static CVCIEFile CreateRaw(byte value)
            => new() { Version = 1, Cols = 3, Rows = 2, Bpp = 8, Channels = 1, Exp = [10], Data = Enumerable.Repeat(value, 6).ToArray() };

        public void ReadAndAssert(string path)
        {
            using Stream read = CVFileReadCache.OpenRead(path);
            AssertContent(path, read);
        }

        public void AssertContent(string path, Stream read)
        {
            using MemoryStream copy = new();
            read.CopyTo(copy);
            Assert.Equal(File.ReadAllBytes(path), copy.ToArray());
        }

        public void Dispose()
        {
            CVFileReadCache.Release();
            CVFileReadCache.MaximumEntries = previousMaximumEntries;
            CVFileReadCache.IsEnabled = previousEnabled;
            foreach (string path in Directory.EnumerateFiles(root)) File.Delete(path);
            Directory.Delete(root);
        }
    }
}
