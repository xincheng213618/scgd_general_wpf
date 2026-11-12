using ColorVision.Solution.Explorer;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

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

    [Fact]
    public void RenamedAndRemovedChildrenUpdateDeduplicationAcrossYields()
    {
        WpfTestHost.Invoke(() =>
        {
            using PopulationFixture fixture = new();
            for (int index = 0; index < 160; index++) fixture.WriteFile($"file{index:D3}.txt");
            string[] paths = fixture.VisiblePaths();
            SolutionExplorer root = fixture.CreateRoot();
            using QueuedContext context = new();

            Task loading = SolutionNodeFactory.PopulateChildren(root, new DirectoryInfo(fixture.Root));

            string renamedPath = paths[70];
            string removedPath = paths[140];
            SolutionNode renamed = root.VisualChildren[0];
            var transient = new SolutionNode { FullPath = removedPath };
            root.AddChild(transient);
            renamed.FullPath = renamedPath.ToUpperInvariant();

            context.RunNext();

            Assert.False(loading.IsCompleted);
            Assert.Same(renamed, Assert.Single(root.VisualChildren,
                node => string.Equals(node.FullPath, renamedPath, StringComparison.OrdinalIgnoreCase)));
            root.RemoveChild(transient);
            context.Complete(loading);

            Assert.IsType<FileNode>(Assert.Single(root.VisualChildren, node => node.FullPath == removedPath));
            Assert.DoesNotContain(transient, root.VisualChildren);
        });
    }

    [Fact]
    public void RefreshDuringRootPopulationDoesNotReplayADeletedEntryFromTheOldSnapshot()
    {
        WpfTestHost.Invoke(() =>
        {
            using PopulationFixture fixture = new();
            for (int index = 0; index < 60; index++) fixture.WriteFile($"file{index:D3}.txt");
            SolutionExplorer root = fixture.CreateRoot();
            typeof(SolutionExplorer).GetField("_projectChangedDebounceTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(root, new DispatcherTimer());
            using QueuedContext context = new();

            Task original = SolutionNodeFactory.PopulateChildren(root, new DirectoryInfo(fixture.Root));
            Assert.False(original.IsCompleted);
            string removed = fixture.VisiblePaths()[^1];
            Assert.DoesNotContain(root.VisualChildren, node => node.FullPath == removed);
            File.Delete(removed);

            root.Refresh();
            context.Drain();
            original.GetAwaiter().GetResult();

            Assert.Equal(fixture.VisiblePaths(), root.VisualChildren.Select(node => node.FullPath));
        });
    }

    [Fact]
    public void ClosingDuringRootPopulationKeepsTheDisposedTreeEmpty()
    {
        WpfTestHost.Invoke(() =>
        {
            using PopulationFixture fixture = new();
            for (int index = 0; index < 60; index++) fixture.WriteFile($"file{index:D3}.txt");
            SolutionExplorer root = fixture.CreateRoot();
            using QueuedContext context = new();

            Task loading = SolutionNodeFactory.PopulateChildren(root, new DirectoryInfo(fixture.Root));
            Assert.False(loading.IsCompleted);
            Assert.NotEmpty(root.VisualChildren);

            root.Dispose();
            context.Complete(loading);

            Assert.Empty(root.VisualChildren);
            context.Complete(SolutionNodeFactory.PopulateChildren(root, new DirectoryInfo(fixture.Root)));
            Assert.Empty(root.VisualChildren);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RefreshRestoresLateFolderExpansionBeforeNotifyingCompletion(bool refreshAgain)
    {
        WpfTestHost.Invoke(() =>
        {
            using PopulationFixture fixture = new();
            for (int index = 0; index < 60; index++) fixture.CreateDirectory($"folder{index:D3}");
            string[] paths = fixture.VisiblePaths();
            string expandedPath = paths[^1];
            using SolutionExplorer root = fixture.CreateRoot();
            typeof(SolutionExplorer).GetField("_projectChangedDebounceTimer", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(root, new DispatcherTimer());
            root.AddChild(new SolutionNode { FullPath = expandedPath, IsExpanded = true });
            var notifications = new List<(int Count, bool TargetExpanded)>();
            root.SolutionStateReloaded += (_, _) => notifications.Add((root.VisualChildren.Count,
                root.VisualChildren.Any(node => node.FullPath == expandedPath && node.IsExpanded)));
            using QueuedContext context = new();

            root.Refresh();
            Assert.DoesNotContain(root.VisualChildren, node => node.FullPath == expandedPath);
            if (refreshAgain) root.Refresh();
            Assert.Empty(notifications);
            context.Drain();

            Assert.True(Assert.Single(root.VisualChildren, node => node.FullPath == expandedPath).IsExpanded);
            Assert.Equal((paths.Length, true), Assert.Single(notifications));
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void OpeningWorkspaceSelectsStartupProjectDiscoveredInALaterBatch(bool lockConfigWhileLoading, bool externalEdit)
    {
        WpfTestHost.Invoke(() =>
        {
            using PopulationFixture fixture = new();
            for (int index = 0; index < 60; index++) fixture.CreateDirectory($"folder{index:D3}");
            string lateFolder = fixture.VisiblePaths()[^1];
            string projectPath = Path.Combine(lateFolder, "Runner.csproj");
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>
                """);
            SolutionConfigStore.Save(fixture.SolutionPath, new SolutionConfig());
            ProjectProviderRegistry.Initialize();
            using QueuedContext context = new();
            using var root = new SolutionExplorer(new SolutionEnvironments { SolutionPath = fixture.SolutionPath });
            Assert.DoesNotContain(root.VisualChildren, node => node.FullPath == lateFolder);

            if (externalEdit)
                SolutionConfigStore.Save(fixture.SolutionPath, new SolutionConfig { StartupProject = "external.cvproj" });
            using FileStream? configLock = lockConfigWhileLoading
                ? new FileStream(fixture.SolutionPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                : null;
            context.Drain();

            ProjectNode project = Assert.Single(root.VisualChildren.OfType<ProjectNode>());
            bool autoSaved = !lockConfigWhileLoading && !externalEdit;
            string expected = autoSaved ? Path.GetRelativePath(fixture.Root, projectPath) : string.Empty;
            Assert.Equal(expected, root.Config.StartupProject);
            Assert.Equal(externalEdit ? "external.cvproj" : expected, SolutionConfigStore.Load(fixture.SolutionPath).Config.StartupProject);
            Assert.Equal(autoSaved, project.IsStartupProject);
            Assert.False(root.OperationHistory.CanUndo);
        });
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public void WorkspaceExitKeepsExternalChangesAndPreservesTheLocalDraft(bool localEdit, bool externalEdit)
    {
        WpfTestHost.Invoke(() =>
        {
            using PopulationFixture fixture = new();
            SolutionConfigStore.Save(fixture.SolutionPath, new SolutionConfig { StartupProject = "opened.cvproj" });
            using var root = new SolutionExplorer(new SolutionEnvironments { SolutionPath = fixture.SolutionPath });
            if (externalEdit)
                SolutionConfigStore.Save(fixture.SolutionPath, new SolutionConfig { StartupProject = "external.cvproj" });
            byte[] beforeExit = File.ReadAllBytes(fixture.SolutionPath);
            if (localEdit) root.Config.StartupProject = "unsaved.cvproj";

            var handler = (EventHandler)typeof(SolutionExplorer)
                .GetField("_processExitHandler", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(root)!;
            handler(null, EventArgs.Empty);

            string[] drafts = Directory.GetFiles(fixture.Root, "*.cvsln.conflict-*");
            if (externalEdit)
                Assert.Equal(beforeExit, File.ReadAllBytes(fixture.SolutionPath));
            else
                Assert.Equal("unsaved.cvproj", SolutionConfigStore.Load(fixture.SolutionPath).Config.StartupProject);

            if (localEdit && externalEdit)
                Assert.Equal("unsaved.cvproj", SolutionConfigStore.Load(Assert.Single(drafts)).Config.StartupProject);
            else
                Assert.Empty(drafts);
        });
    }

    [Fact]
    public void FailedSolutionItemRegistrationKeepsTheWorkspaceUnchangedAndCanBeRetried()
    {
        WpfTestHost.Invoke(() =>
        {
            using PopulationFixture fixture = new();
            string source = fixture.WriteFile("Notes.txt");
            SolutionConfigStore.Save(fixture.SolutionPath, new SolutionConfig());
            using var root = new SolutionExplorer(new SolutionEnvironments { SolutionPath = fixture.SolutionPath });
            SolutionConfig original = root.Config;
            string originalConfig = SolutionConfigStore.Serialize(original);
            byte[] originalFile = File.ReadAllBytes(fixture.SolutionPath);
            bool registered = false;
            string error = string.Empty;
            Exception? escaped;
            using (File.Open(fixture.SolutionPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                escaped = Record.Exception(() => registered = root.RegisterSolutionItems([source], null, out error));

            Assert.Equal(originalConfig, SolutionConfigStore.Serialize(root.Config));
            Assert.Same(original, root.Config);
            Assert.Null(escaped);
            Assert.False(registered);
            Assert.Contains("保存解决方案", error);
            Assert.Equal(originalFile, File.ReadAllBytes(fixture.SolutionPath));
            Assert.False(root.OperationHistory.CanUndo);

            Assert.True(root.RegisterSolutionItems([source], null, out error), error);
            Assert.Single(root.Config.SolutionItems);
            Assert.Single(SolutionConfigStore.Load(fixture.SolutionPath).Config.SolutionItems);
            Assert.True(root.OperationHistory.CanUndo);
            Assert.True(File.Exists(source));
        });
    }

    [Fact]
    public async Task StartupCommandRequeryUsesDiscoverySnapshotAndReloadsChangedProject()
    {
        using PopulationFixture fixture = new();
        fixture.CreateDirectory("app");
        string relativeProject = Path.Combine("app", "Runner.csproj");
        string project = Path.Combine(fixture.Root, relativeProject);
        File.WriteAllText(project, """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>
            """);
        SolutionConfigStore.Save(fixture.SolutionPath, new SolutionConfig { StartupProject = relativeProject });
        ProjectProviderRegistry.Initialize();
        SolutionExplorer root = WpfTestHost.Invoke(() => new SolutionExplorer(new SolutionEnvironments { SolutionPath = fixture.SolutionPath }));
        try
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            var discoveryField = typeof(SolutionExplorer).GetField("_startupDiscovery", instance)!;
            var scanField = discoveryField.FieldType.GetField("PendingTask")!;
            var watcherField = typeof(SolutionExplorer).GetField("_fileSystemWatcher", instance)!;
            var changed = typeof(SolutionExplorer).GetMethod("FileSystemWatcher_Changed", instance)!;
            var deleted = typeof(SolutionExplorer).GetMethod("FileSystemWatcher_Deleted", instance)!;

            Task? GetPendingScan() => (Task?)scanField.GetValue(discoveryField.GetValue(root));

            async Task WaitForDiscoveryAsync()
            {
                Task? scan = WpfTestHost.Invoke(() =>
                {
                    Assert.False(root.CanExecuteStartupProject(ProjectCapabilityIds.Run));
                    return GetPendingScan();
                });
                if (scan != null) await scan.WaitAsync(TimeSpan.FromSeconds(15));
            }

            // Deliver watcher callbacks deterministically after each file change.
            WpfTestHost.Invoke(() => ((FileSystemWatcher)watcherField.GetValue(root)!).EnableRaisingEvents = false);
            await WaitForDiscoveryAsync();
            WpfTestHost.Invoke(() =>
            {
                Assert.True(root.CanExecuteStartupProject(ProjectCapabilityIds.Run));
                Assert.False(root.CanExecuteStartupProject(ProjectCapabilityIds.Debug));
                deleted.Invoke(root, [null, new FileSystemEventArgs(WatcherChangeTypes.Deleted, fixture.Root, "image.txt")]);
                Assert.True(root.CanExecuteStartupProject(ProjectCapabilityIds.Run));

                File.WriteAllText(project, """
                    <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Library</OutputType></PropertyGroup></Project>
                    """);
                changed.Invoke(root, [null, new FileSystemEventArgs(WatcherChangeTypes.Changed, fixture.Root, relativeProject)]);
            });

            await WaitForDiscoveryAsync();
            WpfTestHost.Invoke(() =>
            {
                Assert.False(root.CanExecuteStartupProject(ProjectCapabilityIds.Run));
                Assert.Null(GetPendingScan());
            });
        }
        finally
        {
            WpfTestHost.Invoke(root.Dispose);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WatcherLeavesNewFilesUnderTheirUnloadedPhysicalParent(bool rename)
    {
        using PopulationFixture fixture = new();
        const string folderName = "captures";
        string nestedFolderName = Path.Combine(folderName, "lot");
        string nestedFileName = Path.Combine(nestedFolderName, "frame.txt");
        string nestedPath = Path.Combine(fixture.Root, nestedFileName);
        const string oldName = "previous.txt";
        string oldPath = Path.Combine(fixture.Root, oldName);
        fixture.CreateDirectory(nestedFolderName);
        var (root, folder, debounce) = WpfTestHost.Invoke(() =>
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            SolutionExplorer root = fixture.CreateRoot();
            var debounce = new DispatcherTimer();
            typeof(SolutionExplorer).GetField("_changedDebounceTimer", instance)!.SetValue(root, debounce);
            var folder = new FolderNode(new DirectoryInfo(Path.Combine(fixture.Root, folderName)));
            root.AddChild(folder);
            if (rename)
            {
                fixture.WriteFile(oldName);
                SolutionNodeFactory.AddFileNode(root, new FileInfo(oldPath));
                File.Move(oldPath, nestedPath);
                typeof(SolutionExplorer).GetMethod("FileSystemWatcher_Renamed", instance)!.Invoke(root,
                    [null, new RenamedEventArgs(WatcherChangeTypes.Renamed, fixture.Root, nestedFileName, oldName)]);
            }
            else
            {
                fixture.WriteFile(nestedFileName);
                typeof(SolutionExplorer).GetMethod("FileSystemWatcher_Created", instance)!.Invoke(root,
                    [null, new FileSystemEventArgs(WatcherChangeTypes.Created, fixture.Root, nestedFileName)]);
            }
            return (root, folder, debounce);
        });
        try
        {
            await WpfTestHost.Invoke(() => Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                Assert.Same(folder, Assert.Single(root.VisualChildren));
                Assert.False(folder.AreChildrenLoaded);
            }, DispatcherPriority.Background).Task);

            await WpfTestHost.Invoke(folder.EnsureChildrenLoadedAsync);
            var nested = WpfTestHost.Invoke(() => Assert.IsType<FolderNode>(Assert.Single(folder.VisualChildren)));
            await WpfTestHost.Invoke(nested.EnsureChildrenLoadedAsync);
            WpfTestHost.Invoke(() => Assert.Equal(nestedPath, Assert.Single(nested.VisualChildren).FullPath));
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                debounce.Stop();
                folder.Dispose();
            });
        }
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

        public void RunNext()
        {
            Assert.True(_callbacks.TryDequeue(out Action? callback), "Population must resume through its captured yield context.");
            callback!();
        }

        public void Drain()
        {
            while (_callbacks.Count > 0)
                RunNext();
        }

        public void Complete(Task task)
        {
            while (!task.IsCompleted)
                RunNext();
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
