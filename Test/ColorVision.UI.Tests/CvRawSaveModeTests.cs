using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CvRawSaveModeTests
{
    [Fact]
    public void NodeDefaultsToSynchronousAndKeepsLegacySaveFilesOption()
    {
        var node = new LocalCameraNode();
        Assert.Equal(CVFileSaveMode.Synchronous, node.SaveMode);
        node.SaveFiles = false;
        Assert.Equal(CVFileSaveMode.MemoryOnly, node.SaveMode);
    }

    [Fact]
    public void DisabledFileCacheKeepsSavedPixelsOnDiskWithoutRetainingAFileSlot()
    {
        using CacheScope scope = new();
        CVFileReadCache.IsEnabled = false;
        using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = 3, Height = 2, SourceBpp = 8, Channels = 1, Exposure = [10]
        }, 6, 0);
        byte[] pixels = [1, 2, 3, 4, 5, 6];
        using (var lease = frame.Acquire()) System.Runtime.InteropServices.Marshal.Copy(pixels, 0, lease.RawPointer, pixels.Length);
        LocalFrameFileService.SaveCapture(frame, scope.Path);
        AssertPixels(scope.Path, pixels);
        Assert.Equal(0, CVFileReadCache.GetSnapshot().CapacityBytes);
        Assert.Equal(scope.Path, frame.CvRawFilePath);
    }

    [Fact]
    public void MemoryOnlyPublishesPixelsAndCompleteMetadataWithoutCreatingFile()
    {
        using CacheScope scope = new();
        using CVCIEFile raw = CreateRaw();
        Assert.True(CVFileUtil.WriteCVRaw(scope.Path, raw, CVFileSaveMode.MemoryOnly));
        byte[] pixels = (byte[])raw.Data.Clone();
        Array.Fill(raw.Data, (byte)99);
        CVFileMetadata.SetProperty(scope.Path, "color", 1, [1, 2]);
        using (Stream previous = CVFileReadCache.OpenRead(scope.Path))
        {
            byte[] original = ReadAll(previous);
            CVFileMetadata.SetProperty(scope.Path, "color", 2, [3, 4, 5]);
            CVFileMetadata.SetProperty(scope.Path, "other", 1, [6]);
            previous.Position = 0;
            Assert.Equal(original, ReadAll(previous));
        }
        AssertPixels(scope.Path, pixels);
        Assert.Equal(new byte[] { 3, 4, 5 }, CVFileMetadata.Read(scope.Path)["color"].Value);
        Assert.False(File.Exists(scope.Path));
        CVFileReadCache.Release();
        Assert.Null(CVFileReadCache.GetCachedLength(scope.Path));
        Assert.Throws<FileNotFoundException>(() => CVFileReadCache.OpenRead(scope.Path));
    }

    [Theory]
    [InlineData(true, CVFileSaveMode.Synchronous)]
    [InlineData(false, CVFileSaveMode.Synchronous)]
    [InlineData(true, (CVFileSaveMode)1)]
    [InlineData(false, (CVFileSaveMode)1)]
    public void SaveAndMetadataCompleteBeforeReturnIncludingRetiredMode(bool cacheEnabled, CVFileSaveMode mode)
    {
        using CacheScope scope = new();
        CVFileReadCache.IsEnabled = cacheEnabled;
        using CVCIEFile raw = CreateRaw();
        byte[] pixels = (byte[])raw.Data.Clone();
        int caller = Environment.CurrentManagedThreadId;
        int writer = -1;
        Assert.True(CVFileUtil.WriteCVRaw(scope.Path, raw, raw.Data.Length, stream =>
        {
            writer = Environment.CurrentManagedThreadId;
            if (!cacheEnabled) Assert.IsType<FileStream>(stream);
            stream.Write(raw.Data);
        }, mode));
        Assert.Equal(caller, writer);
        Array.Fill(raw.Data, (byte)99);
        CVFileMetadata.SetProperty(scope.Path, "color", 1, [1, 2, 3]);
        CVFileMetadata.SetProperty(scope.Path, "color", 2, [4, 5]);
        // Opening exclusively verifies that no disk writer remains after return.
        using (var file = new FileStream(scope.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.True(file.Length > pixels.Length);
        CVFileReadCache.Release();
        AssertPixels(scope.Path, pixels);
        Assert.Equal(new byte[] { 4, 5 }, CVFileMetadata.Read(scope.Path)["color"].Value);
        Assert.Equal(2u, CVFileMetadata.Read(scope.Path)["color"].Version);
    }

    private static CVCIEFile CreateRaw() => new()
        { Version = 1, Cols = 3, Rows = 2, Bpp = 8, Channels = 1, Exp = [10], Data = [1, 2, 3, 4, 5, 6] };

    private static void AssertPixels(string path, byte[] pixels)
    {
        Assert.True(CVFileUtil.Read(path, out CVCIEFile actual));
        using (actual) Assert.Equal(pixels, actual.Data);
    }

    private static byte[] ReadAll(Stream stream)
    {
        using MemoryStream memory = new();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed class CacheScope : IDisposable
    {
        private readonly bool enabled = CVFileReadCache.IsEnabled;
        private readonly int maximum = CVFileReadCache.MaximumEntries;
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cvraw-save-{Guid.NewGuid():N}.cvraw");
        public CacheScope()
        {
            CVFileReadCache.Release();
            CVFileReadCache.IsEnabled = true;
            CVFileReadCache.MaximumEntries = 1;
        }
        public void Dispose()
        {
            CVFileReadCache.Release();
            CVFileReadCache.MaximumEntries = maximum;
            CVFileReadCache.IsEnabled = enabled;
            File.Delete(Path);
        }
    }
}
