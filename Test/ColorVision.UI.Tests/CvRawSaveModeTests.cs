using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.FileIO;
using System.IO;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class CvRawSaveModeTests
{
    [Fact]
    public void NodeDefaultsToSynchronousAndKeepsLegacySaveFilesOption()
    {
        var node = new LocalCameraNode();
        Assert.Equal(CVFileSaveMode.Synchronous, node.SaveMode);
        node.SaveAsynchronously = true;
        Assert.Equal(CVFileSaveMode.Asynchronous, node.SaveMode);
        node.SaveFiles = false;
        Assert.Equal(CVFileSaveMode.MemoryOnly, node.SaveMode);
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncSaveOwnsPixelsAndOrdersMetadataAfterCacheReleaseOrReplacement(bool replaceWithNextImage)
    {
        using CacheScope scope = new();
        using ManualResetEventSlim allowWrite = new(false);
        Task blocker = QueueWrite(() => allowWrite.Wait());
        Task? completion = null;
        TaskCompletionSource? nextSaved = null;
        string nextPath = scope.Path + ".next.cvraw";
        byte[] nextPixels = [6, 5, 4, 3, 2, 1];
        try
        {
            using CVCIEFile raw = CreateRaw();
            byte[] pixels = (byte[])raw.Data.Clone();
            Assert.True(CVFileUtil.WriteCVRaw(scope.Path, raw, CVFileSaveMode.Asynchronous));
            Array.Fill(raw.Data, (byte)99);
            CVFileMetadata.SetProperty(scope.Path, "color", 1, [1]);
            CVFileMetadata.SetProperty(scope.Path, "color", 2, [2, 3]);
            CVFileMetadata.SetProperty(scope.Path, "other", 1, [4]);
            AssertPixels(scope.Path, pixels);
            Assert.Equal(2u, CVFileMetadata.Read(scope.Path)["color"].Version);
            Assert.False(File.Exists(scope.Path));
            if (replaceWithNextImage)
            {
                nextSaved = new(TaskCreationOptions.RunContinuationsAsynchronously);
                Thread nextWriter = new(() =>
                {
                    try
                    {
                        using CVCIEFile next = CreateRaw();
                        next.Data = (byte[])nextPixels.Clone();
                        Assert.True(CVFileUtil.WriteCVRaw(nextPath, next, CVFileSaveMode.Asynchronous));
                        Assert.True(File.Exists(scope.Path));
                        Array.Fill(next.Data, (byte)88);
                        CVFileMetadata.SetProperty(nextPath, "color", 3, [9, 8]);
                        nextSaved.SetResult();
                    }
                    catch (Exception ex) { nextSaved.SetException(ex); }
                }) { IsBackground = true };
                long allocations = CVFileReadCache.GetSnapshot().AllocationCount;
                nextWriter.Start();
                // Observe the actual blocked thread; the timeout only bounds a hung test.
                Assert.True(SpinWait.SpinUntil(() => nextSaved.Task.IsCompleted
                    || (nextWriter.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(10)));
                Assert.False(nextSaved.Task.IsCompleted);
                Assert.Null(CVFileReadCache.GetCachedLength(nextPath));
                allowWrite.Set();
                await nextSaved.Task;
                AssertPixels(nextPath, nextPixels);
                Assert.Equal(allocations, CVFileReadCache.GetSnapshot().AllocationCount);
            }
            else CVFileReadCache.Release();
            Assert.Null(CVFileReadCache.GetCachedLength(scope.Path));
            completion = QueueWrite(() => { });
            allowWrite.Set();
            await completion;
            CVFileReadCache.Release();
            AssertPixels(scope.Path, pixels);
            Assert.Equal(new byte[] { 2, 3 }, CVFileMetadata.Read(scope.Path)["color"].Value);
            Assert.Equal(new byte[] { 4 }, CVFileMetadata.Read(scope.Path)["other"].Value);
            if (replaceWithNextImage)
            {
                AssertPixels(nextPath, nextPixels);
                Assert.Equal(new byte[] { 9, 8 }, CVFileMetadata.Read(nextPath)["color"].Value);
            }
        }
        finally
        {
            allowWrite.Set();
            if (nextSaved != null) await nextSaved.Task;
            await (completion ?? QueueWrite(() => { }));
            await blocker;
            File.Delete(nextPath);
        }
    }

    [Fact]
    public async Task SynchronousSaveWaitsForDiskQueue()
    {
        using CacheScope scope = new();
        using ManualResetEventSlim allowWrite = new(false);
        Task blocker = QueueWrite(() => allowWrite.Wait());
        TaskCompletionSource copied = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CVCIEFile raw = CreateRaw();
        Task<bool> save = Task.Run(() => CVFileUtil.WriteCVRaw(scope.Path, raw, raw.Data.Length, stream =>
        {
            stream.Write(raw.Data);
            copied.SetResult();
        }));
        try
        {
            await copied.Task;
            Assert.False(save.IsCompleted);
            Assert.False(File.Exists(scope.Path));
        }
        finally { allowWrite.Set(); }
        Assert.True(await save);
        await blocker;
        Assert.True(File.Exists(scope.Path));
    }

    private static Task QueueWrite(Action action) => (Task)typeof(CVFileReadCache)
        .GetMethod("QueueWrite", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [action])!;

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
