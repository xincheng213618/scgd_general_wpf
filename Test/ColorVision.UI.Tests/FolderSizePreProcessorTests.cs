using ColorVision.Engine.FlowProcessing.PreProcess;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FolderSizePreProcessorTests
{
    [Fact]
    public async Task CleanupRunsThroughBackgroundRunnerAndKeepsFlowSemantics()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ColorVision_FolderSizePreProcessor_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            string oldest = CreateFile(root, "oldest.cvraw", 100, DateTime.UtcNow.AddMinutes(-2));
            string newest = CreateFile(root, "newest.cvraw", 100, DateTime.UtcNow.AddMinutes(-1));
            string ignored = CreateFile(root, "ignored.txt", 500, DateTime.UtcNow.AddMinutes(-3));
            var runnerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseRunner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var processor = new FolderSizePreProcessor(async work =>
            {
                runnerStarted.TrySetResult();
                await releaseRunner.Task;
                return await Task.Run(work);
            });
            processor.Config.FolderPaths = [root];
            processor.Config.FileExtensions = ".cvraw";
            processor.Config.IncludeSubfolders = true;
            processor.Config.TriggerSizeBytes = 150;
            processor.Config.TargetSizeBytes = 100;

            Task<bool> cleanup = processor.PreProcess(new PreProcessContext());
            await runnerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(cleanup.IsCompleted);
            Assert.True(File.Exists(oldest));
            Assert.True(File.Exists(newest));

            releaseRunner.TrySetResult();

            Assert.True(await cleanup.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(File.Exists(oldest));
            Assert.True(File.Exists(newest));
            Assert.True(File.Exists(ignored));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ConcurrentCleanupRequestsAreSerialized()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ColorVision_FolderSizePreProcessor_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            int runnerCalls = 0;
            var firstRunnerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstRunner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var processor = new FolderSizePreProcessor(async work =>
            {
                if (Interlocked.Increment(ref runnerCalls) == 1)
                {
                    firstRunnerStarted.TrySetResult();
                    await releaseFirstRunner.Task;
                }

                return await Task.Run(work);
            });
            processor.Config.FolderPaths = [root];
            processor.Config.TriggerSizeBytes = 1024;
            processor.Config.TargetSizeBytes = 512;

            Task<bool> first = processor.PreProcess(new PreProcessContext());
            await firstRunnerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task<bool> second = processor.PreProcess(new PreProcessContext());

            Assert.Equal(1, Volatile.Read(ref runnerCalls));
            Assert.False(second.IsCompleted);

            releaseFirstRunner.TrySetResult();

            Assert.True(await first.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(await second.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(2, Volatile.Read(ref runnerCalls));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateFile(string root, string name, int length, DateTime lastWriteTimeUtc)
    {
        string path = Path.Combine(root, name);
        File.WriteAllBytes(path, new byte[length]);
        File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
        return path;
    }
}
