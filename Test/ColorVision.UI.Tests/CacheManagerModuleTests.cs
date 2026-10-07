using ColorVision.Core;
using ColorVision.Engine.Services.Caches;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using cvColorVision;
using System.IO;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class CacheManagerModuleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleasingRawBuffersClearsOnlyIdleMemoryAndKeepsThePoolUsable(bool releaseAll)
    {
        using CacheFixture fixture = new();
        CVFileReadCacheSnapshot imageBefore = CVFileReadCache.GetSnapshot();
        CalibrationSharedCacheEntry calibrationBefore = fixture.CalibrationEntry;
        using var firstPool = new LocalCameraRawBufferPool();
        using var secondPool = new LocalCameraRawBufferPool();
        using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 64, 0, firstPool);
        using var lease = frame.Acquire();
        Marshal.WriteByte(lease.RawPointer, 63, 37);
        frame.Dispose();
        using (var idle = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 64, 0, firstPool)) { }
        using (var idle = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 128, 0, secondPool)) { }
        CameraRawBufferCacheModule module = new(() => [("camera-1", firstPool), ("camera-2", secondPool)]);
        Assert.IsType<CameraRawBufferCacheModule>(CacheManagerService.GetById(module.Id));
        CacheModuleSnapshot before = module.GetSnapshot();
        Assert.Equal(192UL, before.MemoryBytes);
        Assert.Equal(new[] { "camera-1", "camera-2" }, before.Entries.Select(entry => entry.Name));
        Assert.True(before.CanToggle);
        Assert.True(before.IsEnabled);

        CacheModuleReleaseResult result = releaseAll
            ? Assert.Single(await CacheManagerService.ReleaseAllAsync([module]))
            : await CacheManagerService.ReleaseAsync(module);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(before.MemoryBytes, result.ReleasedBytes);
        Assert.Equal(0UL, module.GetSnapshot().MemoryBytes);
        Assert.Empty(module.GetSnapshot().Entries);
        Assert.Equal(37, Marshal.ReadByte(lease.RawPointer, 63));
        Assert.Equal(0UL, (await module.ReleaseAsync()).ReleasedBytes);
        CVFileReadCacheSnapshot imageAfter = CVFileReadCache.GetSnapshot();
        Assert.Equal(imageBefore.FilePath, imageAfter.FilePath);
        Assert.Equal(imageBefore.CapacityBytes, imageAfter.CapacityBytes);
        Assert.Equal(imageBefore.AllocationCount, imageAfter.AllocationCount);
        Assert.Equal(calibrationBefore, fixture.CalibrationEntry);
        fixture.AssertFilesUnchanged();

        // The cleared pool can allocate immediately; the older leased image is still independent.
        using (var next = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 64, 0, firstPool))
        using (var nextLease = next.Acquire())
        {
            Assert.NotEqual(lease.RawPointer, nextLease.RawPointer);
            Marshal.WriteByte(nextLease.RawPointer, 63, 41);
            Assert.Equal(37, Marshal.ReadByte(lease.RawPointer, 63));
            lease.Dispose();
            Assert.Equal(41, Marshal.ReadByte(nextLease.RawPointer, 63));
        }
        Assert.Equal(64UL, module.GetSnapshot().MemoryBytes);
        Assert.Equal(64UL, (await module.ReleaseAsync()).ReleasedBytes);
        using (var next = LocalFlowFrame.Allocate(new LocalFrameMetadata(), 128, 0, secondPool)) { }
        Assert.Equal(128UL, module.GetSnapshot().MemoryBytes);
    }

    [Fact]
    public async Task SnapshotFailureKeepsHealthyModuleAvailableAndIdentifiesFailedModule()
    {
        TestCacheModule failed = new("failed") { SnapshotError = new InvalidOperationException("snapshot unavailable") };
        TestCacheModule healthy = new("healthy");
        CacheModuleSnapshot expected = healthy.GetSnapshot();

        IReadOnlyList<CacheModuleSnapshot> snapshots = await CacheManagerService.ReadSnapshotsAsync([failed, healthy]);

        Assert.Equal(["failed", "healthy"], snapshots.Select(snapshot => snapshot.Id));
        Assert.Equal("snapshot unavailable", snapshots[0].Error);
        Assert.Empty(snapshots[0].Entries);
        Assert.False(snapshots[0].CanToggle);
        Assert.Equal(expected, snapshots[1]);
        Assert.Null(snapshots[1].Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseAllContinuesAfterSynchronousOrAsynchronousModuleFailure(bool asynchronousFailure)
    {
        TestCacheModule failed = new("failed")
        {
            ReleaseError = new InvalidOperationException("release unavailable"),
            AsynchronousReleaseFailure = asynchronousFailure,
        };
        TestCacheModule healthy = new("healthy");

        IReadOnlyList<CacheModuleReleaseResult> results = await CacheManagerService.ReleaseAllAsync([failed, healthy]);

        Assert.Equal(["failed", "healthy"], results.Select(result => result.ModuleId));
        Assert.False(results[0].Succeeded);
        Assert.Equal(0UL, results[0].ReleasedBytes);
        Assert.Contains("release unavailable", results[0].Message);
        Assert.True(results[1].Succeeded);
        Assert.Equal(64UL, results[1].ReleasedBytes);
        Assert.Equal(1, failed.ReleaseCount);
        Assert.Equal(1, healthy.ReleaseCount);
    }

    [Fact]
    public async Task ReleasingImageFileModulePreservesCalibrationOwnersAndOriginalFiles()
    {
        using CacheFixture fixture = new();
        CalibrationSharedCacheEntry calibration = fixture.CalibrationEntry;
        ImageFileCacheModule module = new();
        CacheModuleSnapshot before = module.GetSnapshot();
        Assert.True(before.MemoryBytes > 0);
        Assert.Equal(fixture.ImagePath, Assert.Single(before.Entries).FilePath);

        CacheModuleReleaseResult result = await CacheManagerService.ReleaseAsync(module);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(before.MemoryBytes, result.ReleasedBytes);
        Assert.Equal(0UL, module.GetSnapshot().MemoryBytes);
        Assert.Empty(module.GetSnapshot().Entries);
        Assert.Equal(calibration, fixture.CalibrationEntry);
        Assert.True(fixture.CalibrationEntry.ActiveOwnerCount > 0);
        fixture.AssertFilesUnchanged();
    }

    [Fact]
    public async Task ReleasingCalibrationSharedCachePreservesImageFileSlotAndOriginalFiles()
    {
        using CacheFixture fixture = new();
        fixture.ReleaseCalibrationOwner();
        Assert.Equal(0U, fixture.CalibrationEntry.ActiveOwnerCount);
        CVFileReadCacheSnapshot before = CVFileReadCache.GetSnapshot();

        LocalCalibrationCacheReleaseSummary result = await LocalCalibrationCacheService.ReleaseCalibrationAsync(Array.Empty<DeviceCamera>());

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(0, result.ImageFileBytesReleased);
        Assert.DoesNotContain(LocalCalibrationCacheManager.GetEntries().Entries, entry => entry.FilePath == fixture.CalibrationPath);
        CVFileReadCacheSnapshot after = CVFileReadCache.GetSnapshot();
        Assert.Equal(before.FilePath, after.FilePath);
        Assert.Equal(before.CapacityBytes, after.CapacityBytes);
        Assert.Equal(before.ContentBytes, after.ContentBytes);
        fixture.AssertFilesUnchanged();
    }

    [Fact]
    public async Task ExistingReleaseAllEntryPointStillReleasesBothCachesWithoutDeletingFiles()
    {
        using CacheFixture fixture = new();
        fixture.ReleaseCalibrationOwner();
        CVFileReadCacheSnapshot before = CVFileReadCache.GetSnapshot();

        LocalCalibrationCacheReleaseSummary result = await LocalCalibrationCacheService.ReleaseAllAsync(Array.Empty<DeviceCamera>());

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(before.CapacityBytes, result.ImageFileBytesReleased);
        Assert.True(result.NativeRelease!.ReleasedEntryCount > 0);
        Assert.Equal(0, CVFileReadCache.GetSnapshot().CapacityBytes);
        Assert.DoesNotContain(LocalCalibrationCacheManager.GetEntries().Entries, entry => entry.FilePath == fixture.CalibrationPath);
        fixture.AssertFilesUnchanged();
    }

    private sealed class TestCacheModule(string id) : ICacheModule
    {
        private readonly CacheModuleSnapshot snapshot = new(id, id, $"{id} cache", 64, 1, 3, 1, 0, true, true,
            "ready", [new CacheEntrySnapshot("item", "file.cvraw", 32, 64, 3, 0, "ready")]);

        public string Id => id;
        public string Name => id;
        public string Description => $"{id} cache";
        public Exception? SnapshotError { get; init; }
        public Exception? ReleaseError { get; init; }
        public bool AsynchronousReleaseFailure { get; init; }
        public int ReleaseCount { get; private set; }

        public CacheModuleSnapshot GetSnapshot() => SnapshotError == null ? snapshot : throw SnapshotError;

        public Task<CacheModuleReleaseResult> ReleaseAsync()
        {
            ReleaseCount++;
            if (ReleaseError != null)
            {
                if (AsynchronousReleaseFailure) return Task.FromException<CacheModuleReleaseResult>(ReleaseError);
                throw ReleaseError;
            }
            return Task.FromResult(new CacheModuleReleaseResult(Id, 64, "released", true));
        }

        public void SetEnabled(bool enabled) => throw new NotSupportedException();
    }

    private sealed class CacheFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"cv-cache-modules-{Guid.NewGuid():N}");
        private readonly bool previousEnabled = CVFileReadCache.IsEnabled;
        private readonly bool previousRawBufferEnabled = LocalCameraRawBufferPool.IsCacheEnabled;
        private readonly byte[] imageFile;
        private readonly byte[] calibrationFile;
        private IntPtr calibrationContext;

        public string ImagePath { get; }
        public string CalibrationPath { get; }
        public CalibrationSharedCacheEntry CalibrationEntry => Assert.Single(LocalCalibrationCacheManager.GetEntries().Entries,
            entry => string.Equals(entry.FilePath, CalibrationPath, StringComparison.OrdinalIgnoreCase));

        public CacheFixture()
        {
            Directory.CreateDirectory(root);
            ImagePath = Path.Combine(root, "image.cvraw");
            CalibrationPath = Path.Combine(root, "dark.dat");
            CVFileReadCache.Release();
            CVFileReadCache.IsEnabled = true;
            LocalCameraRawBufferPool.IsCacheEnabled = true;
            LocalCalibrationCacheManager.ClearShared();
            try
            {
                using CVCIEFile raw = new() { Version = 1, Cols = 3, Rows = 2, Bpp = 8, Channels = 1, Exp = [10], Data = [1, 2, 3, 4, 5, 6] };
                Assert.True(CVFileUtil.WriteCVRaw(ImagePath, raw));
                using (Stream read = CVFileReadCache.OpenRead(ImagePath)) Assert.True(read.ReadByte() >= 0);
                imageFile = File.ReadAllBytes(ImagePath);
                File.WriteAllText(CalibrationPath, """{"bpp":16,"Texp_x":1.0,"DarkNoiseRatio":2.0}""");
                calibrationFile = File.ReadAllBytes(CalibrationPath);
                Assert.Equal(OpenCVCalibration.CalibrationOk, OpenCVCalibration.M_CalibrationCreate(out calibrationContext));
                Assert.Equal(OpenCVCalibration.CalibrationOk,
                    OpenCVCalibration.M_CalibrationLoadFileW(calibrationContext, (int)CalibrationType.DarkNoise, CalibrationPath));
                Assert.True(CalibrationEntry.ActiveOwnerCount > 0);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void ReleaseCalibrationOwner()
        {
            if (calibrationContext == IntPtr.Zero) return;
            Assert.Equal(OpenCVCalibration.CalibrationOk, OpenCVCalibration.M_CalibrationDestroy(calibrationContext));
            calibrationContext = IntPtr.Zero;
        }

        public void AssertFilesUnchanged()
        {
            Assert.Equal(imageFile, File.ReadAllBytes(ImagePath));
            Assert.Equal(calibrationFile, File.ReadAllBytes(CalibrationPath));
        }

        public void Dispose()
        {
            ReleaseCalibrationOwner();
            LocalCalibrationCacheManager.ClearShared();
            CVFileReadCache.Release();
            CVFileReadCache.IsEnabled = previousEnabled;
            LocalCameraRawBufferPool.IsCacheEnabled = previousRawBufferEnabled;
            foreach (string file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }
}
