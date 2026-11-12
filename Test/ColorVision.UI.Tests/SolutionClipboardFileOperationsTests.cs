using ColorVision.Solution;
using ColorVision.Solution.Explorer;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetryingAPartialCutDoesNotMoveAReplacementAtAnAlreadyMovedPath(bool clipboardBusy)
    {
        WpfTestHost.Invoke(() =>
        {
            using var files = new CopyFiles();
            string first = Path.Combine(files.Source, "data.txt");
            string second = Path.Combine(files.Source, "other.txt");
            string firstTarget = Path.Combine(files.Target, "data.txt");
            string secondTarget = Path.Combine(files.Target, "other.txt");
            File.WriteAllText(second, "second");
            File.WriteAllText(secondTarget, "collision");
            IDataObject? clipboard = TreeViewControl.CreatePathDataObject([first, second], isCut: true);

            var partial = Paste(files.Target);
            Assert.Equal(1, partial.SucceededCount);
            Assert.Equal(second, Assert.Single(partial.Failures).SourcePath);
            Assert.Equal("original", File.ReadAllText(firstTarget));
            if (!clipboardBusy)
                Assert.Equal([second], Assert.IsType<string[]>(clipboard!.GetData(DataFormats.FileDrop)));
            clipboardBusy = false;

            File.WriteAllText(first, "new file");
            string retryTarget = Directory.CreateDirectory(Path.Combine(files.Target, "retry")).FullName;
            var retry = Paste(retryTarget);

            Assert.True(retry.IsComplete);
            Assert.True(File.Exists(first));
            Assert.Equal(1, retry.RequestedCount);
            Assert.Null(clipboard);
            Assert.Equal("new file", File.ReadAllText(first));
            Assert.Equal("original", File.ReadAllText(firstTarget));
            Assert.Equal("collision", File.ReadAllText(secondTarget));
            Assert.Equal("second", File.ReadAllText(Path.Combine(retryTarget, "other.txt")));

            SolutionFileOperationResult Paste(string target)
            {
                IDataObject snapshot = Assert.IsAssignableFrom<IDataObject>(clipboard);
                string[] paths = Assert.IsType<string[]>(snapshot.GetData(DataFormats.FileDrop));
                return TreeViewControl.PastePaths(paths, target, true, snapshot, () => clipboard, value =>
                {
                    if (clipboardBusy) throw new ExternalException("Clipboard busy");
                    clipboard = value;
                });
            }
        });
    }

    [Fact]
    public void FinishingACutKeepsANewerClipboardWithTheSamePaths()
    {
        WpfTestHost.Invoke(() =>
        {
            using var files = new CopyFiles();
            string source = Path.Combine(files.Source, "data.txt");
            IDataObject captured = TreeViewControl.CreatePathDataObject([source], isCut: true);
            IDataObject replacement = TreeViewControl.CreatePathDataObject([source], isCut: true);
            IDataObject? clipboard = replacement;

            var result = TreeViewControl.PastePaths([source], files.Target, true, captured, () => clipboard, value => clipboard = value);

            Assert.True(result.IsComplete);
            Assert.Same(replacement, clipboard);
        });
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
