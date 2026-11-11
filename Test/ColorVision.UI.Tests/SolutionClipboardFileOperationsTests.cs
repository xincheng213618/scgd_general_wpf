using ColorVision.Solution.Explorer;
using System.Collections.Concurrent;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class SolutionClipboardFileOperationsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FolderPathsWithTrailingSeparatorsKeepTheirNameInTheTarget(bool move)
    {
        using var files = new CopyFiles();
        string destination = Path.Combine(files.Target, "source");
        if (!move)
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "existing.txt"), "keep");
        }

        string sourceWithSeparator = files.Source + Path.DirectorySeparatorChar;
        var result = SolutionClipboardFileOperations.Execute([sourceWithSeparator], files.Target, isMove: move);

        Assert.True(result.IsComplete, string.Join(Environment.NewLine, result.Failures.Select(failure => failure.Message)));
        Assert.Equal(!move, Directory.Exists(files.Source));
        if (!move)
        {
            Assert.Equal("keep", File.ReadAllText(Path.Combine(destination, "existing.txt")));
            destination = Path.Combine(files.Target, "source - Copy (1)");
        }
        Assert.Equal("original", File.ReadAllText(Path.Combine(destination, "data.txt")));
    }

    [Fact]
    public async Task FailedFolderCopyNeverPublishesTheDestination()
    {
        using var files = new CopyFiles();
        using var locked = File.Open(Path.Combine(files.Source, "data.txt"), FileMode.Open, FileAccess.Read, FileShare.None);
        using var watcher = new FileSystemWatcher(files.Target) { NotifyFilter = NotifyFilters.DirectoryName };
        var created = new ConcurrentQueue<string>();
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        const string marker = "changes-delivered";
        watcher.Created += (_, args) =>
        {
            if (args.Name != null) created.Enqueue(args.Name);
            if (args.Name == marker) delivered.TrySetResult();
        };
        watcher.EnableRaisingEvents = true;

        var result = SolutionClipboardFileOperations.Execute([files.Source], files.Target, isMove: false);
        Directory.CreateDirectory(Path.Combine(files.Target, marker));
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(result.IsComplete);
        Assert.Single(result.Failures);
        Assert.DoesNotContain("source", created);
        Assert.Equal([marker], Directory.EnumerateFileSystemEntries(files.Target).Select(Path.GetFileName).ToArray());
        Assert.True(File.Exists(Path.Combine(files.Source, "data.txt")));
    }

    [Fact]
    public async Task ConcurrentFolderCopiesPublishCompleteNumberedDirectories()
    {
        using var files = new CopyFiles();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<SolutionFileOperationResult>[] copies = Enumerable.Range(0, 4).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            return SolutionClipboardFileOperations.Execute([files.Source], files.Target, isMove: false);
        })).ToArray();
        start.SetResult();

        Assert.All(await Task.WhenAll(copies), result => Assert.True(result.IsComplete));
        string[] destinations = Directory.GetDirectories(files.Target).OrderBy(path => path, StringComparer.Ordinal).ToArray();
        Assert.Equal(["source", "source - Copy (1)", "source - Copy (2)", "source - Copy (3)"], destinations.Select(Path.GetFileName));
        Assert.All(destinations, destination => Assert.Equal("original", File.ReadAllText(Path.Combine(destination, "data.txt"))));
    }

    private sealed class CopyFiles : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("ColorVision-FolderCopy-");

        public string Source { get; }
        public string Target { get; }

        public CopyFiles()
        {
            Source = _root.CreateSubdirectory("source").FullName;
            Target = _root.CreateSubdirectory("target").FullName;
            File.WriteAllText(Path.Combine(Source, "data.txt"), "original");
        }

        public void Dispose() => _root.Delete(recursive: true);
    }
}
