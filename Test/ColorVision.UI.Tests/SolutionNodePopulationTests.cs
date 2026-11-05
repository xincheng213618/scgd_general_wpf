using ColorVision.Solution.Explorer;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class SolutionNodePopulationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PopulationPreservesEntryOrderAndRefreshReadsTheCurrentDirectory(bool useCache)
    {
        WpfTestHost.Invoke(() =>
        {
            using PopulationFixture fixture = new();
            fixture.CreateDirectory("folder10");
            fixture.CreateDirectory("folder2");
            string removedPath = fixture.WriteFile("file10.txt");
            fixture.WriteFile("file2.txt");
            fixture.WriteFile("Workspace.cvsln.bak");
            using SolutionCache? cache = useCache ? new SolutionCache(fixture.SolutionPath) : null;
            if (cache != null)
            {
                foreach (string path in Directory.GetDirectories(fixture.Root)) cache.AddDirectory(path, fixture.Root);
                foreach (string path in Directory.GetFiles(fixture.Root)) cache.AddFile(path, fixture.Root);
                Assert.True(cache.ValidateDirectory(fixture.Root));
            }
            SolutionExplorer root = fixture.CreateRoot();
            var originalCollection = root.VisualChildren;
            string[] expected = cache != null
                ? cache.GetChildren(fixture.Root)
                    .Where(entry => entry.IsDirectory ? Directory.Exists(entry.FullPath) : File.Exists(entry.FullPath) && !SolutionNodeFactory.IsInternalFile(entry.Name))
                    .Select(entry => entry.FullPath).ToArray()
                : fixture.VisiblePaths();
            using QueuedContext context = new();

            context.Complete(SolutionNodeFactory.PopulateChildren(root, new DirectoryInfo(fixture.Root), cache));

            Assert.Equal(expected, root.VisualChildren.Select(node => node.FullPath));
            Assert.Same(originalCollection, root.VisualChildren);
            Assert.All(root.VisualChildren, node => Assert.Same(root, node.Parent));
            SolutionNode[] firstPopulation = root.VisualChildren.ToArray();
            context.Complete(SolutionNodeFactory.PopulateChildren(root, new DirectoryInfo(fixture.Root), cache));
            Assert.Equal(firstPopulation, root.VisualChildren);

            foreach (IDisposable child in root.VisualChildren.OfType<IDisposable>()) child.Dispose();
            root.VisualChildren.Clear();
            File.Delete(removedPath);
            fixture.WriteFile("fresh.txt");

            // Refresh intentionally bypasses the old persisted listing.
            context.Complete(SolutionNodeFactory.PopulateChildren(root, new DirectoryInfo(fixture.Root), cache: null));

            Assert.Equal(fixture.VisiblePaths(), root.VisualChildren.Select(node => node.FullPath));
            Assert.Same(originalCollection, root.VisualChildren);
            Assert.All(root.VisualChildren, node => Assert.Same(root, node.Parent));
            foreach (IDisposable child in root.VisualChildren.OfType<IDisposable>()) child.Dispose();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WatcherNodesAddedAtAYieldKeepTheirIdentityAndPosition(bool useProjectDirectory)
    {
        WpfTestHost.Invoke(() =>
        {
            using PopulationFixture fixture = new();
            for (int index = 0; index < 60; index++)
            {
                if (useProjectDirectory) fixture.CreateDirectory($"folder{index:D2}");
                else fixture.WriteFile($"file{index:D2}.txt");
            }
            string[] initialPaths = fixture.VisiblePaths();
            SolutionExplorer root = fixture.CreateRoot();
            var originalCollection = root.VisualChildren;
            using QueuedContext context = new();

            Task loading = SolutionNodeFactory.PopulateChildren(root, new DirectoryInfo(fixture.Root));

            Assert.False(loading.IsCompleted);
            string[] attachedBeforeYield = root.VisualChildren.Select(node => node.FullPath).ToArray();
            Assert.NotEmpty(attachedBeforeYield);
            string duplicatePath = initialPaths[^1];
            Assert.DoesNotContain(root.VisualChildren, node => string.Equals(node.FullPath, duplicatePath, StringComparison.OrdinalIgnoreCase));
            SolutionNode watcherNode = useProjectDirectory
                ? new ProjectNode(new ProjectDefinition("test", "Retained project", "1.0", new FileInfo(Path.Combine(duplicatePath, "test.cvproj"))))
                : new SolutionNode();
            watcherNode.FullPath = duplicatePath.ToUpperInvariant();
            watcherNode.IsSelected = true;
            root.AddChild(watcherNode);
            string latePath = fixture.WriteFile("late.txt");
            var lateNode = new SolutionNode { FullPath = latePath };
            root.AddChild(lateNode);

            context.Complete(loading);

            string[] expected = attachedBeforeYield
                .Concat(new[] { watcherNode.FullPath, latePath })
                .Concat(initialPaths.Skip(attachedBeforeYield.Length).Where(path => !string.Equals(path, duplicatePath, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            Assert.Equal(expected, root.VisualChildren.Select(node => node.FullPath));
            Assert.Same(originalCollection, root.VisualChildren);
            Assert.Same(watcherNode, Assert.Single(root.VisualChildren, node => string.Equals(node.FullPath, duplicatePath, StringComparison.OrdinalIgnoreCase)));
            Assert.Same(lateNode, Assert.Single(root.VisualChildren, node => node.FullPath == latePath));
            Assert.True(watcherNode.IsSelected);
            Assert.All(root.VisualChildren, node => Assert.Same(root, node.Parent));

            SolutionNode loadedNode = root.VisualChildren[0];
            root.AddChild(new SolutionNode { FullPath = loadedNode.FullPath.ToUpperInvariant() });
            Assert.Same(loadedNode, root.VisualChildren[0]);
            Assert.Equal(expected.Length, root.VisualChildren.Count);
            string afterLoadPath = fixture.WriteFile("after-load.txt");
            SolutionNodeFactory.AddFileNode(root, new FileInfo(afterLoadPath));
            Assert.Equal(afterLoadPath, root.VisualChildren[^1].FullPath);
            Assert.Same(root, root.VisualChildren[^1].Parent);
            foreach (IDisposable child in root.VisualChildren.OfType<IDisposable>()) child.Dispose();
        });
    }

    [Fact]
    public void NonRootNodesKeepTheirAddChildOverrideAndLazyPlaceholder()
    {
        WpfTestHost.Invoke(() =>
        {
            using PopulationFixture fixture = new();
            fixture.WriteFile("visible.txt");
            var parent = new RecordingNode();
            using QueuedContext context = new();

            context.Complete(SolutionNodeFactory.PopulateChildren(parent, new DirectoryInfo(fixture.Root)));

            Assert.Equal(1, parent.AddCalls);
            parent.VisualChildren.Clear();
            var placeholder = new LazyLoadingNode();
            parent.VisualChildren.Add(placeholder);
            context.Complete(SolutionNodeFactory.PopulateChildren(parent, new DirectoryInfo(fixture.Root)));
            Assert.Same(placeholder, Assert.Single(parent.VisualChildren));
            Assert.Equal(1, parent.AddCalls);
        });
    }

    private sealed class RecordingNode : SolutionNode
    {
        public int AddCalls { get; private set; }

        public override void AddChild(SolutionNode node)
        {
            AddCalls++;
            base.AddChild(node);
        }
    }

    private sealed class QueuedContext : SynchronizationContext, IDisposable
    {
        private readonly SynchronizationContext? _previous = Current;
        private readonly Queue<Action> _callbacks = new();

        public QueuedContext() => SetSynchronizationContext(this);

        public override void Post(SendOrPostCallback callback, object? state) => _callbacks.Enqueue(() => callback(state));

        public void Complete(Task task)
        {
            while (!task.IsCompleted)
            {
                Assert.True(_callbacks.TryDequeue(out Action? callback), "Population must resume through its captured yield context.");
                callback!();
            }
            task.GetAwaiter().GetResult();
            Assert.Empty(_callbacks);
        }

        public void Dispose() => SetSynchronizationContext(_previous);
    }

    private sealed class PopulationFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "ColorVisionSolutionPopulationTests", Guid.NewGuid().ToString("N"));
        public string SolutionPath => Path.Combine(Root, "Workspace.cvsln");

        public PopulationFixture() => Directory.CreateDirectory(Root);

        public SolutionExplorer CreateRoot()
        {
            // Populate the real root path without starting watchers, config
            // singletons or a background rebuild against a user's workspace.
            var root = (SolutionExplorer)RuntimeHelpers.GetUninitializedObject(typeof(SolutionExplorer));
            root.VisualChildren = new ObservableCollection<SolutionNode>();
            root.FullPath = Root;
            typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.Config))!.SetValue(root, new SolutionConfig());
            typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.DirectoryInfo))!.SetValue(root, new DirectoryInfo(Root));
            return root;
        }

        public string WriteFile(string name)
        {
            string path = Path.Combine(Root, name);
            File.WriteAllText(path, "fixture");
            return path;
        }

        public void CreateDirectory(string name) => Directory.CreateDirectory(Path.Combine(Root, name));

        public string[] VisiblePaths() => Directory.GetDirectories(Root)
            .Where(path => (File.GetAttributes(path) & FileAttributes.Hidden) == 0)
            .Concat(Directory.GetFiles(Root).Where(path => !SolutionNodeFactory.IsInternalFile(Path.GetFileName(path))))
            .ToArray();

        public void Dispose()
        {
            string path = Path.GetFullPath(Root);
            string allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVisionSolutionPopulationTests")) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected test workspace path.");
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }
}
