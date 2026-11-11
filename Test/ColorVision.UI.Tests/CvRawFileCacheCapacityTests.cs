using ColorVision.FileIO;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;

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

    [Theory]
    [InlineData("read", false)]
    [InlineData("missing", false)]
    [InlineData("save", false)]
    [InlineData("read", true)]
    public void OpportunisticDiskFillKeepsTheOnlyCopyOfAMemoryOnlyCapture(string operation, bool hasDiskEntry)
    {
        using CacheFiles files = new(maximumEntries: hasDiskEntry ? 2 : 1);
        string directory = Path.GetDirectoryName(files.A)!;
        string capturePath = Path.Combine(directory, "capture.cvraw");
        using CVCIEFile capture = CacheFiles.CreateRaw(71);
        Assert.True(CVFileUtil.WriteCVRaw(capturePath, capture, CVFileSaveMode.MemoryOnly));
        CVFileMetadata.SetProperty(capturePath, "calibration", 1, [2, 3, 5]);
        if (hasDiskEntry) files.ReadAndAssert(files.A);
        var before = CVFileReadCache.GetSnapshot();

        if (operation == "save")
        {
            using CVCIEFile diskImage = CacheFiles.CreateRaw(91);
            Assert.True(CVFileUtil.WriteCVRaw(files.B, diskImage));
            Assert.True(File.Exists(files.B));
        }
        else if (operation == "missing")
        {
            string missing = Path.Combine(directory, "missing.cvraw");
            Assert.Throws<FileNotFoundException>(() => CVFileReadCache.OpenRead(missing).Dispose());
        }
        else files.ReadAndAssert(files.B);

        Assert.False(File.Exists(capturePath));
        Assert.True(CVFileUtil.ReadCVRaw(capturePath, out CVCIEFile recovered));
        using (recovered) Assert.Equal(capture.Data, recovered.Data);
        Assert.Equal(new byte[] { 2, 3, 5 }, CVFileMetadata.Read(capturePath)["calibration"].Value);
        AssertPaths(hasDiskEntry ? [capturePath, files.B] : [capturePath]);
        Assert.Equal(before.CapacityBytes, CVFileReadCache.GetSnapshot().CapacityBytes);
        Assert.Equal(before.AllocationCount, CVFileReadCache.GetSnapshot().AllocationCount);
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

    [Fact]
    public void FailedMetadataWriteLeavesDiskAndCachedSnapshotsUnchangedUntilRetrySucceeds()
    {
        using CacheFiles files = new(maximumEntries: 1);
        const string property = "calibration";
        byte[] previousValue = [1, 2, 3], updatedValue = [7, 8, 9, 10, 11];
        files.ReadAndAssert(files.A);
        CVFileMetadata.SetProperty(files.A, property, 1, previousValue);
        byte[] previousBytes = File.ReadAllBytes(files.A);
        using Stream previousReader = CVFileReadCache.OpenRead(files.A);

        using (FileStream diskReader = File.Open(files.A, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<IOException>(() => CVFileMetadata.SetProperty(files.A, property, 2, updatedValue));
            Assert.Equal(previousBytes, File.ReadAllBytes(files.A));
            Assert.Equal((long)previousBytes.Length, CVFileReadCache.GetCachedLength(files.A));
            CVFileProperty cached = CVFileMetadata.Read(files.A)[property];
            Assert.Equal(1u, cached.Version);
            Assert.Equal(previousValue, cached.Value);
        }

        CVFileMetadata.SetProperty(files.A, property, 2, updatedValue);
        CVFileProperty updated = CVFileMetadata.Read(files.A)[property];
        Assert.Equal(2u, updated.Version);
        Assert.Equal(updatedValue, updated.Value);
        files.ReadAndAssert(files.A);
        using MemoryStream oldSnapshot = new();
        previousReader.CopyTo(oldSnapshot);
        Assert.Equal(previousBytes, oldSnapshot.ToArray());
        previousReader.Dispose();
        files.ReadAndAssert(files.B);
        AssertPaths(files.B);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void MetadataUpdateUsesTheCurrentDiskFileAfterExternalReplacement(bool resize, bool changeMetadata)
    {
        using CacheFiles files = new(maximumEntries: 1);
        const string previousKind = "old-only", updatedKind = "updated";
        string replacementKind = changeMetadata ? "new-only" : previousKind;
        files.ReadAndAssert(files.A);
        CVFileMetadata.SetProperty(files.A, previousKind, 3, [1, 2, 3]);
        DateTime previousWriteTime = File.GetLastWriteTimeUtc(files.A);
        byte[] previous = File.ReadAllBytes(files.A);
        using Stream previousReader = CVFileReadCache.OpenRead(files.A, populateCache: false);

        string replacementPath = Path.ChangeExtension(files.A, ".cvcie");
        using CVCIEFile replacement = CacheFiles.CreateRaw(97);
        if (resize)
        {
            replacement.Cols = 5;
            replacement.Data = Enumerable.Repeat((byte)97, 10).ToArray();
        }
        Assert.True(CVFileUtil.WriteCIEFile(replacementPath, replacement));
        byte[] headerAndPixels = File.ReadAllBytes(replacementPath);
        uint replacementVersion = changeMetadata ? 5u : 3u;
        byte[] replacementValue = changeMetadata ? [4, 5, 6] : [1, 2, 3];
        CVFileMetadata.SetProperty(replacementPath, replacementKind, replacementVersion, replacementValue);
        // Exercise the header and tail comparisons even when an external writer preserves the timestamp.
        DateTime replacementWriteTime = changeMetadata ? previousWriteTime : previousWriteTime.AddMinutes(1);
        File.SetLastWriteTimeUtc(replacementPath, replacementWriteTime);
        if (!resize) Assert.Equal(previous.LongLength, new FileInfo(replacementPath).Length);
        File.Move(replacementPath, files.A, overwrite: true);
        Assert.Equal(replacementWriteTime, File.GetLastWriteTimeUtc(files.A));

        CVFileMetadata.SetProperty(files.A, updatedKind, 7, [8, 9]);
        byte[] updated = File.ReadAllBytes(files.A);
        Assert.Equal(headerAndPixels, updated[..headerAndPixels.Length]);
        using (FileStream disk = File.OpenRead(files.A))
        {
            var properties = CVFileMetadata.ReadFromStream(disk);
            Assert.Equal(new[] { replacementKind, updatedKind }, properties.Keys.Order());
            Assert.Equal(replacementVersion, properties[replacementKind].Version);
            Assert.Equal(replacementValue, properties[replacementKind].Value);
            Assert.Equal(7u, properties[updatedKind].Version);
            Assert.Equal(new byte[] { 8, 9 }, properties[updatedKind].Value);
        }

        using MemoryStream oldSnapshot = new();
        previousReader.CopyTo(oldSnapshot);
        Assert.Equal(previous, oldSnapshot.ToArray());
        Assert.Null(CVFileReadCache.GetCachedLength(files.A));
        previousReader.Dispose();
        files.ReadAndAssert(files.A);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedSynchronousSavePreservesThePreviouslyPublishedSnapshot(bool previousMemoryOnly)
    {
        using CacheFiles files = new(maximumEntries: 1);
        byte previousValue = previousMemoryOnly ? (byte)61 : (byte)11;
        if (previousMemoryOnly)
        {
            using CVCIEFile memoryOnly = CacheFiles.CreateRaw(previousValue);
            Assert.True(CVFileUtil.WriteCVRaw(files.A, memoryOnly, CVFileSaveMode.MemoryOnly));
        }
        else files.ReadAndAssert(files.A);
        CVFileMetadata.SetProperty(files.A, "calibration", 1, [1, 2, 3]);
        byte[] diskBefore = File.ReadAllBytes(files.A);
        long? cachedLength = CVFileReadCache.GetCachedLength(files.A);
        using CVCIEFile replacement = CacheFiles.CreateRaw(97);

        using (FileStream diskReader = File.Open(files.A, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.False(CVFileUtil.WriteCVRaw(files.A, replacement));
            Assert.Equal(diskBefore, File.ReadAllBytes(files.A));
            Assert.True(CVFileUtil.ReadCVRaw(files.A, out CVCIEFile stillPublished));
            using (stillPublished) Assert.Equal(Enumerable.Repeat(previousValue, 6), stillPublished.Data);
            Assert.Equal(cachedLength, CVFileReadCache.GetCachedLength(files.A));
            Assert.Equal(new byte[] { 1, 2, 3 }, CVFileMetadata.Read(files.A)["calibration"].Value);
        }

        Assert.True(CVFileUtil.WriteCVRaw(files.A, replacement));
        files.ReadAndAssert(files.A);
        Assert.True(CVFileUtil.ReadCVRaw(files.A, out CVCIEFile saved));
        using (saved) Assert.Equal(Enumerable.Repeat((byte)97, 6), saved.Data);
        CVFileMetadata.SetProperty(files.A, "calibration", 2, [8, 9]);
        CVFileReadCache.Release();
        Assert.Equal(new byte[] { 8, 9 }, CVFileMetadata.Read(files.A)["calibration"].Value);
    }

    [Fact]
    public void FailedSynchronousStreamingSavePreservesExistingDiskBytesWithCacheDisabled()
    {
        using CacheFiles files = new(maximumEntries: 1);
        CVFileReadCache.IsEnabled = false;
        byte[] previous = File.ReadAllBytes(files.A);
        using CVCIEFile replacement = CacheFiles.CreateRaw(97);
        Assert.False(CVFileUtil.WriteCVRaw(files.A, replacement, replacement.Data.Length, stream =>
        {
            stream.WriteByte(97);
            throw new IOException("Simulated payload write failure.");
        }));
        Assert.Equal(previous, File.ReadAllBytes(files.A));
        Assert.Equal(new[] { files.A, files.B, files.C }.Order(), Directory.EnumerateFiles(Path.GetDirectoryName(files.A)!).Order());
    }

    [Theory]
    [InlineData(".cvcie", true, true)]
    [InlineData(".cvcie", false, false)]
    [InlineData(".cvsrc", true, false)]
    [InlineData(".cvsrc", false, true)]
    public void FailedSynchronousImageSaveNeverPublishesAnIncompleteNonRawFile(string extension, bool replaceExisting, bool callbackThrows)
    {
        using CacheFiles files = new(maximumEntries: 1);
        files.ReadAndAssert(files.A);
        string directory = Path.GetDirectoryName(files.A)!;
        string path = Path.ChangeExtension(files.B, extension);
        using CVCIEFile original = CacheFiles.CreateRaw(31);
        if (extension == ".cvcie")
        {
            original.Version = 2;
            original.Bpp = 32;
            original.Channels = 3;
            original.Exp = [10, 20, 30];
            original.Data = new byte[3 * 2 * 3 * sizeof(float)];
        }
        byte[]? before = null;
        if (replaceExisting)
        {
            Assert.True(CVFileUtil.WriteCIEFile(path, original));
            CVFileMetadata.SetProperty(path, "calibration", 1, [2, 3, 5]);
            before = File.ReadAllBytes(path);
        }
        string[] previousFiles = Directory.EnumerateFiles(directory).Order().ToArray();
        byte[] replacement = Enumerable.Repeat((byte)97, original.Data.Length).ToArray();

        Assert.False(CVFileUtil.WriteCIEFile(path, original, replacement.Length, stream =>
        {
            stream.WriteByte(replacement[0]);
            if (callbackThrows) throw new IOException("Simulated payload write failure.");
        }));

        if (before == null) Assert.False(File.Exists(path));
        else Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(previousFiles, Directory.EnumerateFiles(directory).Order());
        AssertPaths(files.A);

        Assert.True(CVFileUtil.WriteCIEFile(path, original, replacement.Length, stream => stream.Write(replacement)));
        Assert.True(CVFileUtil.Read(path, out CVCIEFile saved));
        using (saved) Assert.Equal(replacement, saved.Data);
        Assert.Equal(new[] { files.A, files.B, files.C, path }.Order(), Directory.EnumerateFiles(directory).Order());
        AssertPaths(files.A);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    public void FailedMemoryOnlySavePreservesPublishedSnapshotUntilRetrySucceeds(bool samePath, bool holdReader, bool callbackThrows)
    {
        using CacheFiles files = new(maximumEntries: 1);
        string directory = Path.GetDirectoryName(files.A)!;
        string previousPath = Path.Combine(directory, "previous.cvraw");
        string targetPath = samePath ? previousPath : Path.Combine(directory, "replacement.cvraw");
        using CVCIEFile original = CacheFiles.CreateRaw(61);
        Assert.True(CVFileUtil.WriteCVRaw(previousPath, original, CVFileSaveMode.MemoryOnly));
        CVFileMetadata.SetProperty(previousPath, "calibration", 1, [2, 3, 5]);
        byte[] previous;
        using (Stream snapshot = CVFileReadCache.OpenRead(previousPath, populateCache: false)) previous = ReadAll(snapshot);
        using Stream? borrowed = holdReader ? CVFileReadCache.OpenRead(previousPath, populateCache: false) : null;
        long previousCapacity = CVFileReadCache.GetSnapshot().CapacityBytes;

        using CVCIEFile replacement = CacheFiles.CreateRaw(97);
        Assert.False(CVFileUtil.WriteCVRaw(targetPath, replacement, replacement.Data.Length, stream =>
        {
            stream.WriteByte(97);
            if (callbackThrows) throw new IOException("Simulated memory-only payload write failure.");
        }, CVFileSaveMode.MemoryOnly));

        AssertPaths(previousPath);
        Assert.Equal(previousCapacity, CVFileReadCache.GetSnapshot().CapacityBytes);
        Assert.Equal(previous.LongLength, CVFileReadCache.GetCachedLength(previousPath));
        using (Stream current = CVFileReadCache.OpenRead(previousPath, populateCache: false)) Assert.Equal(previous, ReadAll(current));
        Assert.Equal(new byte[] { 2, 3, 5 }, CVFileMetadata.Read(previousPath)["calibration"].Value);

        Assert.True(CVFileUtil.WriteCVRaw(targetPath, replacement, CVFileSaveMode.MemoryOnly));
        Assert.True(CVFileUtil.ReadCVRaw(targetPath, out CVCIEFile saved));
        using (saved) Assert.Equal(replacement.Data, saved.Data);
        Assert.Equal(targetPath, Assert.Single(CVFileReadCache.GetSnapshot().Entries, entry => !entry.IsRetired).FilePath);
        if (borrowed != null) Assert.Equal(previous, ReadAll(borrowed));
        Assert.False(File.Exists(previousPath));
        Assert.False(File.Exists(targetPath));

        static byte[] ReadAll(Stream stream)
        {
            using MemoryStream copy = new();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
    }

    [Theory]
    [InlineData(FileAttributes.ReadOnly)]
    [InlineData(FileAttributes.Hidden)]
    [InlineData(FileAttributes.System)]
    public void SynchronousSavePreservesProtectedFileAndCachedSnapshot(FileAttributes attribute)
    {
        using CacheFiles files = new(maximumEntries: 1);
        files.ReadAndAssert(files.A);
        byte[] original = File.ReadAllBytes(files.A);
        FileAttributes attributes = File.GetAttributes(files.A);
        using CVCIEFile replacement = CacheFiles.CreateRaw(97);
        try
        {
            File.SetAttributes(files.A, attributes | attribute);
            Assert.False(CVFileUtil.WriteCVRaw(files.A, replacement));
            Assert.Equal(attributes | attribute, File.GetAttributes(files.A));
            Assert.Equal(original, File.ReadAllBytes(files.A));
            files.ReadAndAssert(files.A);
        }
        finally { File.SetAttributes(files.A, attributes); }
    }

    [Fact]
    public void SynchronousOverwritePreservesExistingCreationTimeAndDacl()
    {
        using CacheFiles files = new(maximumEntries: 1);
        files.ReadAndAssert(files.A);
        DateTime creation = new(2023, 10, 15, 10, 23, 45, DateTimeKind.Utc);
        File.SetCreationTimeUtc(files.A, creation);
        FileInfo target = new(files.A);
        FileSecurity security = target.GetAccessControl(AccessControlSections.Access);
        SecurityIdentifier unusedIdentity = new("S-1-5-21-723174703-1260426790-2378755966-4987");
        security.AddAccessRule(new FileSystemAccessRule(unusedIdentity, FileSystemRights.ReadData, AccessControlType.Deny));
        target.SetAccessControl(security);
        using CVCIEFile replacement = CacheFiles.CreateRaw(97);

        Assert.True(CVFileUtil.WriteCVRaw(files.A, replacement));

        Assert.Equal(creation, File.GetCreationTimeUtc(files.A));
        var explicitRules = target.GetAccessControl(AccessControlSections.Access)
            .GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier)).OfType<FileSystemAccessRule>();
        Assert.Contains(explicitRules, rule => rule.IdentityReference.Equals(unusedIdentity) && rule.AccessControlType == AccessControlType.Deny
            && rule.FileSystemRights.HasFlag(FileSystemRights.ReadData));
        Assert.NotNull(CVFileReadCache.GetCachedLength(files.A));
        files.ReadAndAssert(files.A);
        Assert.Equal(new[] { files.A, files.B, files.C }.Order(), Directory.EnumerateFiles(Path.GetDirectoryName(files.A)!).Order());
    }

    [Fact]
    public async Task SynchronousStreamingSaveKeepsExistingReadersUsableUntilCommit()
    {
        using CacheFiles files = new(maximumEntries: 2);
        files.ReadAndAssert(files.A);
        files.ReadAndAssert(files.B);
        byte[] original = File.ReadAllBytes(files.A);
        using CVCIEFile replacement = CacheFiles.CreateRaw(97);
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim resume = new(false);
        Task<bool> save = Task.Run(() => CVFileUtil.WriteCVRaw(files.A, replacement, replacement.Data.Length, stream =>
        {
            stream.Write(replacement.Data);
            entered.Set();
            resume.Wait();
        }));
        Stream? originalReader = null;
        Task? read = null;
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            read = Task.Run(() =>
            {
                originalReader = CVFileReadCache.OpenRead(files.A);
                files.AssertContent(files.A, originalReader);
                files.ReadAndAssert(files.B);
                Assert.Equal(original, File.ReadAllBytes(files.A));
            });
            await read.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            resume.Set();
            try
            {
                Assert.True(await save.WaitAsync(TimeSpan.FromSeconds(10)));
                if (read != null) await read.WaitAsync(TimeSpan.FromSeconds(10));
                if (originalReader != null)
                {
                    originalReader.Position = 0;
                    using MemoryStream copy = new();
                    originalReader.CopyTo(copy);
                    Assert.Equal(original, copy.ToArray());
                }
            }
            finally { originalReader?.Dispose(); }
        }
        Assert.True(CVFileUtil.ReadCVRaw(files.A, out CVCIEFile saved));
        using (saved) Assert.Equal(replacement.Data, saved.Data);
        Assert.Equal(0, CVFileReadCache.GetSnapshot().ActiveReaders);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousSaveDoesNotRefillCacheAfterRetirementDuringStaging(bool disableCache)
    {
        using CacheFiles files = new(maximumEntries: 1);
        files.ReadAndAssert(files.A);
        using CVCIEFile replacement = CacheFiles.CreateRaw(97);
        using ManualResetEventSlim entered = new(false);
        using ManualResetEventSlim resume = new(false);
        Task<bool> save = Task.Run(() => CVFileUtil.WriteCVRaw(files.A, replacement, replacement.Data.Length, stream =>
        {
            stream.Write(replacement.Data);
            entered.Set();
            resume.Wait();
        }));
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(10)));
            await Task.Run(() =>
            {
                if (disableCache)
                {
                    CVFileReadCache.IsEnabled = false;
                    CVFileReadCache.IsEnabled = true;
                }
                else CVFileReadCache.Release();
            }).WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            resume.Set();
            Assert.True(await save.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        Assert.Empty(CVFileReadCache.GetSnapshot().Entries);
        Assert.True(CVFileUtil.ReadCVRaw(files.A, out CVCIEFile saved));
        using (saved) Assert.Equal(replacement.Data, saved.Data);
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
            .Invoke(null, [path, true, openFile, null])!;

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
