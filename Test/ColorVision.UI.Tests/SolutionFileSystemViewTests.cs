using ColorVision.Solution.Explorer;
using System.IO;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public class SolutionFileSystemViewTests
{
    [Fact]
    public async Task PhysicalViewShowsProjectAndHiddenFilesWithoutLoadingAProjectOrWritingSource()
    {
        using var fixture = new TemporaryWorkspace();
        string projectDirectory = fixture.CreateDirectory("Project");
        fixture.WriteFile("Project/Project.csproj", "<Project />");
        fixture.WriteFile("Project/Project.cvproj", "deliberately not a project definition");
        fixture.WriteFile("Workspace.cvsln", "solution metadata");
        fixture.WriteFile("Workspace.cvsln.cache.db", "existing cache metadata");
        string hiddenDirectory = fixture.CreateDirectory(".git");
        File.SetAttributes(hiddenDirectory, File.GetAttributes(hiddenDirectory) | FileAttributes.Hidden);
        fixture.WriteFile(".git/config", "hidden repository metadata");
        fixture.WriteFile("reference.lnk", "not a shell shortcut");
        string[] before = fixture.CaptureFiles();
        FileSystemFolderNode root = WpfTestHost.Invoke(() => new FileSystemFolderNode(new DirectoryInfo(fixture.Root), true));
        try
        {
            await WpfTestHost.Invoke(root.EnsureChildrenLoadedAsync);
            FileSystemFolderNode project = WpfTestHost.Invoke(() =>
            {
                Assert.Null(SolutionNodeFactory.FindSolutionExplorer(root));
                Assert.Contains(root.VisualChildren, node => node.FullPath == hiddenDirectory);
                Assert.Contains(root.VisualChildren, node => node.Name == "Workspace.cvsln");
                Assert.Contains(root.VisualChildren, node => node.Name == "Workspace.cvsln.cache.db");
                Assert.Contains(root.VisualChildren, node => node.Name == "reference.lnk");
                return Assert.IsType<FileSystemFolderNode>(Assert.Single(root.VisualChildren, node => node.FullPath == projectDirectory));
            });
            await WpfTestHost.Invoke(project.EnsureChildrenLoadedAsync);
            WpfTestHost.Invoke(() =>
            {
                Assert.All(project.VisualChildren, node => Assert.IsType<FileNode>(node));
                Assert.Contains(project.VisualChildren, node => node.Name == "Project.csproj");
                Assert.Contains(project.VisualChildren, node => node.Name == "Project.cvproj");
            });
            Assert.Equal(before, fixture.CaptureFiles());
        }
        finally
        {
            WpfTestHost.Invoke(root.Dispose);
        }
    }

    [Fact]
    public void WorkspaceRootRejectsPhysicalRenameDeleteAndCut()
    {
        using var fixture = new TemporaryWorkspace();
        fixture.WriteFile("keep.txt", "keep");
        WpfTestHost.Invoke(() =>
        {
            using var root = new FileSystemFolderNode(new DirectoryInfo(fixture.Root), true);
            Assert.False(root.CanReName);
            Assert.False(root.CanDelete);
            Assert.False(root.CanCut);
            Assert.Null(root.PhysicalDeletePath);
            Assert.False(root.ReName("renamed"));
            Assert.False(root.TryDelete(showConfirmation: false));
            Assert.True(Directory.Exists(fixture.Root));
            Assert.Equal("keep", File.ReadAllText(Path.Combine(fixture.Root, "keep.txt")));
        });
    }

    [Fact]
    public async Task RefreshReplacesRemovedFilesAndFindsNewFiles()
    {
        using var fixture = new TemporaryWorkspace();
        string oldPath = fixture.WriteFile("old.txt", "old");
        FileSystemFolderNode root = WpfTestHost.Invoke(() => new FileSystemFolderNode(new DirectoryInfo(fixture.Root), true));
        try
        {
            await WpfTestHost.Invoke(root.EnsureChildrenLoadedAsync);
            WpfTestHost.Invoke(() => Assert.Contains(root.VisualChildren, node => node.FullPath == oldPath));
            File.Delete(oldPath);
            string newPath = fixture.WriteFile("new.csproj", "<Project />");
            WpfTestHost.Invoke(root.Refresh);
            await WpfTestHost.Invoke(root.EnsureChildrenLoadedAsync);
            WpfTestHost.Invoke(() =>
            {
                Assert.DoesNotContain(root.VisualChildren, node => node.FullPath == oldPath);
                Assert.IsType<FileNode>(Assert.Single(root.VisualChildren, node => node.FullPath == newPath));
            });
        }
        finally
        {
            WpfTestHost.Invoke(root.Dispose);
        }
    }

    [Fact]
    public async Task PhysicalNavigationAndCollapsePreserveUnopenedLazyBranches()
    {
        using var fixture = new TemporaryWorkspace();
        string targetPath = fixture.WriteFile("Project/Nested/target.txt", "target");
        string untouchedPath = fixture.CreateDirectory("Untouched");
        fixture.WriteFile("Untouched/Deeper/keep.txt", "keep");
        string[] before = fixture.CaptureFiles();
        FileSystemFolderNode root = WpfTestHost.Invoke(() => new FileSystemFolderNode(new DirectoryInfo(fixture.Root), true));
        try
        {
            SolutionNode? target = await WpfTestHost.Invoke(() => SolutionTreeNavigationService.ResolvePathAsync(root, targetPath));
            WpfTestHost.Invoke(() =>
            {
                FileNode file = Assert.IsType<FileNode>(target);
                FileSystemFolderNode nested = Assert.IsType<FileSystemFolderNode>(file.Parent);
                FileSystemFolderNode project = Assert.IsType<FileSystemFolderNode>(nested.Parent);
                FileSystemFolderNode untouched = Assert.IsType<FileSystemFolderNode>(
                    Assert.Single(root.VisualChildren, node => node.FullPath == untouchedPath));
                SolutionNode placeholder = Assert.Single(untouched.VisualChildren);
                Assert.IsType<LazyLoadingNode>(placeholder);
                Assert.True(root.IsExpanded && project.IsExpanded && nested.IsExpanded);
                Assert.False(untouched.AreChildrenLoaded);

                project.Open();
                Assert.False(project.IsExpanded);
                Assert.True(nested.IsExpanded);
                SolutionTreeNavigationService.CollapseDescendants(root);

                Assert.True(root.IsExpanded);
                Assert.False(project.IsExpanded);
                Assert.False(nested.IsExpanded);
                Assert.False(untouched.IsExpanded);
                Assert.False(untouched.AreChildrenLoaded);
                Assert.Same(placeholder, Assert.Single(untouched.VisualChildren));

                project.Open();
                nested.Open();
                Assert.True(project.IsExpanded && nested.IsExpanded);
                Assert.Same(file, Assert.Single(nested.VisualChildren));
            });
            Assert.Equal(before, fixture.CaptureFiles());
        }
        finally
        {
            WpfTestHost.Invoke(root.Dispose);
        }
    }

    [Fact]
    public async Task PhysicalSearchUsesWorkspaceDiskScopeEvenForAnExplicitSolution()
    {
        using var fixture = new TemporaryWorkspace();
        string unrelatedFile = fixture.WriteFile("NotInProject/find-me.csproj", "<Project />");
        fixture.WriteFile("InProject/readme.txt", "project");
        string[] before = fixture.CaptureFiles();
        // Search only needs workspace identity. Avoid constructing a production Explorer,
        // which would install a watcher, create a cache, and register process-exit writes.
        SolutionExplorer explorer = (SolutionExplorer)RuntimeHelpers.GetUninitializedObject(typeof(SolutionExplorer));
        typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.ConfigFileInfo))!
            .SetValue(explorer, new FileInfo(Path.Combine(fixture.Root, "Workspace.cvsln")));
        typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.Config))!
            .SetValue(explorer, new SolutionConfig { ProjectMode = SolutionProjectMode.Explicit });
        FileSystemFolderNode root = WpfTestHost.Invoke(() => new FileSystemFolderNode(new DirectoryInfo(fixture.Root), true));
        try
        {
            SolutionSearchResult result = await WpfTestHost.Invoke(() => SolutionSearchService.SearchFileSystemAsync(
                explorer, root, "find-me", 100, CancellationToken.None));
            SolutionSearchHit hit = Assert.Single(result.Hits);
            Assert.Equal(unrelatedFile, hit.FullPath);
            Assert.Same(root, hit.ParentNode);
            Assert.False(result.IsTruncated);
            Assert.Equal(before, fixture.CaptureFiles());
        }
        finally
        {
            WpfTestHost.Invoke(root.Dispose);
        }
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public string Root { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVision.Solution.Tests", Guid.NewGuid().ToString("N")));

        public TemporaryWorkspace() => Directory.CreateDirectory(Root);

        public string CreateDirectory(string relativePath) => Directory.CreateDirectory(Path.Combine(Root, relativePath)).FullName;

        public string WriteFile(string relativePath, string content)
        {
            string path = Path.GetFullPath(Path.Combine(Root, relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public string[] CaptureFiles() => Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => $"{Path.GetRelativePath(Root, path)}:{Convert.ToBase64String(File.ReadAllBytes(path))}")
            .ToArray();

        public void Dispose()
        {
            string fixtureParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVision.Solution.Tests")) + Path.DirectorySeparatorChar;
            if (!Root.StartsWith(fixtureParent, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Test directory is outside the fixture parent.");
            foreach (string directory in Directory.GetDirectories(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(directory, FileAttributes.Directory);
            Directory.Delete(Root, recursive: true);
        }
    }
}
