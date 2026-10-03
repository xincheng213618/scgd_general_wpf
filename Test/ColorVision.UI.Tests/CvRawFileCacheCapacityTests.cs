using ColorVision.FileIO;
using System.IO;

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
