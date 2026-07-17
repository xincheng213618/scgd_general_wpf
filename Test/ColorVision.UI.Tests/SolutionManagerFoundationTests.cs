using ColorVision.Solution;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Explorer;
using ColorVision.Solution.Terminal;
using ColorVision.Solution.Workspace;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

public class SolutionManagerFoundationTests
{
    [Theory]
    [InlineData("txt", ".txt")]
    [InlineData(".TXT", ".txt")]
    [InlineData("  cvflow ", ".cvflow")]
    [InlineData(null, "")]
    public void NormalizeExtension_ReturnsStableKey(string? extension, string expected)
    {
        Assert.Equal(expected, EditorManager.NormalizeExtension(extension));
    }

    [Fact]
    public void SelectDefaultFileEditor_PrefersSpecializedEditorOverGenericDefault()
    {
        var specialized = CreateFileDescriptor<SpecializedEditor>("specialized", priority: 10);
        var generic = CreateFileDescriptor<GenericEditor>("generic", isGeneric: true, isDefault: true, priority: 1000);

        var selected = EditorManager.SelectDefaultFileEditor([specialized], [generic]);

        Assert.Equal(typeof(SpecializedEditor), selected?.EditorType);
    }

    [Fact]
    public void SelectDefaultFileEditor_UsesConfiguredStableIdOrLegacyTypeName()
    {
        var specialized = CreateFileDescriptor<SpecializedEditor>("specialized", isDefault: true);
        var alternate = CreateFileDescriptor<AlternateEditor>("alternate");

        var selectedById = EditorManager.SelectDefaultFileEditor([specialized, alternate], [], "alternate");
        var selectedByLegacyType = EditorManager.SelectDefaultFileEditor([specialized, alternate], [], typeof(AlternateEditor).FullName);

        Assert.Equal(typeof(AlternateEditor), selectedById?.EditorType);
        Assert.Equal(typeof(AlternateEditor), selectedByLegacyType?.EditorType);
    }

    [Fact]
    public void SelectDefaultFileEditor_UsesPriorityForDeterministicFallback()
    {
        var lowPriority = CreateFileDescriptor<SpecializedEditor>("low", priority: 10);
        var highPriority = CreateFileDescriptor<AlternateEditor>("high", priority: 100);

        var selected = EditorManager.SelectDefaultFileEditor([lowPriority, highPriority], []);

        Assert.Equal(typeof(AlternateEditor), selected?.EditorType);
    }

    [Fact]
    public void EditorManager_ProgrammaticDescriptorsPreserveStableIdsAndDisplayNames()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string extension = $".dynamic{suffix}";
        string firstId = $"tests.dynamic.first.{suffix}";
        string secondId = $"tests.dynamic.second.{suffix}";
        var first = new EditorDescriptor(
            firstId,
            typeof(TrackingEditor),
            EditorResourceKind.File,
            [extension],
            IsGeneric: false,
            IsDefault: false,
            Priority: 10,
            IsVisibleInOpenWith: true,
            DisplayName: "动态编辑器 A");
        var second = first with
        {
            Id = secondId,
            Priority = 20,
            DisplayName = "动态编辑器 B",
        };
        EditorManager.Instance.RegisterEditor(first);
        EditorManager.Instance.RegisterEditor(second);
        string directoryPath = CreateTemporaryDirectory();
        string filePath = Path.Combine(directoryPath, $"Example{extension}");
        File.WriteAllText(filePath, "test");

        try
        {
            List<EditorDescriptor> descriptors = EditorManager.Instance
                .GetFileEditorDescriptors(extension, visibleOnly: true)
                .Where(item => item.Id == firstId || item.Id == secondId)
                .ToList();

            Assert.Equal([secondId, firstId], descriptors.Select(item => item.Id));
            Assert.Equal("动态编辑器 B", EditorManager.GetEditorName(descriptors[0]));
            TrackingEditor.LastOpenedPath = null;
            Assert.True(EditorManager.Instance.OpenFileWith(filePath, secondId));
            Assert.Equal(filePath, TrackingEditor.LastOpenedPath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void TryGetProjectDirectory_ResolvesExistingProjectFile()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), $"ColorVision.Solution.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        string projectPath = Path.Combine(directoryPath, "Example.CVPROJ");
        File.WriteAllText(projectPath, "{}");

        try
        {
            Assert.True(SolutionManager.TryGetProjectDirectory(projectPath, out string resolvedDirectory));
            Assert.Equal(Path.GetFullPath(directoryPath), resolvedDirectory, ignoreCase: true);
            Assert.False(SolutionManager.TryGetProjectDirectory(Path.Combine(directoryPath, "Example.txt"), out _));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void DirectoryCopyDestination_RejectsSourceAndDescendants()
    {
        string sourcePath = Path.Combine(Path.GetTempPath(), "ColorVision", "Source");
        string descendantPath = Path.Combine(sourcePath, "Child", "Source");
        string siblingPath = Path.Combine(Path.GetDirectoryName(sourcePath)!, "Source - Copy (1)");

        Assert.False(TreeViewControl.IsSafeDirectoryCopyDestination(sourcePath, sourcePath));
        Assert.False(TreeViewControl.IsSafeDirectoryCopyDestination(sourcePath, descendantPath));
        Assert.True(TreeViewControl.IsSafeDirectoryCopyDestination(sourcePath, siblingPath));
    }

    [Fact]
    public void SelectionService_SelectsVisibleRangeFromStableAnchor()
    {
        var first = CreateNode("first");
        var second = CreateNode("second");
        var third = CreateNode("third");
        var fourth = CreateNode("fourth");
        var service = new SolutionSelectionService();

        service.SelectSingle(second);
        service.SelectRange([first, second, third, fourth], fourth, additive: false);

        Assert.Equal([second, third, fourth], service.SelectedNodes);
        Assert.Same(second, service.AnchorNode);
        Assert.False(first.IsMultiSelected);
        Assert.True(second.IsMultiSelected);
        Assert.True(third.IsMultiSelected);
        Assert.True(fourth.IsMultiSelected);
    }

    [Fact]
    public void SelectionService_TopLevelNodesRemoveSelectedDescendants()
    {
        var parent = CreateNode("parent");
        var child = CreateNode("child");
        var sibling = CreateNode("sibling");
        parent.AddChild(child);
        var service = new SolutionSelectionService();
        service.SelectSingle(parent);
        service.Toggle(child);
        service.Toggle(sibling);

        var effectiveNodes = service.GetTopLevelNodes(_ => true);

        Assert.Equal([parent, sibling], effectiveNodes);
        Assert.True(SolutionCommandIds.SupportsMultipleSelection(SolutionCommandIds.Delete));
        Assert.False(SolutionCommandIds.SupportsMultipleSelection(SolutionCommandIds.Rename));
    }

    [Fact]
    public void SelectionService_SelectManyRestoresMovedNodeSelectionAndAnchor()
    {
        var first = CreateNode("first");
        var second = CreateNode("second");
        var stale = CreateNode("stale");
        var service = new SolutionSelectionService();
        service.SelectSingle(stale);

        service.SelectMany([first, second], first);

        Assert.Equal([first, second], service.SelectedNodes);
        Assert.Same(first, service.AnchorNode);
        Assert.False(stale.IsMultiSelected);
        Assert.True(first.IsMultiSelected);
        Assert.True(second.IsMultiSelected);
    }

    [Fact]
    public void SelectionService_SearchResultsResolveToCommandTargets()
    {
        var parent = CreateNode("parent");
        var child = CreateNode("child");
        parent.AddChild(child);
        using var parentResult = new SolutionSearchResultNode(parent, "Project\\parent", ownsTarget: false);
        using var childResult = new SolutionSearchResultNode(child, "Project\\parent\\child", ownsTarget: false);
        var service = new SolutionSelectionService();

        service.SelectMany([parentResult, childResult]);

        Assert.Equal([parent, child], service.CommandNodes);
        Assert.Equal([parent], service.GetTopLevelNodes(_ => true));
        Assert.Equal([parentResult, childResult], service.SelectedNodes);
    }

    [Fact]
    public void SolutionSearch_ParseKeywordsSupportsPhrasesAndRemovesDuplicates()
    {
        IReadOnlyList<string> keywords = SolutionSearchService.ParseKeywords(
            "  \"alpha beta\"  gamma  GAMMA  ");

        Assert.Equal(["alpha beta", "gamma"], keywords);
    }

    [Fact]
    public async Task SolutionSearch_ExplicitModeHonorsProjectScopesAndExternalProjects()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string externalContainer = CreateTemporaryDirectory();
        string projectADirectory = Path.Combine(solutionDirectory, "ProjectA");
        string projectBDirectory = Path.Combine(externalContainer, "ProjectB");
        string projectAFile = Path.Combine(projectADirectory, "ProjectA.cvproj");
        string projectBFile = Path.Combine(projectBDirectory, "ProjectB.cvproj");
        string includedA = Path.Combine(projectADirectory, "Source", "needle-a.txt");
        string excludedA = Path.Combine(projectADirectory, "Output", "needle-excluded.txt");
        string includedB = Path.Combine(projectBDirectory, "Source", "needle-b.txt");
        string outsideProject = Path.Combine(solutionDirectory, "Outside", "needle-outside.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(includedA)!);
        Directory.CreateDirectory(Path.GetDirectoryName(excludedA)!);
        Directory.CreateDirectory(Path.GetDirectoryName(includedB)!);
        Directory.CreateDirectory(Path.GetDirectoryName(outsideProject)!);
        File.WriteAllText(includedA, "A");
        File.WriteAllText(excludedA, "excluded");
        File.WriteAllText(includedB, "B");
        File.WriteAllText(outsideProject, "outside");
        FolderProjectProvider.CreateProjectFile(projectAFile, "ProjectA", excludedPaths: ["Output/**"]);
        FolderProjectProvider.CreateProjectFile(projectBFile, "ProjectB");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects =
            [
                Path.GetRelativePath(solutionDirectory, projectAFile),
                Path.GetRelativePath(solutionDirectory, projectBFile),
            ],
        });

        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            explorer.Cache!.RebuildCache(solutionDirectory);

            SolutionSearchResult result = await SolutionSearchService.SearchAsync([explorer], "needle", 20);
            string[] paths = result.Hits.Select(hit => hit.FullPath).ToArray();

            Assert.Contains(includedA, paths, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(includedB, paths, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(excludedA, paths, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain(outsideProject, paths, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(result.Hits, hit =>
                string.Equals(hit.FullPath, includedB, StringComparison.OrdinalIgnoreCase)
                && hit.DisplayPath.StartsWith("ProjectB", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
            Directory.Delete(externalContainer, recursive: true);
        }
    }

    [Fact]
    public async Task SolutionSearch_MatchesPathSegmentsAndReportsTruncation()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string matchingDirectory = Path.Combine(solutionDirectory, "UniqueNeedleFolder");
        Directory.CreateDirectory(matchingDirectory);
        for (int index = 0; index < 5; index++)
            File.WriteAllText(Path.Combine(matchingDirectory, $"item-{index}.txt"), index.ToString());
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.AutoDiscover,
        });

        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            explorer.Cache!.RebuildCache(solutionDirectory);

            SolutionSearchResult result = await SolutionSearchService.SearchAsync(
                [explorer],
                "UniqueNeedleFolder",
                maxResults: 2);

            Assert.Equal(2, result.Hits.Count);
            Assert.True(result.IsTruncated);
            Assert.All(result.Hits, hit =>
                Assert.Contains("UniqueNeedleFolder", hit.FullPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void FolderProjectProvider_LoadsLegacyAndTypedProjectFiles()
    {
        string directoryPath = CreateTemporaryDirectory();
        try
        {
            string legacyPath = Path.Combine(directoryPath, "Legacy.cvproj");
            File.WriteAllText(legacyPath, "{\"Name\":\"Legacy Project\",\"Version\":\"1.2\"}");
            var provider = new FolderProjectProvider();

            Assert.True(provider.CanLoad(new FileInfo(legacyPath)));
            ProjectDefinition legacy = provider.Load(new FileInfo(legacyPath));
            Assert.Equal("Legacy Project", legacy.Name);
            Assert.Equal("1.2", legacy.Version);

            string typedPath = Path.Combine(directoryPath, "Typed.cvproj");
            FolderProjectProvider.CreateProjectFile(typedPath, "Typed Project", "2.0");
            string typedJson = File.ReadAllText(typedPath);
            Assert.Contains("\"ProjectType\": \"folder\"", typedJson, StringComparison.Ordinal);
            Assert.Equal("Typed Project", provider.Load(new FileInfo(typedPath)).Name);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void FolderProjectProvider_ResolvesConfiguredProjectRoot()
    {
        string containerPath = CreateTemporaryDirectory();
        string definitionPath = Path.Combine(containerPath, "Definitions");
        string projectRootPath = Path.Combine(containerPath, "Workspace");
        Directory.CreateDirectory(definitionPath);
        Directory.CreateDirectory(projectRootPath);
        string projectFilePath = Path.Combine(definitionPath, "External.cvproj");
        string relativeRootPath = Path.GetRelativePath(definitionPath, projectRootPath);

        try
        {
            FolderProjectProvider.CreateProjectFile(
                projectFilePath,
                "External Project",
                rootPath: relativeRootPath);

            ProjectDefinition project = new FolderProjectProvider().Load(new FileInfo(projectFilePath));
            Assert.Null(project.LoadError);
            Assert.Equal(projectRootPath, project.ProjectDirectory.FullName, ignoreCase: true);
            Assert.Equal(definitionPath, project.ProjectFile.DirectoryName, ignoreCase: true);

            using FolderNode node = SolutionNodeFactory.CreateFolderNode(new DirectoryInfo(definitionPath));
            ProjectNode projectNode = Assert.IsType<ProjectNode>(node);
            Assert.Equal(projectRootPath, projectNode.FullPath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Theory]
    [InlineData("bin/tool.dll", "bin", true)]
    [InlineData("src/bin/tool.dll", "**/bin/**", true)]
    [InlineData("notes.tmp", "**/*.tmp", true)]
    [InlineData("src/notes.tmp", "**/*.tmp", true)]
    [InlineData("src/notes.txt", "**/*.tmp", false)]
    [InlineData("Output/report.csv", "Out?ut/**", true)]
    public void ProjectItemRules_MatchProjectRelativeExcludePatterns(string relativePath, string pattern, bool expected)
    {
        Assert.Equal(expected, ProjectItemRules.IsExcluded(relativePath, [pattern]));
    }

    [Fact]
    public void FolderProjectProvider_LoadsProjectItemExclusions()
    {
        string projectDirectory = CreateTemporaryDirectory();
        string projectFilePath = Path.Combine(projectDirectory, "Filtered.cvproj");
        string includedFilePath = Path.Combine(projectDirectory, "Source", "main.py");
        string excludedFilePath = Path.Combine(projectDirectory, "Output", "report.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(includedFilePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(excludedFilePath)!);
        File.WriteAllText(includedFilePath, string.Empty);
        File.WriteAllText(excludedFilePath, string.Empty);

        try
        {
            FolderProjectProvider.CreateProjectFile(
                projectFilePath,
                "Filtered",
                excludedPaths: ["Output/**", "**/*.tmp"]);

            ProjectDefinition project = new FolderProjectProvider().Load(new FileInfo(projectFilePath));
            Assert.NotNull(project.ItemRules);
            Assert.True(project.ItemRules.Includes(project.ProjectDirectory, includedFilePath));
            Assert.False(project.ItemRules.Includes(project.ProjectDirectory, excludedFilePath));
            Assert.Contains("\"Items\"", File.ReadAllText(projectFilePath), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void ProjectItemRules_UseSpecificIncludeOverridesAndKeepAncestorsVisible()
    {
        string projectDirectory = CreateTemporaryDirectory();
        string outputDirectory = Path.Combine(projectDirectory, "Output");
        string includedFilePath = Path.Combine(outputDirectory, "report.csv");
        string excludedFilePath = Path.Combine(outputDirectory, "debug.log");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(includedFilePath, string.Empty);
        File.WriteAllText(excludedFilePath, string.Empty);

        try
        {
            var rules = new ProjectItemRules(["Output/**"], ["Output/report.csv"]);
            Assert.False(rules.Includes(new DirectoryInfo(projectDirectory), outputDirectory));
            Assert.True(rules.IsVisible(new DirectoryInfo(projectDirectory), outputDirectory));
            Assert.True(rules.Includes(new DirectoryInfo(projectDirectory), includedFilePath));
            Assert.False(rules.Includes(new DirectoryInfo(projectDirectory), excludedFilePath));

            var exactExcludeWins = new ProjectItemRules(["Output/report.csv"], ["Output/**"]);
            Assert.False(exactExcludeWins.Includes(new DirectoryInfo(projectDirectory), includedFilePath));
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void FolderProjectProvider_PersistsMultiItemMembershipChanges()
    {
        string projectDirectory = CreateTemporaryDirectory();
        string projectFilePath = Path.Combine(projectDirectory, "Mutable.cvproj");
        string firstFilePath = Path.Combine(projectDirectory, "Source", "first.txt");
        string secondFilePath = Path.Combine(projectDirectory, "Source", "second.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(firstFilePath)!);
        File.WriteAllText(firstFilePath, string.Empty);
        File.WriteAllText(secondFilePath, string.Empty);

        try
        {
            FolderProjectProvider.CreateProjectFile(projectFilePath, "Mutable");
            var provider = new FolderProjectProvider();
            ProjectDefinition project = provider.Load(new FileInfo(projectFilePath));

            Assert.True(provider.TrySetItemMembership(
                project,
                [firstFilePath, secondFilePath],
                included: false,
                out ProjectDefinition? excludedProject,
                out string excludeError));
            Assert.Equal(string.Empty, excludeError);
            Assert.NotNull(excludedProject);
            Assert.False(excludedProject.ItemRules!.Includes(excludedProject.ProjectDirectory, firstFilePath));
            Assert.False(excludedProject.ItemRules.Includes(excludedProject.ProjectDirectory, secondFilePath));

            Assert.True(provider.TrySetItemMembership(
                excludedProject,
                [firstFilePath],
                included: true,
                out ProjectDefinition? includedProject,
                out string includeError));
            Assert.Equal(string.Empty, includeError);
            Assert.NotNull(includedProject);
            Assert.True(includedProject.ItemRules!.Includes(includedProject.ProjectDirectory, firstFilePath));
            Assert.False(includedProject.ItemRules.Includes(includedProject.ProjectDirectory, secondFilePath));

            JObject json = JObject.Parse(File.ReadAllText(projectFilePath));
            Assert.Contains("Source/first.txt", json["Items"]!["Include"]!.Values<string>());
            Assert.Contains("Source/second.txt", json["Items"]!["Exclude"]!.Values<string>());
            Assert.DoesNotContain("Source/first.txt", json["Items"]!["Exclude"]!.Values<string>());
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void FolderProjectProvider_PersistsDependenciesAndRejectsSelfDependency()
    {
        string containerPath = CreateTemporaryDirectory();
        try
        {
            ProjectDefinition projectA = CreateBuildProject(containerPath, "A");
            ProjectDefinition projectB = CreateBuildProject(containerPath, "B");
            string dependencyReference = SolutionConfigurationEditorModel.CreateDependencyReference(projectA, projectB);

            Assert.True(ProjectProviderRegistry.CanChangeProjectDependencies(projectA));
            Assert.True(ProjectProviderRegistry.TrySetProjectDependencies(
                projectA,
                [dependencyReference],
                out ProjectDefinition? updatedProject,
                out string errorMessage));
            Assert.Equal(string.Empty, errorMessage);
            Assert.Equal(["../B/B.cvproj"], updatedProject?.Dependencies);

            string originalJson = File.ReadAllText(projectA.ProjectFile.FullName);
            Assert.False(ProjectProviderRegistry.TrySetProjectDependencies(
                updatedProject!,
                ["A.cvproj"],
                out _,
                out errorMessage));
            Assert.Contains("不能依赖自身", errorMessage, StringComparison.Ordinal);
            Assert.Equal(originalJson, File.ReadAllText(projectA.ProjectFile.FullName));
        }
        finally
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Fact]
    public async Task ProjectTree_AppliesProjectItemExclusions()
    {
        string projectDirectory = CreateTemporaryDirectory();
        string projectFilePath = Path.Combine(projectDirectory, "Filtered.cvproj");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Source"));
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Output"));
        File.WriteAllText(Path.Combine(projectDirectory, "Source", "main.py"), string.Empty);
        File.WriteAllText(Path.Combine(projectDirectory, "Output", "report.csv"), string.Empty);

        try
        {
            FolderProjectProvider.CreateProjectFile(projectFilePath, "Filtered", excludedPaths: ["Output/**"]);
            using FolderNode node = SolutionNodeFactory.CreateFolderNode(new DirectoryInfo(projectDirectory));
            ProjectNode projectNode = Assert.IsType<ProjectNode>(node);
            projectNode.VisualChildren.Clear();

            await SolutionNodeFactory.PopulateChildren(projectNode, projectNode.DirectoryInfo);

            SolutionNode includedSource = Assert.Single(projectNode.VisualChildren, child => child.Name == "Source");
            Assert.DoesNotContain(projectNode.VisualChildren, child => child.Name == "Output");
            var includedMenuItems = new List<ColorVision.UI.Menus.MenuItemMetadata>();
            includedSource.CollectMenuItems(includedMenuItems);
            Assert.Contains(includedMenuItems, item =>
                item.GuidId == "ExcludeFromProject"
                && item.Command == SolutionProjectCommands.ExcludeFromProject);

            Assert.False(projectNode.ShowAllFiles);
            projectNode.ToggleShowAllFilesCommand.Execute(null);
            Assert.True(projectNode.ShowAllFiles);
            projectNode.VisualChildren.Clear();
            await SolutionNodeFactory.PopulateChildren(projectNode, projectNode.DirectoryInfo);
            SolutionNode excludedOutput = Assert.Single(projectNode.VisualChildren, child => child.Name == "Output");
            Assert.True(excludedOutput.IsExcludedFromProject);
            var excludedMenuItems = new List<ColorVision.UI.Menus.MenuItemMetadata>();
            excludedOutput.CollectMenuItems(excludedMenuItems);
            Assert.Contains(excludedMenuItems, item =>
                item.GuidId == "IncludeInProject"
                && item.Command == SolutionProjectCommands.IncludeInProject);
        }
        finally
        {
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void ProjectReferenceMatches_AcceptsLegacyDirectoryAndProjectFileReferences()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(solutionDirectory, "ProjectA");
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, "ProjectA.cvproj");
        FolderProjectProvider.CreateProjectFile(projectPath, "Project A");
        var project = new FolderProjectProvider().Load(new FileInfo(projectPath));

        try
        {
            Assert.True(SolutionExplorer.ProjectReferenceMatches(solutionDirectory, "ProjectA", project));
            Assert.True(SolutionExplorer.ProjectReferenceMatches(solutionDirectory, Path.Combine("ProjectA", "ProjectA.cvproj"), project));
            Assert.False(SolutionExplorer.ProjectReferenceMatches(solutionDirectory, "ProjectB", project));
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void EditorDocumentIdentity_IncludesStableEditorId()
    {
        string filePath = Path.Combine(Path.GetTempPath(), "document.txt");

        string textDocumentId = EditorDocumentService.CreateContentId(filePath, "colorvision.text");
        string hexDocumentId = EditorDocumentService.CreateContentId(filePath, "colorvision.hex");

        Assert.NotEqual(textDocumentId, hexDocumentId);
        Assert.Equal(textDocumentId, EditorDocumentService.CreateContentId(filePath, "colorvision.text"));
    }

    [Fact]
    public void ResourceOpenService_ClassifiesFolderSolutionProjectAndFile()
    {
        string directoryPath = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(directoryPath, "Example.cvsln");
        string projectPath = Path.Combine(directoryPath, "Example.cvproj");
        string filePath = Path.Combine(directoryPath, "Example.txt");
        File.WriteAllText(solutionPath, "{}");
        File.WriteAllText(projectPath, "{}");
        File.WriteAllText(filePath, "text");

        try
        {
            Assert.Equal(ResourceOpenKind.Folder, ResourceOpenService.Classify(directoryPath));
            Assert.Equal(ResourceOpenKind.Solution, ResourceOpenService.Classify(solutionPath));
            Assert.Equal(ResourceOpenKind.Project, ResourceOpenService.Classify(projectPath));
            Assert.Equal(ResourceOpenKind.File, ResourceOpenService.Classify(filePath));
            Assert.Equal(ResourceOpenKind.Missing, ResourceOpenService.Classify(Path.Combine(directoryPath, "missing.txt")));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void FolderWorkspace_IsStableAndDoesNotWriteConfigurationIntoOpenedFolder()
    {
        string folderPath = CreateTemporaryDirectory();
        string? workspacePath = null;

        try
        {
            Assert.True(SolutionManager.TryCreateFolderWorkspace(
                new DirectoryInfo(folderPath),
                out workspacePath));
            Assert.True(File.Exists(workspacePath));
            Assert.False(File.Exists(Path.Combine(folderPath, SolutionManager.FolderWorkspaceFileName)));
            Assert.False(workspacePath.StartsWith(
                Path.TrimEndingDirectorySeparator(folderPath) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));

            SolutionConfig config = SolutionConfigStore.Load(workspacePath).Config;
            Assert.Equal(folderPath, config.RootPath, ignoreCase: true);
            config.SolutionFolders.Add(new SolutionFolderDefinition { Id = "preserved", Name = "Preserved" });
            SolutionConfigStore.Save(workspacePath, config);

            Assert.True(SolutionManager.TryCreateFolderWorkspace(
                new DirectoryInfo(folderPath),
                out string reopenedWorkspacePath));
            Assert.Equal(workspacePath, reopenedWorkspacePath, ignoreCase: true);
            Assert.Contains(
                SolutionConfigStore.Load(reopenedWorkspacePath).Config.SolutionFolders,
                folder => folder.Id == "preserved");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(workspacePath))
            {
                File.Delete(workspacePath);
                File.Delete(SolutionConfigStore.GetBackupPath(workspacePath));
            }
            Directory.Delete(folderPath, recursive: true);
        }
    }

    [Fact]
    public void FolderWorkspace_MigratesLegacyConfigurationWithoutRewritingSourceFolder()
    {
        string folderPath = CreateTemporaryDirectory();
        string legacyWorkspacePath = Path.Combine(folderPath, SolutionManager.FolderWorkspaceFileName);
        string? workspacePath = null;
        var legacyConfig = new SolutionConfig();
        legacyConfig.SolutionFolders.Add(new SolutionFolderDefinition { Id = "legacy", Name = "Legacy" });
        string legacyContent = SolutionConfigStore.Serialize(legacyConfig);
        File.WriteAllText(legacyWorkspacePath, legacyContent);

        try
        {
            Assert.True(SolutionManager.TryCreateFolderWorkspace(
                new DirectoryInfo(folderPath),
                out workspacePath));

            Assert.False(string.Equals(
                legacyWorkspacePath,
                workspacePath,
                StringComparison.OrdinalIgnoreCase));
            Assert.Equal(legacyContent, File.ReadAllText(legacyWorkspacePath));
            SolutionConfig migratedConfig = SolutionConfigStore.Load(workspacePath).Config;
            Assert.Equal(folderPath, migratedConfig.RootPath, ignoreCase: true);
            Assert.Contains(migratedConfig.SolutionFolders, folder => folder.Id == "legacy");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(workspacePath))
            {
                File.Delete(workspacePath);
                File.Delete(SolutionConfigStore.GetBackupPath(workspacePath));
            }
            Directory.Delete(folderPath, recursive: true);
        }
    }

    [Fact]
    public void ProjectTemplateRegistry_ReplacesTemplatesByStableIdAndRaisesChanges()
    {
        string templateId = $"tests.project-template.{Guid.NewGuid():N}";
        var first = new TestProjectTemplate(templateId, "First");
        var replacement = new TestProjectTemplate(templateId, "Replacement");
        int changeCount = 0;
        EventHandler handler = (_, _) => changeCount++;
        ProjectTemplateRegistry.TemplatesChanged += handler;

        try
        {
            ProjectTemplateRegistry.Register(first, priority: 1);
            ProjectTemplateRegistry.Register(replacement, priority: 2);

            IProjectTemplate registered = Assert.Single(
                ProjectTemplateRegistry.GetTemplates(),
                template => template.Id == templateId);
            Assert.Same(replacement, registered);
            Assert.True(changeCount >= 2);
        }
        finally
        {
            ProjectTemplateRegistry.Unregister(templateId);
            ProjectTemplateRegistry.TemplatesChanged -= handler;
        }
    }

    [Fact]
    public void ProjectTemplateCreation_ValidatesProviderAndRemovesFailedDirectories()
    {
        string rootPath = CreateTemporaryDirectory();
        var validTemplate = new TestProjectTemplate(
            $"tests.project-template.valid.{Guid.NewGuid():N}",
            "Valid",
            FolderProjectProvider.ProviderId,
            (projectDirectory, projectName) => FolderProjectProvider.CreateProjectFile(
                Path.Combine(projectDirectory, $"{projectName}.cvproj"),
                projectName));
        var failingTemplate = new TestProjectTemplate(
            $"tests.project-template.failing.{Guid.NewGuid():N}",
            "Failing",
            FolderProjectProvider.ProviderId,
            (projectDirectory, _) =>
            {
                File.WriteAllText(Path.Combine(projectDirectory, "partial.txt"), "partial");
                throw new InvalidOperationException("template failure");
            });
        var wrongProviderTemplate = new TestProjectTemplate(
            $"tests.project-template.wrong-provider.{Guid.NewGuid():N}",
            "Wrong Provider",
            "tests.provider.missing",
            (projectDirectory, projectName) => FolderProjectProvider.CreateProjectFile(
                Path.Combine(projectDirectory, $"{projectName}.cvproj"),
                projectName));

        try
        {
            Assert.True(ProjectTemplateRegistry.TryCreateFromTemplate(
                validTemplate,
                rootPath,
                "ExactName",
                out DirectoryInfo? projectDirectory,
                out string createError));
            Assert.Equal(string.Empty, createError);
            Assert.Equal(Path.Combine(rootPath, "ExactName"), projectDirectory?.FullName, ignoreCase: true);
            Assert.True(ProjectProviderRegistry.TryLoadProject(projectDirectory!, out ProjectDefinition? project));
            Assert.Equal(FolderProjectProvider.ProviderId, project?.ProviderId);

            Assert.False(ProjectTemplateRegistry.TryCreateFromTemplate(
                failingTemplate,
                rootPath,
                "FailedProject",
                out _,
                out string failureError));
            Assert.Contains("template failure", failureError, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(rootPath, "FailedProject")));

            Assert.False(ProjectTemplateRegistry.TryCreateFromTemplate(
                wrongProviderTemplate,
                rootPath,
                "WrongProviderProject",
                out _,
                out string providerError));
            Assert.Contains("tests.provider.missing", providerError, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(rootPath, "WrongProviderProject")));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void ProviderDeclaredProjectFormat_DrivesRoutingDiscoveryAndTreeVisibility()
    {
        string directoryPath = CreateTemporaryDirectory();
        string projectPath = Path.Combine(directoryPath, "Example.mockproj");
        File.WriteAllText(projectPath, "{}");
        ProjectProviderRegistry.Register(new MockProjectProvider(), priority: 1000);

        try
        {
            Assert.True(SolutionManager.IsProjectFilePath(projectPath));
            Assert.Equal(ResourceOpenKind.Project, ResourceOpenService.Classify(projectPath));
            Assert.Contains("*.mockproj", ProjectProviderRegistry.GetProjectFilePatterns(), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("*.mockproj", ProjectProviderRegistry.GetProjectFileDialogPattern(), StringComparison.OrdinalIgnoreCase);
            Assert.True(ProjectProviderRegistry.TryLoadProject(new FileInfo(projectPath), out ProjectDefinition? project));
            Assert.Equal(MockProjectProvider.ProviderId, project?.ProviderId);
            Assert.Equal(projectPath, Assert.Single(ProjectProviderRegistry.EnumerateProjectFiles(
                new DirectoryInfo(directoryPath),
                SearchOption.TopDirectoryOnly)).FullName,
                ignoreCase: true);
            Assert.True(SolutionNodeFactory.IsInternalFile(Path.GetFileName(projectPath)));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void RegisteringProjectProvider_ReplacesUnavailableProjectInOpenSolution()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string projectPath = Path.Combine(solutionDirectory, "Example.lateproj");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        File.WriteAllText(projectPath, "{}");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects = [Path.GetFileName(projectPath)],
        });

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });
            UnavailableProjectNode unavailableNode = Assert.Single(
                explorer.VisualChildren.OfType<UnavailableProjectNode>());
            Assert.Equal(projectPath, unavailableNode.FullPath, ignoreCase: true);
            Assert.Contains("项目引用", unavailableNode.BuildDiagnosticMessage(), StringComparison.Ordinal);
            var unavailableMenuItems = new List<ColorVision.UI.Menus.MenuItemMetadata>();
            unavailableNode.CollectMenuItems(unavailableMenuItems);
            Assert.Contains(unavailableMenuItems, item => item.GuidId == SolutionCommandIds.Refresh);
            Assert.Contains(unavailableMenuItems, item => item.GuidId == "OpenUnavailableProjectContainer");
            Assert.Contains(unavailableMenuItems, item =>
                item.GuidId == SolutionCommandIds.Delete
                && ReferenceEquals(item.Command, ApplicationCommands.Delete));

            ProjectProviderRegistry.Register(new LateProjectProvider(), priority: 1000);

            ProjectNode projectNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            Assert.Equal(LateProjectProvider.ProviderId, projectNode.Project.ProviderId);
            Assert.Empty(explorer.VisualChildren.OfType<UnavailableProjectNode>());
            Assert.Equal(ResourceOpenKind.Project, ResourceOpenService.Classify(projectPath));
            var projectMenuItems = new List<ColorVision.UI.Menus.MenuItemMetadata>();
            projectNode.CollectMenuItems(projectMenuItems);
            Assert.Single(projectMenuItems, item => item.GuidId == SolutionCommandIds.Delete);
            Assert.DoesNotContain(projectMenuItems, item => item.GuidId == "RemoveProjectFromSolution");
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void UnavailableDirectoryProject_UsesRealDiagnosticPathAndRecoversAfterProjectAppears()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(solutionDirectory, "ProjectA");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        Directory.CreateDirectory(projectDirectory);
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects = ["ProjectA"],
        });

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });
            UnavailableProjectNode unavailableNode = Assert.Single(
                explorer.VisualChildren.OfType<UnavailableProjectNode>());
            Assert.Equal(projectDirectory, unavailableNode.ResolvedPath, ignoreCase: true);
            Assert.NotEqual(unavailableNode.ResolvedPath, unavailableNode.FullPath);
            Assert.Contains(projectDirectory, unavailableNode.BuildDiagnosticMessage(), StringComparison.OrdinalIgnoreCase);

            string projectPath = Path.Combine(projectDirectory, "ProjectA.cvproj");
            FolderProjectProvider.CreateProjectFile(projectPath, "Project A");
            explorer.ReloadSolutionState();

            ProjectNode projectNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            Assert.Equal(projectPath, projectNode.Project.ProjectFile.FullName, ignoreCase: true);
            Assert.Empty(explorer.VisualChildren.OfType<UnavailableProjectNode>());
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void ProjectProviderRegistry_ReportsUnsupportedProjectType()
    {
        string directoryPath = CreateTemporaryDirectory();
        string projectPath = Path.Combine(directoryPath, "Unsupported.cvproj");
        File.WriteAllText(projectPath, "{\"ProjectType\":\"vendor.missing\"}");

        try
        {
            Assert.False(ProjectProviderRegistry.TryLoadProject(
                new FileInfo(projectPath),
                out _,
                out string errorMessage));
            Assert.Contains("没有已安装的项目 Provider", errorMessage, StringComparison.Ordinal);
            Assert.Contains("可能缺少对应插件", errorMessage, StringComparison.Ordinal);
            Assert.Contains("vendor.missing", errorMessage, StringComparison.Ordinal);
            Assert.Equal("vendor.missing", ProjectProviderRegistry.GetDeclaredProviderId(new FileInfo(projectPath)));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Theory]
    [InlineData("Example.cvproj.012345.tmp")]
    [InlineData("Example.cvsln.012345.tmp")]
    [InlineData("Example.cvsln.bak")]
    [InlineData("Example.cvsln.corrupt-20260717120000000-012345")]
    public void SolutionNodeFactory_HidesSolutionPersistenceArtifacts(string fileName)
    {
        Assert.True(SolutionNodeFactory.IsInternalFile(fileName));
    }

    [Fact]
    public void SolutionConfigStore_MigratesUnversionedLegacySolutionAndPreservesUnknownFields()
    {
        const string legacyJson = """
            {
              "RootPath": ".",
              "Projects": ["App/App.cvproj"],
              "VendorFeature": { "Enabled": true }
            }
            """;

        SolutionConfig config = SolutionConfigStore.DeserializeAndMigrate(legacyJson, out int sourceSchemaVersion);

        Assert.Equal(0, sourceSchemaVersion);
        Assert.Equal(SolutionConfigStore.CurrentSchemaVersion, config.SchemaVersion);
        Assert.Equal(SolutionProjectMode.Explicit, config.ProjectMode);
        Assert.Equal("Debug", config.ActiveConfiguration);
        Assert.NotNull(config.ProjectConfigurations);
        Assert.NotNull(config.SolutionFolders);
        Assert.NotNull(config.ProjectSolutionFolders);
        Assert.NotNull(config.SolutionItems);
        JObject saved = JObject.Parse(SolutionConfigStore.Serialize(config));
        Assert.Equal(SolutionConfigStore.CurrentSchemaVersion, saved.Value<int>(nameof(SolutionConfig.SchemaVersion)));
        Assert.True(saved["VendorFeature"]?["Enabled"]?.Value<bool>());
    }

    [Fact]
    public void SolutionConfigStore_RejectsFutureSchemaVersion()
    {
        string json = $$"""
            {
              "SchemaVersion": {{SolutionConfigStore.CurrentSchemaVersion + 1}}
            }
            """;

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => SolutionConfigStore.DeserializeAndMigrate(json, out _));

        Assert.Contains("较新的 SchemaVersion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SolutionConfigStore_RecoversCorruptPrimaryFromLastGoodBackup()
    {
        string directoryPath = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(directoryPath, "Example.cvsln");
        try
        {
            SolutionConfigStore.Save(solutionPath, new SolutionConfig { ActiveConfiguration = "Debug" });
            SolutionConfigStore.Save(solutionPath, new SolutionConfig { ActiveConfiguration = "Release" });
            string backupPath = SolutionConfigStore.GetBackupPath(solutionPath);
            Assert.True(File.Exists(backupPath));
            Assert.Equal(
                "Debug",
                SolutionConfigStore.DeserializeAndMigrate(File.ReadAllText(backupPath), out _).ActiveConfiguration);

            File.WriteAllText(solutionPath, "{ not valid json");
            SolutionConfigLoadResult recovered = SolutionConfigStore.Load(solutionPath);

            Assert.True(recovered.RecoveredFromBackup);
            Assert.Equal("Debug", recovered.Config.ActiveConfiguration);
            Assert.False(string.IsNullOrWhiteSpace(recovered.CorruptCopyPath));
            Assert.True(File.Exists(recovered.CorruptCopyPath));
            Assert.Equal(
                "Debug",
                SolutionConfigStore.DeserializeAndMigrate(File.ReadAllText(solutionPath), out _).ActiveConfiguration);
            Assert.Equal(
                "Debug",
                SolutionConfigStore.DeserializeAndMigrate(File.ReadAllText(backupPath), out _).ActiveConfiguration);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void SolutionGlobalMenus_UseSharedRoutedCommands()
    {
        Assert.Equal(SolutionMenuIds.Build, new MenuSolutionBuild().GuidId);
        Assert.Equal(SolutionMenuIds.Debug, new MenuSolutionDebug().GuidId);
        Assert.Same(SolutionProjectCommands.BuildSolution, new MenuBuildSolution().Command);
        Assert.Same(SolutionProjectCommands.ConfigurationManager, new MenuSolutionConfigurationManager().Command);
        Assert.Same(SolutionProjectCommands.Debug, new MenuDebugStartupProject().Command);
        Assert.Same(SolutionProjectCommands.Run, new MenuRunStartupProject().Command);
    }

    [Fact]
    public void SolutionExplorer_ActiveConfigurationPersistsAndNotifiesCommandSurfaces()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        File.WriteAllText(solutionPath, JsonConvert.SerializeObject(new SolutionConfig
        {
            ActiveConfiguration = "Debug",
            RootPath = ".",
        }));

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });
            var changedProperties = new List<string?>();
            explorer.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

            Assert.True(explorer.SetActiveConfiguration("Release"));

            Assert.Equal("Release", explorer.ActiveConfiguration);
            Assert.Contains(nameof(SolutionExplorer.ActiveConfiguration), changedProperties);
            var saved = JsonConvert.DeserializeObject<SolutionConfig>(File.ReadAllText(solutionPath));
            Assert.Equal("Release", saved?.ActiveConfiguration);
            Assert.False(explorer.SetActiveConfiguration("Release"));
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionConfigurationWindow_LoadsAndUpdatesBindingsOnStaThread()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        File.WriteAllText(solutionPath, JsonConvert.SerializeObject(new SolutionConfig
        {
            ActiveConfiguration = "Debug",
            RootPath = ".",
        }));
        CreateConfiguredProject(
            solutionDirectory,
            "App",
            new Dictionary<string, ProjectConfigurationDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["Debug"] = new(new Dictionary<string, ProjectCommandDefinition>
                {
                    [ProjectCapabilityIds.Build] = new("dotnet build -c Debug", "."),
                }),
                ["Release"] = new(new Dictionary<string, ProjectCommandDefinition>
                {
                    [ProjectCapabilityIds.Build] = new("dotnet build -c Release", "."),
                }),
            });

        Exception? threadException = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var explorer = new SolutionExplorer(new SolutionEnvironments
                {
                    SolutionDir = solutionDirectory,
                    SolutionName = "Example",
                    SolutionExt = ".cvsln",
                    SolutionFileName = "Example",
                    SolutionPath = solutionPath,
                });
                var window = new SolutionConfigurationWindow(explorer);
                window.Show();
                window.UpdateLayout();

                var checkableMetadata = new ColorVision.UI.Menus.MenuItemMetadata
                {
                    Header = "Release",
                    IsChecked = true,
                };
                MenuItem checkableMenuItem = ColorVision.UI.Menus.MenuItemIcon.ToMenuItem(checkableMetadata);
                Assert.True(checkableMenuItem.IsCheckable);
                Assert.True(checkableMenuItem.IsChecked);

                var model = Assert.IsType<SolutionConfigurationEditorModel>(window.DataContext);
                var configurationComboBox = Assert.IsType<ComboBox>(window.FindName("ActiveConfigurationComboBox"));
                var projectGrid = Assert.IsType<DataGrid>(window.FindName("ProjectConfigurationDataGrid"));
                var saveButton = Assert.IsType<Button>(window.FindName("SaveButton"));
                Assert.Equal(2, configurationComboBox.Items.Count);
                Assert.Single(projectGrid.Items);
                Assert.True(saveButton.IsEnabled);

                configurationComboBox.SelectedItem = "Release";
                configurationComboBox.GetBindingExpression(ComboBox.SelectedItemProperty)?.UpdateSource();
                Assert.Equal("Release", model.ActiveConfiguration);
                Assert.False(model.HasErrors);
                window.Close();
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);

        try
        {
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "配置管理器窗口未能在 30 秒内完成交互回归。");
            if (threadException != null)
                ExceptionDispatchInfo.Capture(threadException).Throw();
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionFolders_PersistHierarchyMoveProjectsAndNeverDeleteProjectDirectory()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        ProjectDefinition projectA = CreateBuildProject(solutionDirectory, "A");
        ProjectDefinition projectB = CreateBuildProject(solutionDirectory, "B");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        const string rootFolderId = "apps";
        const string childFolderId = "tests";
        string projectAReference = Path.GetRelativePath(solutionDirectory, projectA.ProjectFile.FullName);
        string projectBReference = Path.GetRelativePath(solutionDirectory, projectB.ProjectFile.FullName);
        File.WriteAllText(solutionPath, JsonConvert.SerializeObject(new SolutionConfig
        {
            ActiveConfiguration = "Debug",
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects = [projectAReference, projectBReference],
            SolutionFolders =
            [
                new SolutionFolderDefinition { Id = rootFolderId, Name = "Apps" },
                new SolutionFolderDefinition { Id = childFolderId, Name = "Tests", ParentId = rootFolderId },
            ],
            ProjectSolutionFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [projectAReference] = childFolderId,
            },
        }));

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });

            SolutionFolderNode appsNode = Assert.Single(explorer.VisualChildren.OfType<SolutionFolderNode>());
            SolutionFolderNode testsNode = Assert.Single(appsNode.VisualChildren.OfType<SolutionFolderNode>());
            Assert.Equal("Apps", appsNode.Name);
            Assert.Equal("Tests", testsNode.Name);
            Assert.Equal("A", Assert.Single(testsNode.VisualChildren.OfType<ProjectNode>()).Project.Name);
            ProjectNode projectBNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            Assert.Equal("B", projectBNode.Project.Name);

            Assert.True(explorer.MoveProjectToSolutionFolder(projectBNode.Project, childFolderId));
            appsNode = Assert.Single(explorer.VisualChildren.OfType<SolutionFolderNode>());
            testsNode = Assert.Single(appsNode.VisualChildren.OfType<SolutionFolderNode>());
            Assert.Equal(["A", "B"], testsNode.VisualChildren.OfType<ProjectNode>()
                .Select(node => node.Project.Name)
                .OrderBy(name => name));
            projectBNode = Assert.Single(testsNode.VisualChildren.OfType<ProjectNode>(), node => node.Project.Name == "B");
            Assert.True(explorer.MoveProjectToSolutionFolder(projectBNode.Project, folderId: null));

            Assert.True(explorer.RemoveSolutionFolder(childFolderId));
            appsNode = Assert.Single(explorer.VisualChildren.OfType<SolutionFolderNode>());
            Assert.Equal("A", Assert.Single(appsNode.VisualChildren.OfType<ProjectNode>()).Project.Name);
            Assert.Equal(rootFolderId, explorer.Config.ProjectSolutionFolders[projectAReference]);

            projectBNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            Assert.True(projectBNode.TryDelete(showConfirmation: false));
            Assert.True(projectB.ProjectDirectory.Exists);
            Assert.DoesNotContain(explorer.Config.Projects, reference =>
                SolutionExplorer.ProjectReferencesEqual(solutionDirectory, reference, projectBReference));

            Assert.True(explorer.RemoveSolutionFolder(rootFolderId));
            Assert.Empty(explorer.Config.SolutionFolders);
            Assert.Empty(explorer.Config.ProjectSolutionFolders);
            Assert.Equal("A", Assert.Single(explorer.VisualChildren.OfType<ProjectNode>()).Project.Name);

            var saved = JsonConvert.DeserializeObject<SolutionConfig>(File.ReadAllText(solutionPath));
            Assert.Empty(saved!.SolutionFolders);
            Assert.Empty(saved.ProjectSolutionFolders);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void CreatingSolutionFolder_PreservesAutoDiscoveredProjectsWhenSwitchingToExplicitMode()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        CreateBuildProject(solutionDirectory, "A");
        CreateBuildProject(solutionDirectory, "B");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        File.WriteAllText(solutionPath, JsonConvert.SerializeObject(new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.AutoDiscover,
        }));

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });

            SolutionFolderDefinition folder = explorer.CreateSolutionFolder();

            Assert.Equal(SolutionProjectMode.Explicit, explorer.Config.ProjectMode);
            Assert.Equal(2, explorer.Config.Projects.Count);
            Assert.Equal(folder.Id, Assert.Single(explorer.VisualChildren.OfType<SolutionFolderNode>()).FolderId);
            Assert.Equal(["A", "B"], explorer.VisualChildren.OfType<ProjectNode>()
                .Select(node => node.Project.Name)
                .OrderBy(name => name));
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionOperationHistory_MovesEntriesOnlyAfterSuccessfulApply()
    {
        var history = new SolutionOperationHistory();
        history.Record("新建解决方案文件夹", "before", "after");

        Assert.True(history.CanUndo);
        Assert.False(history.TryUndo(_ => false));
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);

        Assert.True(history.TryUndo(snapshot => snapshot == "before"));
        Assert.False(history.CanUndo);
        Assert.True(history.CanRedo);
        Assert.False(history.TryRedo(_ => false));
        Assert.True(history.CanRedo);
        Assert.True(history.TryRedo(snapshot => snapshot == "after"));
        Assert.True(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void SolutionExplorer_SolutionFolderCreationCanBeUndoneAndRedone()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
        });

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });

            SolutionFolderDefinition createdFolder = explorer.CreateSolutionFolder();
            Assert.True(explorer.CanUndoSolutionOperation);
            Assert.False(explorer.CanRedoSolutionOperation);
            Assert.Equal(createdFolder.Id, Assert.Single(explorer.Config.SolutionFolders).Id);

            Assert.True(explorer.TryUndoSolutionOperation(out string undoError), undoError);
            Assert.Empty(explorer.Config.SolutionFolders);
            Assert.Empty(explorer.VisualChildren.OfType<SolutionFolderNode>());
            Assert.False(explorer.CanUndoSolutionOperation);
            Assert.True(explorer.CanRedoSolutionOperation);
            Assert.Empty(SolutionConfigStore.Load(solutionPath).Config.SolutionFolders);

            Assert.True(explorer.TryRedoSolutionOperation(out string redoError), redoError);
            Assert.Equal(createdFolder.Id, Assert.Single(explorer.Config.SolutionFolders).Id);
            Assert.Equal(createdFolder.Id, Assert.Single(explorer.VisualChildren.OfType<SolutionFolderNode>()).FolderId);
            Assert.True(explorer.CanUndoSolutionOperation);
            Assert.False(explorer.CanRedoSolutionOperation);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionExplorer_MultiDeleteIsOneUndoableSolutionOperation()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            SolutionFolders =
            [
                new SolutionFolderDefinition { Id = "first", Name = "First" },
                new SolutionFolderDefinition { Id = "second", Name = "Second" },
            ],
        });

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });
            List<SolutionNode> folderNodes = explorer.VisualChildren
                .OfType<SolutionFolderNode>()
                .Cast<SolutionNode>()
                .ToList();

            Assert.Empty(explorer.DeleteNodesAsSingleOperation(folderNodes));
            Assert.Empty(explorer.Config.SolutionFolders);
            Assert.True(explorer.CanUndoSolutionOperation);

            Assert.True(explorer.TryUndoSolutionOperation(out string undoError), undoError);
            Assert.Equal(2, explorer.Config.SolutionFolders.Count);
            Assert.False(explorer.CanUndoSolutionOperation);
            Assert.True(explorer.CanRedoSolutionOperation);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionWorkspaceState_RestoresStableExpansionAndSelectionOutsideWorkspace()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string stateDirectory = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            SolutionFolders =
            [
                new SolutionFolderDefinition { Id = "resources", Name = "Resources" },
            ],
        });

        try
        {
            SolutionWorkspaceState persistedState;
            using (var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            }))
            {
                SolutionFolderNode folderNode = Assert.Single(explorer.VisualChildren.OfType<SolutionFolderNode>());
                folderNode.IsExpanded = true;
                persistedState = SolutionWorkspaceStateStore.Capture(explorer, [folderNode], folderNode);
                SolutionWorkspaceStateStore.Save(solutionPath, persistedState, stateDirectory);

                string statePath = SolutionWorkspaceStateStore.GetStateFilePath(solutionPath, stateDirectory);
                Assert.True(File.Exists(statePath));
                Assert.Equal(Path.GetFullPath(stateDirectory), Path.GetDirectoryName(statePath), ignoreCase: true);
                Assert.False(File.Exists(Path.Combine(solutionDirectory, Path.GetFileName(statePath))));
            }

            using var recreatedExplorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });
            SolutionFolderNode recreatedFolder = Assert.Single(recreatedExplorer.VisualChildren.OfType<SolutionFolderNode>());
            recreatedFolder.IsExpanded = false;
            SolutionWorkspaceStateLoadResult loadResult = SolutionWorkspaceStateStore.Load(solutionPath, stateDirectory);

            Assert.True(loadResult.HasPersistedState);
            SolutionWorkspaceStateStore.RestoreExpansion(recreatedExplorer, loadResult.State);
            Assert.True(recreatedFolder.IsExpanded);
            Assert.Same(recreatedFolder, Assert.Single(
                SolutionWorkspaceStateStore.ResolveSelectedNodes(recreatedExplorer, loadResult.State)));
            Assert.Same(recreatedFolder, SolutionWorkspaceStateStore.ResolveAnchorNode(
                recreatedExplorer,
                loadResult.State));
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
            Directory.Delete(stateDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task FolderNode_LoadsChildrenLazilyAndClearsLeafPlaceholder()
    {
        string rootPath = CreateTemporaryDirectory();
        string emptyFolderPath = Path.Combine(rootPath, "01-empty");
        Directory.CreateDirectory(emptyFolderPath);
        File.WriteAllText(Path.Combine(rootPath, "02-file.txt"), string.Empty);

        try
        {
            using FolderNode rootNode = SolutionNodeFactory.CreateFolderNode(new DirectoryInfo(rootPath));
            var changedProperties = new List<string?>();
            rootNode.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);
            var placeholderCollection = rootNode.VisualChildren;
            Assert.IsType<LazyLoadingNode>(Assert.Single(rootNode.VisualChildren));

            await rootNode.EnsureChildrenLoadedAsync();

            Assert.True(rootNode.AreChildrenLoaded);
            Assert.False(rootNode.AreChildrenLoading);
            Assert.NotSame(placeholderCollection, rootNode.VisualChildren);
            Assert.Contains(nameof(SolutionNode.VisualChildren), changedProperties);
            Assert.Equal(["01-empty", "02-file.txt"], rootNode.VisualChildren.Select(node => node.Name));
            FolderNode emptyFolderNode = Assert.IsType<FolderNode>(rootNode.VisualChildren[0]);
            Assert.IsType<LazyLoadingNode>(Assert.Single(emptyFolderNode.VisualChildren));

            await emptyFolderNode.EnsureChildrenLoadedAsync();

            Assert.True(emptyFolderNode.AreChildrenLoaded);
            Assert.Empty(emptyFolderNode.VisualChildren);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task FolderNode_ReloadRejectsStaleChildrenAndUsesLatestSnapshot()
    {
        string rootPath = CreateTemporaryDirectory();
        for (int index = 0; index < 256; index++)
            File.WriteAllText(Path.Combine(rootPath, $"item-{index:D3}.txt"), string.Empty);

        try
        {
            using FolderNode rootNode = SolutionNodeFactory.CreateFolderNode(new DirectoryInfo(rootPath));
            rootNode.IsExpanded = true;
            Task originalLoad = rootNode.EnsureChildrenLoadedAsync();
            string latestFilePath = Path.Combine(rootPath, "latest.txt");
            File.WriteAllText(latestFilePath, string.Empty);

            rootNode.ReloadChildren();
            Task latestLoad = rootNode.EnsureChildrenLoadedAsync();
            await Task.WhenAll(originalLoad, latestLoad);

            Assert.True(rootNode.AreChildrenLoaded);
            Assert.False(rootNode.AreChildrenLoading);
            Assert.Equal(257, rootNode.VisualChildren.Count);
            Assert.Equal(
                rootNode.VisualChildren.Count,
                rootNode.VisualChildren.Select(node => node.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Contains(rootNode.VisualChildren, node =>
                string.Equals(node.FullPath, latestFilePath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task SolutionWorkspaceState_RestoresNestedLazyExpansionAndSelection()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        string parentPath = Path.Combine(solutionDirectory, "Parent");
        string childPath = Path.Combine(parentPath, "Child");
        string filePath = Path.Combine(childPath, "notes.txt");
        Directory.CreateDirectory(childPath);
        File.WriteAllText(filePath, string.Empty);
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.AutoDiscover,
        });

        try
        {
            SolutionWorkspaceState state;
            using (var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath))
            {
                FolderNode parentNode = Assert.Single(
                    explorer.VisualChildren.OfType<FolderNode>(),
                    node => string.Equals(node.FullPath, parentPath, StringComparison.OrdinalIgnoreCase));
                parentNode.IsExpanded = true;
                await parentNode.EnsureChildrenLoadedAsync();
                FolderNode childNode = Assert.Single(parentNode.VisualChildren.OfType<FolderNode>());
                childNode.IsExpanded = true;
                await childNode.EnsureChildrenLoadedAsync();
                FileNode fileNode = Assert.Single(childNode.VisualChildren.OfType<FileNode>());
                state = SolutionWorkspaceStateStore.Capture(explorer, [fileNode], fileNode);
            }

            using var recreatedExplorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            await SolutionWorkspaceStateStore.RestoreExpansionAsync(recreatedExplorer, state);

            FolderNode recreatedParent = Assert.Single(
                recreatedExplorer.VisualChildren.OfType<FolderNode>(),
                node => string.Equals(node.FullPath, parentPath, StringComparison.OrdinalIgnoreCase));
            Assert.True(recreatedParent.IsExpanded);
            Assert.True(recreatedParent.AreChildrenLoaded);
            FolderNode recreatedChild = Assert.Single(recreatedParent.VisualChildren.OfType<FolderNode>());
            Assert.True(recreatedChild.IsExpanded);
            Assert.True(recreatedChild.AreChildrenLoaded);
            FileNode recreatedFile = Assert.Single(recreatedChild.VisualChildren.OfType<FileNode>());
            Assert.Equal(filePath, recreatedFile.FullPath, ignoreCase: true);
            Assert.Same(recreatedFile, Assert.Single(
                SolutionWorkspaceStateStore.ResolveSelectedNodes(recreatedExplorer, state)));
            Assert.Same(recreatedFile, SolutionWorkspaceStateStore.ResolveAnchorNode(recreatedExplorer, state));
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionOrganizationMove_MovesBatchesAndRejectsFolderCycles()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        ProjectDefinition projectA = CreateBuildProject(solutionDirectory, "A");
        ProjectDefinition projectB = CreateBuildProject(solutionDirectory, "B");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        const string appsFolderId = "apps";
        const string testsFolderId = "tests";
        const string toolsFolderId = "tools";
        string projectAReference = Path.GetRelativePath(solutionDirectory, projectA.ProjectFile.FullName);
        string projectBReference = Path.GetRelativePath(solutionDirectory, projectB.ProjectFile.FullName);
        File.WriteAllText(solutionPath, JsonConvert.SerializeObject(new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects = [projectAReference, projectBReference],
            SolutionFolders =
            [
                new SolutionFolderDefinition { Id = appsFolderId, Name = "Apps" },
                new SolutionFolderDefinition { Id = testsFolderId, Name = "Tests", ParentId = appsFolderId },
                new SolutionFolderDefinition { Id = toolsFolderId, Name = "Tools" },
            ],
            ProjectSolutionFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [projectAReference] = testsFolderId,
            },
        }));

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });

            ProjectNode projectBNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            SolutionFolderNode appsNode = Assert.Single(
                explorer.VisualChildren.OfType<SolutionFolderNode>(),
                node => node.FolderId == appsFolderId);
            SolutionFolderNode testsNode = Assert.Single(appsNode.VisualChildren.OfType<SolutionFolderNode>());
            ProjectNode projectANode = Assert.Single(testsNode.VisualChildren.OfType<ProjectNode>());
            SolutionFolderNode originalToolsNode = Assert.Single(
                explorer.VisualChildren.OfType<SolutionFolderNode>(),
                node => node.FolderId == toolsFolderId);
            IReadOnlyList<(string? Id, string DisplayName)> moveOptions =
                explorer.GetSolutionFolderMoveOptions(appsFolderId);
            Assert.Contains(moveOptions, option => option.Id == null);
            Assert.Contains(moveOptions, option => option.Id == toolsFolderId);
            Assert.DoesNotContain(moveOptions, option => option.Id == appsFolderId || option.Id == testsFolderId);
            var menuItems = new List<ColorVision.UI.Menus.MenuItemMetadata>();
            appsNode.CollectMenuItems(menuItems);
            Assert.Contains(menuItems, item => item.GuidId == $"MoveSolutionFolder.{toolsFolderId}");
            Assert.DoesNotContain(menuItems, item => item.GuidId == $"MoveSolutionFolder.{testsFolderId}");

            Assert.False(explorer.CanMoveSolutionItemsToFolder(
                [],
                [appsFolderId],
                testsFolderId,
                out string cycleError));
            Assert.Contains("子文件夹", cycleError);

            Assert.True(explorer.MoveSolutionItemsToFolder(
                [projectBNode.Project],
                [appsFolderId],
                toolsFolderId,
                out string moveError),
                moveError);

            SolutionFolderDefinition appsFolder = Assert.Single(
                explorer.Config.SolutionFolders,
                folder => folder.Id == appsFolderId);
            Assert.Equal(toolsFolderId, appsFolder.ParentId);
            Assert.Equal(toolsFolderId, explorer.Config.ProjectSolutionFolders[projectBReference]);
            SolutionFolderNode toolsNode = Assert.Single(
                explorer.VisualChildren.OfType<SolutionFolderNode>(),
                node => node.FolderId == toolsFolderId);
            Assert.Same(originalToolsNode, toolsNode);
            Assert.Same(appsNode, Assert.Single(
                toolsNode.VisualChildren.OfType<SolutionFolderNode>(),
                node => node.FolderId == appsFolderId));
            Assert.Same(projectBNode, Assert.Single(
                toolsNode.VisualChildren.OfType<ProjectNode>(),
                node => node.Project.Name == "B"));
            Assert.Same(projectANode, Assert.Single(
                appsNode.VisualChildren.GetAllVisualChildren().OfType<ProjectNode>(),
                node => node.Project.Name == "A"));
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void ExplicitProjectRefresh_PreservesNodeIdentityAndUpdatesDefinition()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        ProjectDefinition project = CreateBuildProject(solutionDirectory, "Original");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        string projectReference = Path.GetRelativePath(solutionDirectory, project.ProjectFile.FullName);
        File.WriteAllText(solutionPath, JsonConvert.SerializeObject(new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects = [projectReference],
        }));

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });
            ProjectNode originalNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());

            FolderProjectProvider.CreateProjectFile(
                project.ProjectFile.FullName,
                "Renamed",
                commands: new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    [ProjectCapabilityIds.Run] = new("dotnet run", "."),
                },
                excludedPaths: ["bin"]);
            ProjectDefinition updatedProject = new FolderProjectProvider().Load(project.ProjectFile);
            explorer.ApplyProjectMutation(updatedProject);

            ProjectNode refreshedNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            Assert.Same(originalNode, refreshedNode);
            Assert.Equal("Renamed", refreshedNode.Name);
            Assert.Contains(refreshedNode.Capabilities, capability => capability.Id == ProjectCapabilityIds.Run);
            Assert.Equal(["bin"], refreshedNode.Project.ItemRules!.Exclude);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionItems_MoveAndRemoveReferencesWithoutDeletingPhysicalFiles()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string notesPath = Path.Combine(solutionDirectory, "notes.md");
        File.WriteAllText(notesPath, "# Notes");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        const string docsFolderId = "docs";
        File.WriteAllText(solutionPath, JsonConvert.SerializeObject(new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            SolutionFolders =
            [
                new SolutionFolderDefinition { Id = docsFolderId, Name = "Docs" },
            ],
        }));

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });
            Assert.Contains(explorer.VisualChildren.OfType<FileNode>(), node => node.FullPath == notesPath);

            Assert.True(explorer.RegisterSolutionItems([notesPath], docsFolderId, out string registerError), registerError);
            Assert.Single(explorer.Config.SolutionItems);
            Assert.DoesNotContain(explorer.VisualChildren.OfType<FileNode>(), node => node is not SolutionItemNode && node.FullPath == notesPath);
            SolutionFolderNode docsNode = Assert.Single(
                explorer.VisualChildren.OfType<SolutionFolderNode>(),
                node => node.FolderId == docsFolderId);
            SolutionItemNode solutionItemNode = Assert.Single(docsNode.VisualChildren.OfType<SolutionItemNode>());
            Assert.Equal(notesPath, solutionItemNode.FullPath);
            var menuItems = new List<ColorVision.UI.Menus.MenuItemMetadata>();
            solutionItemNode.CollectMenuItems(menuItems);
            Assert.Contains(menuItems, item => item.GuidId == "MoveSolutionItem.Root");
            Assert.Contains(menuItems, item =>
                item.GuidId == SolutionCommandIds.Delete
                && string.Equals(item.Header?.ToString(), "从解决方案中移除(_V)", StringComparison.Ordinal));

            Assert.True(explorer.MoveSolutionItemsToFolder(
                [],
                [],
                [solutionItemNode.ItemId],
                targetFolderId: null,
                out string moveError),
                moveError);
            Assert.Same(solutionItemNode, Assert.Single(explorer.VisualChildren.OfType<SolutionItemNode>()));

            Assert.True(solutionItemNode.TryDelete(showConfirmation: false));
            Assert.True(File.Exists(notesPath));
            Assert.Empty(explorer.Config.SolutionItems);
            Assert.Contains(explorer.VisualChildren.OfType<FileNode>(), node => node is not SolutionItemNode && node.FullPath == notesPath);

            var saved = JsonConvert.DeserializeObject<SolutionConfig>(File.ReadAllText(solutionPath));
            Assert.Empty(saved!.SolutionItems);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void RegisterDroppedProjects_IsAtomicAndAssignsTheTargetSolutionFolder()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        ProjectDefinition projectA = CreateBuildProject(solutionDirectory, "A");
        ProjectDefinition projectB = CreateBuildProject(solutionDirectory, "B");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        const string folderId = "imports";
        File.WriteAllText(solutionPath, JsonConvert.SerializeObject(new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            SolutionFolders =
            [
                new SolutionFolderDefinition { Id = folderId, Name = "Imports" },
            ],
        }));

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });

            Assert.False(explorer.RegisterDroppedProjects(
                [projectA.ProjectFile.FullName, Path.Combine(solutionDirectory, "missing.cvproj")],
                folderId,
                out string loadError));
            Assert.Contains("不存在", loadError);
            Assert.Empty(explorer.Config.Projects);

            Assert.True(explorer.RegisterDroppedProjects(
                [projectA.ProjectFile.FullName, projectB.ProjectDirectory.FullName],
                folderId,
                out string registerError),
                registerError);
            Assert.Equal(2, explorer.Config.Projects.Count);
            Assert.Equal(2, explorer.Config.ProjectSolutionFolders.Count);
            Assert.All(explorer.Config.ProjectSolutionFolders.Values, value => Assert.Equal(folderId, value));
            SolutionFolderNode importsNode = Assert.Single(explorer.VisualChildren.OfType<SolutionFolderNode>());
            Assert.Equal(["A", "B"], importsNode.VisualChildren.OfType<ProjectNode>()
                .Select(node => node.Project.Name)
                .OrderBy(name => name));

            var saved = JsonConvert.DeserializeObject<SolutionConfig>(File.ReadAllText(solutionPath));
            Assert.Equal(2, saved!.Projects.Count);
            Assert.Equal(2, saved.ProjectSolutionFolders.Count);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void RegisterDroppedSolutionResources_ClassifiesProjectsAndFilesAtomically()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        ProjectDefinition project = CreateBuildProject(solutionDirectory, "App");
        string notesPath = Path.Combine(solutionDirectory, "notes.txt");
        File.WriteAllText(notesPath, "notes");
        string invalidDirectory = Path.Combine(solutionDirectory, "NotAProject");
        Directory.CreateDirectory(invalidDirectory);
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        const string folderId = "imports";
        File.WriteAllText(solutionPath, JsonConvert.SerializeObject(new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            SolutionFolders =
            [
                new SolutionFolderDefinition { Id = folderId, Name = "Imports" },
            ],
        }));

        try
        {
            using var explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });

            Assert.False(explorer.RegisterDroppedSolutionResources(
                [notesPath, invalidDirectory],
                folderId,
                out string invalidError));
            Assert.Contains("Provider", invalidError);
            Assert.Empty(explorer.Config.Projects);
            Assert.Empty(explorer.Config.SolutionItems);

            Assert.True(explorer.RegisterDroppedSolutionResources(
                [project.ProjectFile.FullName, notesPath],
                folderId,
                out IReadOnlyList<SolutionNode> registeredNodes,
                out string registerError),
                registerError);
            Assert.Single(explorer.Config.Projects);
            Assert.Single(explorer.Config.SolutionItems);
            SolutionFolderNode importsNode = Assert.Single(explorer.VisualChildren.OfType<SolutionFolderNode>());
            Assert.Single(importsNode.VisualChildren.OfType<ProjectNode>());
            Assert.Single(importsNode.VisualChildren.OfType<SolutionItemNode>());
            Assert.Equal(2, registeredNodes.Count);
            Assert.Contains(registeredNodes, node => node is ProjectNode);
            Assert.Contains(registeredNodes, node => node is SolutionItemNode);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionExplorer_DisposesResourcesNestedUnderVirtualFolders()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        File.WriteAllText(solutionPath, JsonConvert.SerializeObject(new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            SolutionFolders =
            [
                new SolutionFolderDefinition { Id = "resources", Name = "Resources" },
            ],
        }));
        SolutionExplorer? explorer = null;

        try
        {
            explorer = new SolutionExplorer(new SolutionEnvironments
            {
                SolutionDir = solutionDirectory,
                SolutionName = "Example",
                SolutionExt = ".cvsln",
                SolutionFileName = "Example",
                SolutionPath = solutionPath,
            });
            SolutionFolderNode folderNode = Assert.Single(explorer.VisualChildren.OfType<SolutionFolderNode>());
            var disposableNode = new TrackingDisposableNode();
            folderNode.AddChild(disposableNode);

            explorer.Dispose();
            explorer = null;

            Assert.True(disposableNode.IsDisposed);
        }
        finally
        {
            explorer?.Dispose();
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void FolderProjectProvider_ExposesOnlyConfiguredCapabilities()
    {
        string directoryPath = CreateTemporaryDirectory();
        string projectPath = Path.Combine(directoryPath, "Script.cvproj");
        try
        {
            FolderProjectProvider.CreateProjectFile(
                projectPath,
                "Script",
                commands: new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    [ProjectCapabilityIds.Run] = new("python \"Scripts\\main.py\"", "."),
                    ["publish"] = new("dotnet publish", "."),
                });

            var provider = new FolderProjectProvider();
            ProjectDefinition project = provider.Load(new FileInfo(projectPath));
            IReadOnlyList<ProjectCapabilityDescriptor> capabilities = provider.GetCapabilities(project);
            IReadOnlyList<ProjectCapabilityDescriptor> registeredCapabilities = ProjectProviderRegistry.GetCapabilities(project);

            Assert.Equal([ProjectCapabilityIds.Run, "publish"], capabilities.Select(capability => capability.Id));
            Assert.Equal([ProjectCapabilityIds.Run, "publish"], registeredCapabilities.Select(capability => capability.Id));
            Assert.True(provider.CanExecuteCapability(project, ProjectCapabilityIds.Run));
            Assert.False(provider.CanExecuteCapability(project, ProjectCapabilityIds.Build));
            Assert.Equal(ProjectCapabilityIds.Run, SolutionProjectCommands.GetCapabilityId(SolutionProjectCommands.Run));
            Assert.Contains(
                SolutionProjectCommands.Debug.InputGestures.OfType<KeyGesture>(),
                gesture => gesture.Key == Key.F5 && gesture.Modifiers == ModifierKeys.None);
            Assert.Contains(
                SolutionProjectCommands.Run.InputGestures.OfType<KeyGesture>(),
                gesture => gesture.Key == Key.F5 && gesture.Modifiers == ModifierKeys.Control);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void FolderProjectProvider_LoadsConfigurationOverridesAndKeepsLegacyCommands()
    {
        string directoryPath = CreateTemporaryDirectory();
        string projectPath = Path.Combine(directoryPath, "Configured.cvproj");
        try
        {
            FolderProjectProvider.CreateProjectFile(
                projectPath,
                "Configured",
                commands: new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    [ProjectCapabilityIds.Build] = new("dotnet build -c {Configuration}", "."),
                    ["publish"] = new("dotnet publish", "."),
                },
                configurations: new Dictionary<string, ProjectConfigurationDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Debug"] = new(new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        [ProjectCapabilityIds.Run] = new("dotnet run -c Debug", "."),
                    }),
                    ["Release"] = new(new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        [ProjectCapabilityIds.Run] = new("dotnet run -c Release", "."),
                    }),
                });

            ProjectDefinition project = new FolderProjectProvider().Load(new FileInfo(projectPath));
            ProjectDefinition releaseProject = project.ForConfiguration("release");
            ProjectDefinition missingConfiguration = project.ForConfiguration("Missing");

            Assert.Equal(["Debug", "Release"], project.Configurations!.Keys.OrderBy(name => name));
            Assert.Equal("Release", releaseProject.ActiveConfiguration);
            Assert.Equal(
                [ProjectCapabilityIds.Build, ProjectCapabilityIds.Run, "publish"],
                ProjectProviderRegistry.GetCapabilities(releaseProject).Select(capability => capability.Id));
            Assert.True(ProjectProviderRegistry.TryCreateCapabilityInvocation(
                releaseProject,
                ProjectCapabilityIds.Build,
                out ProjectCommandInvocation? invocation));
            Assert.Equal("dotnet build -c Release", invocation?.Command);
            Assert.False(ProjectProviderRegistry.HasCapability(missingConfiguration, ProjectCapabilityIds.Run));

            JObject json = JObject.Parse(File.ReadAllText(projectPath));
            Assert.Equal("dotnet run -c Release", json["Configurations"]!["Release"]!["Commands"]!["run"]!["Command"]);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void SolutionConfiguration_MapsAndListsProjectConfigurations()
    {
        string containerPath = CreateTemporaryDirectory();
        try
        {
            ProjectDefinition project = CreateConfiguredProject(
                containerPath,
                "App",
                new Dictionary<string, ProjectConfigurationDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Development"] = new(new Dictionary<string, ProjectCommandDefinition>()),
                    ["Production"] = new(new Dictionary<string, ProjectCommandDefinition>()),
                });
            var mappings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                [Path.Combine("App", "App.cvproj")] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Debug"] = "Development",
                    ["Release"] = "Production",
                    ["Staging"] = "Development",
                },
            };

            string mappedConfiguration = SolutionExplorer.ResolveProjectConfigurationName(
                containerPath,
                "debug",
                mappings,
                project);
            string defaultConfiguration = SolutionExplorer.ResolveProjectConfigurationName(
                containerPath,
                "release",
                null,
                project);
            IReadOnlyList<string> availableConfigurations = SolutionExplorer.GetAvailableSolutionConfigurations(
                [project],
                "Debug",
                mappings);

            Assert.Equal("Development", mappedConfiguration);
            Assert.Equal("release", defaultConfiguration);
            Assert.Equal(["Debug", "Release", "Development", "Production", "Staging"], availableConfigurations);
        }
        finally
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Fact]
    public void SolutionConfiguration_SerializesActiveConfigurationAndProjectMappings()
    {
        var config = new SolutionConfig
        {
            ActiveConfiguration = "Release",
            ProjectConfigurations = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["App/App.cvproj"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Release"] = "Production",
                },
            },
        };

        string json = JsonConvert.SerializeObject(config);
        SolutionConfig restored = JsonConvert.DeserializeObject<SolutionConfig>(json)!;

        Assert.Equal("Release", restored.ActiveConfiguration);
        Assert.Equal("Production", restored.ProjectConfigurations["App/App.cvproj"]["Release"]);
    }

    [Fact]
    public void SolutionConfigurationEditorModel_DetectsCyclesAndCreatesDependencyChanges()
    {
        string containerPath = CreateTemporaryDirectory();
        try
        {
            ProjectDefinition projectA = CreateBuildProject(containerPath, "A");
            ProjectDefinition projectB = CreateBuildProject(containerPath, "B");
            var model = new SolutionConfigurationEditorModel(
                containerPath,
                [projectA, projectB],
                "Debug",
                startupProjectReference: null,
                projectConfigurations: null);
            SolutionConfigurationProjectModel rowA = Assert.Single(model.Projects, row => row.Project.Name == "A");
            SolutionConfigurationProjectModel rowB = Assert.Single(model.Projects, row => row.Project.Name == "B");

            Assert.False(model.HasErrors);
            Assert.Single(rowA.Dependencies).IsSelected = true;
            Assert.Single(rowB.Dependencies).IsSelected = true;

            Assert.True(model.HasErrors);
            Assert.Contains(model.Diagnostics, diagnostic => diagnostic.Message.Contains("循环依赖", StringComparison.Ordinal));

            Assert.Single(rowB.Dependencies).IsSelected = false;
            model.ActiveConfiguration = "Release";
            SolutionConfigurationChanges changes = model.CreateChanges();

            Assert.False(model.HasErrors);
            Assert.Equal("Release", changes.ActiveConfiguration);
            Assert.Equal(["../B/B.cvproj"], changes.ProjectDependencies[projectA.ProjectFile.FullName]);
            Assert.Empty(changes.ProjectDependencies[projectB.ProjectFile.FullName]);
        }
        finally
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Fact]
    public void SolutionConfigurationEditorModel_MapsValidProjectConfigurationAndRemovesMissingDependency()
    {
        string containerPath = CreateTemporaryDirectory();
        try
        {
            ProjectDefinition project = CreateConfiguredProject(
                containerPath,
                "App",
                new Dictionary<string, ProjectConfigurationDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Debug"] = new(new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        [ProjectCapabilityIds.Run] = new("dotnet run", "."),
                    }),
                }) with
            {
                Dependencies = ["../Missing/Missing.cvproj"],
            };
            string startupReference = Path.GetRelativePath(containerPath, project.ProjectFile.FullName);
            var model = new SolutionConfigurationEditorModel(
                containerPath,
                [project],
                "Release",
                startupReference,
                projectConfigurations: null);
            SolutionConfigurationProjectModel row = Assert.Single(model.Projects);

            Assert.True(model.HasErrors);
            Assert.Contains(model.Diagnostics, diagnostic => diagnostic.Message.Contains("不存在配置", StringComparison.Ordinal));
            Assert.Contains(model.Diagnostics, diagnostic => diagnostic.Message.Contains("不存在或未加入", StringComparison.Ordinal));

            row.SelectedConfiguration = "Debug";
            SolutionDependencyOption missingDependency = Assert.Single(row.Dependencies);
            Assert.False(missingDependency.IsAvailable);
            missingDependency.IsSelected = false;
            SolutionConfigurationChanges changes = model.CreateChanges();

            Assert.False(model.HasErrors);
            Assert.Equal(
                "Debug",
                changes.ProjectConfigurations["App/App.cvproj"]["Release"]);
            Assert.Empty(changes.ProjectDependencies[project.ProjectFile.FullName]);
        }
        finally
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Fact]
    public void ProjectBuildExecutor_UsesSelectedConfigurationCommand()
    {
        string containerPath = CreateTemporaryDirectory();
        try
        {
            ProjectDefinition project = CreateConfiguredProject(
                containerPath,
                "App",
                new Dictionary<string, ProjectConfigurationDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Release"] = new(new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                    {
                        [ProjectCapabilityIds.Build] = new("dotnet build -c {Configuration}", "."),
                    }),
                }).ForConfiguration("Release");

            var plan = new ProjectBuildPlan([project], Array.Empty<ProjectBuildDiagnostic>());

            Assert.True(ProjectBuildExecutor.TryCreateCommandBatch(plan, out var commands, out string errorMessage));
            TerminalCommandRequest command = Assert.Single(commands);
            Assert.Equal(string.Empty, errorMessage);
            Assert.Equal("生成 App (Release)", command.DisplayName);
            Assert.Equal("dotnet build -c Release", command.Command);
        }
        finally
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Fact]
    public void StartupProjectSelection_PrefersConfiguredReferenceAndDefaultsToRunnableProject()
    {
        string containerPath = CreateTemporaryDirectory();
        try
        {
            ProjectDefinition buildOnly = CreateCommandProject(
                containerPath,
                "BuildOnly",
                new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    [ProjectCapabilityIds.Build] = new("dotnet build", "."),
                });
            ProjectDefinition runnable = CreateCommandProject(
                containerPath,
                "Runnable",
                new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    [ProjectCapabilityIds.Run] = new("dotnet run", "."),
                });
            ProjectDefinition debuggable = CreateCommandProject(
                containerPath,
                "Debuggable",
                new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    [ProjectCapabilityIds.Debug] = new("dotnet run --configuration Debug", "."),
                });

            ProjectDefinition? defaultProject = SolutionExplorer.SelectStartupProject(
                [buildOnly, runnable, debuggable],
                containerPath,
                configuredReference: null);
            ProjectDefinition? configuredProject = SolutionExplorer.SelectStartupProject(
                [buildOnly, runnable, debuggable],
                containerPath,
                Path.Combine("Debuggable", "Debuggable.cvproj"));
            ProjectDefinition? unavailableProject = SolutionExplorer.SelectStartupProject(
                [buildOnly, runnable, debuggable],
                containerPath,
                Path.Combine("Missing", "Missing.cvproj"));

            Assert.Same(runnable, defaultProject);
            Assert.Same(debuggable, configuredProject);
            Assert.Null(unavailableProject);
        }
        finally
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Fact]
    public void ProjectBuildPlanner_OrdersDependenciesBeforeTarget()
    {
        string containerPath = CreateTemporaryDirectory();
        try
        {
            ProjectDefinition projectC = CreateBuildProject(containerPath, "C");
            ProjectDefinition projectB = CreateBuildProject(containerPath, "B", ["../C/C.cvproj"]);
            ProjectDefinition projectA = CreateBuildProject(containerPath, "A", ["../B/B.cvproj"]);

            ProjectBuildPlan plan = ProjectBuildPlanner.Create(
                [projectA, projectC, projectB],
                [projectA]);

            Assert.True(plan.IsValid);
            Assert.Equal(["C", "B", "A"], plan.OrderedProjects.Select(project => project.Name));
            Assert.True(ProjectBuildExecutor.TryCreateCommandBatch(plan, out var commands, out string errorMessage));
            Assert.Equal(string.Empty, errorMessage);
            Assert.Equal(["生成 C", "生成 B", "生成 A"], commands.Select(command => command.DisplayName));
        }
        finally
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Fact]
    public void ProjectBuildPlanner_ReportsCircularDependencies()
    {
        string containerPath = CreateTemporaryDirectory();
        try
        {
            ProjectDefinition projectA = CreateBuildProject(containerPath, "A", ["../B/B.cvproj"]);
            ProjectDefinition projectB = CreateBuildProject(containerPath, "B", ["../A/A.cvproj"]);

            ProjectBuildPlan plan = ProjectBuildPlanner.Create([projectA, projectB], [projectA]);

            ProjectBuildDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
            Assert.False(plan.IsValid);
            Assert.Equal(ProjectBuildDiagnosticKind.CircularDependency, diagnostic.Kind);
            Assert.Contains("A → B → A", diagnostic.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Fact]
    public void ProjectBuildPlanner_ReportsMissingDependencies()
    {
        string containerPath = CreateTemporaryDirectory();
        try
        {
            ProjectDefinition project = CreateBuildProject(containerPath, "A", ["../Missing/Missing.cvproj"]);

            ProjectBuildPlan plan = ProjectBuildPlanner.Create([project], [project]);

            ProjectBuildDiagnostic diagnostic = Assert.Single(plan.Diagnostics);
            Assert.False(plan.IsValid);
            Assert.Equal(ProjectBuildDiagnosticKind.MissingDependency, diagnostic.Kind);
            Assert.Contains("Missing.cvproj", diagnostic.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Fact]
    public void TerminalBatchCommand_PreservesOrderAndStopsOnFailure()
    {
        TerminalCommandRequest[] commands =
        [
            new("生成 Core", "dotnet build Core.csproj", @"C:\Work\Core"),
            new("生成 App", "dotnet build App.csproj", @"C:\Work\App"),
        ];

        string powerShell = TerminalControl.BuildBatchCommand(commands, "powershell");
        string cmd = TerminalControl.BuildBatchCommand(commands, "cmd");

        Assert.True(powerShell.IndexOf("生成 Core", StringComparison.Ordinal) < powerShell.IndexOf("生成 App", StringComparison.Ordinal));
        Assert.Contains("if (-not $?)", powerShell, StringComparison.Ordinal);
        Assert.True(cmd.IndexOf("生成 Core", StringComparison.Ordinal) < cmd.IndexOf("生成 App", StringComparison.Ordinal));
        Assert.Contains(" && ", cmd, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitProjectMode_UsesReferencesWhileFolderModeAutoDiscovers()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(solutionDirectory, "ProjectA");
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, "ProjectA.cvproj");
        FolderProjectProvider.CreateProjectFile(projectPath, "Project A");
        ProjectDefinition project = new FolderProjectProvider().Load(new FileInfo(projectPath));

        try
        {
            Assert.True(SolutionExplorer.IsProjectReferenceIncluded(
                SolutionProjectMode.AutoDiscover,
                solutionDirectory,
                [],
                project));
            Assert.False(SolutionExplorer.IsProjectReferenceIncluded(
                SolutionProjectMode.Explicit,
                solutionDirectory,
                [],
                project));
            Assert.True(SolutionExplorer.IsProjectReferenceIncluded(
                SolutionProjectMode.Explicit,
                solutionDirectory,
                [Path.Combine("ProjectA", "ProjectA.cvproj")],
                project));
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void ProjectReferenceResolver_SupportsFileAndLegacyDirectoryReferences()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(solutionDirectory, "ProjectA");
        Directory.CreateDirectory(projectDirectory);
        string projectPath = Path.Combine(projectDirectory, "ProjectA.cvproj");
        FolderProjectProvider.CreateProjectFile(projectPath, "Project A");

        try
        {
            Assert.True(SolutionExplorer.TryResolveProjectReference(
                solutionDirectory,
                Path.Combine("ProjectA", "ProjectA.cvproj"),
                out ProjectDefinition? fileProject,
                out _));
            Assert.Equal(projectPath, fileProject?.ProjectFile.FullName, ignoreCase: true);

            Assert.True(SolutionExplorer.TryResolveProjectReference(
                solutionDirectory,
                "ProjectA",
                out ProjectDefinition? directoryProject,
                out _));
            Assert.Equal(projectPath, directoryProject?.ProjectFile.FullName, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData(false, "notes.txt")]
    [InlineData(true, "notes.txt *")]
    public void EditorDocumentTitle_ReflectsDirtyState(bool isDirty, string expected)
    {
        Assert.Equal(expected, EditorDocumentService.FormatTitle("notes.txt", isDirty));
    }

    [Fact]
    public void DocumentResourcePathMapping_TracksFileAndFolderRenames()
    {
        string rootPath = CreateTemporaryDirectory();
        try
        {
            string oldFolderPath = Path.Combine(rootPath, "Old");
            string newFolderPath = Path.Combine(rootPath, "New");
            string nestedFilePath = Path.Combine(oldFolderPath, "Nested", "notes.txt");

            Assert.True(EditorDocumentService.TryMapResourcePathAfterRename(
                nestedFilePath,
                oldFolderPath,
                newFolderPath,
                out string mappedFilePath));
            Assert.Equal(Path.Combine(newFolderPath, "Nested", "notes.txt"), mappedFilePath, ignoreCase: true);

            string renamedFilePath = Path.Combine(newFolderPath, "Nested", "renamed.txt");
            Assert.True(EditorDocumentService.TryMapResourcePathAfterRename(
                mappedFilePath,
                mappedFilePath,
                renamedFilePath,
                out string mappedRenamedFilePath));
            Assert.Equal(renamedFilePath, mappedRenamedFilePath, ignoreCase: true);

            string siblingPath = Path.Combine(rootPath, "OldSibling", "notes.txt");
            Assert.False(EditorDocumentService.TryMapResourcePathAfterRename(
                siblingPath,
                oldFolderPath,
                newFolderPath,
                out string unchangedPath));
            Assert.Equal(siblingPath, unchangedPath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void ImplicitProjectSolution_PreservesProjectIdentityAndRoot()
    {
        string projectDirectory = CreateTemporaryDirectory();
        string projectPath = Path.Combine(projectDirectory, "ProjectA.cvproj");
        FolderProjectProvider.CreateProjectFile(projectPath, "Project A");
        string? implicitSolutionPath = null;

        try
        {
            Assert.True(SolutionManager.TryCreateImplicitProjectSolution(
                new FileInfo(projectPath),
                out implicitSolutionPath,
                out string displayName));

            Assert.Equal("Project A", displayName);
            Assert.True(File.Exists(implicitSolutionPath));
            SolutionConfig config = JsonConvert.DeserializeObject<SolutionConfig>(File.ReadAllText(implicitSolutionPath))!;
            Assert.Equal(SolutionProjectMode.Explicit, config.ProjectMode);
            Assert.Equal(projectDirectory, config.RootPath, ignoreCase: true);
            Assert.Contains("ProjectA.cvproj", config.Projects, StringComparer.OrdinalIgnoreCase);
            Assert.Equal("ProjectA.cvproj", config.StartupProject, ignoreCase: true);
            Assert.Equal("Debug", config.ActiveConfiguration);
            Assert.Equal(
                projectDirectory,
                SolutionExplorer.ResolveRootDirectory(new FileInfo(implicitSolutionPath), config.RootPath).FullName,
                ignoreCase: true);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(implicitSolutionPath) && File.Exists(implicitSolutionPath))
                File.Delete(implicitSolutionPath);
            Directory.Delete(projectDirectory, recursive: true);
        }
    }

    [Fact]
    public void FolderEditors_UsePathAwareBrowserAsDefault()
    {
        IReadOnlyList<EditorDescriptor> descriptors = EditorManager.Instance.GetFolderEditorDescriptors();
        EditorDescriptor browser = Assert.Single(descriptors, descriptor => descriptor.EditorType == typeof(ProjectEditor));

        Assert.True(browser.IsDefault);
        Assert.Equal("colorvision.folder.project-list", browser.Id);
        Assert.DoesNotContain(descriptors, descriptor => descriptor.EditorType == typeof(SolutionEditor));
    }

    private static EditorDescriptor CreateFileDescriptor<T>(
        string id,
        bool isGeneric = false,
        bool isDefault = false,
        int priority = 0)
    {
        return new EditorDescriptor(
            id,
            typeof(T),
            EditorResourceKind.File,
            isGeneric ? [] : [".test"],
            isGeneric,
            isDefault,
            priority,
            IsVisibleInOpenWith: true);
    }

    private static SolutionNode CreateNode(string path)
    {
        return new SolutionNode { FullPath = path };
    }

    private static string CreateTemporaryDirectory()
    {
        string directoryPath = Path.Combine(Path.GetTempPath(), $"ColorVision.Solution.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static SolutionExplorer CreateSolutionExplorer(string solutionDirectory, string solutionPath)
    {
        return new SolutionExplorer(new SolutionEnvironments
        {
            SolutionDir = solutionDirectory,
            SolutionName = Path.GetFileNameWithoutExtension(solutionPath),
            SolutionExt = Path.GetExtension(solutionPath),
            SolutionFileName = Path.GetFileNameWithoutExtension(solutionPath),
            SolutionPath = solutionPath,
        });
    }

    private static ProjectDefinition CreateBuildProject(
        string containerPath,
        string projectName,
        IReadOnlyList<string>? dependencies = null)
    {
        string projectDirectory = Path.Combine(containerPath, projectName);
        Directory.CreateDirectory(projectDirectory);
        string projectFilePath = Path.Combine(projectDirectory, $"{projectName}.cvproj");
        FolderProjectProvider.CreateProjectFile(
            projectFilePath,
            projectName,
            commands: new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [ProjectCapabilityIds.Build] = new($"dotnet build {projectName}.csproj", "."),
            },
            dependencies: dependencies);
        return new FolderProjectProvider().Load(new FileInfo(projectFilePath));
    }

    private static ProjectDefinition CreateCommandProject(
        string containerPath,
        string projectName,
        IReadOnlyDictionary<string, ProjectCommandDefinition> commands)
    {
        string projectDirectory = Path.Combine(containerPath, projectName);
        Directory.CreateDirectory(projectDirectory);
        string projectFilePath = Path.Combine(projectDirectory, $"{projectName}.cvproj");
        FolderProjectProvider.CreateProjectFile(projectFilePath, projectName, commands: commands);
        return new FolderProjectProvider().Load(new FileInfo(projectFilePath));
    }

    private static ProjectDefinition CreateConfiguredProject(
        string containerPath,
        string projectName,
        IReadOnlyDictionary<string, ProjectConfigurationDefinition> configurations)
    {
        string projectDirectory = Path.Combine(containerPath, projectName);
        Directory.CreateDirectory(projectDirectory);
        string projectFilePath = Path.Combine(projectDirectory, $"{projectName}.cvproj");
        FolderProjectProvider.CreateProjectFile(projectFilePath, projectName, configurations: configurations);
        return new FolderProjectProvider().Load(new FileInfo(projectFilePath));
    }

    private sealed class SpecializedEditor { }
    private sealed class AlternateEditor { }
    private sealed class GenericEditor { }

    private sealed class TrackingEditor : IEditor
    {
        public static string? LastOpenedPath { get; set; }

        public void Open(string filePath)
        {
            LastOpenedPath = filePath;
        }
    }

    private sealed class TrackingDisposableNode : SolutionNode, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    private sealed class TestProjectTemplate : IProjectTemplate
    {
        private readonly Action<string, string> _createProject;

        public string Id { get; }
        public string? ProjectProviderId { get; }
        public string Name { get; }
        public string Category => "Tests";
        public string Description => Name;
        public int Order => 0;
        public System.Windows.Media.ImageSource? Icon => null;

        public TestProjectTemplate(
            string id,
            string name,
            string? projectProviderId = null,
            Action<string, string>? createProject = null)
        {
            Id = id;
            Name = name;
            ProjectProviderId = projectProviderId;
            _createProject = createProject ?? ((_, _) => { });
        }

        public void CreateProject(string projectDir, string projectName)
        {
            _createProject(projectDir, projectName);
        }
    }

    private sealed class MockProjectProvider : IProjectProvider, IProjectFileFormatProvider
    {
        public const string ProviderId = "tests.mock-project";

        public string Id => ProviderId;
        public IReadOnlyList<string> ProjectFilePatterns { get; } = ["*.mockproj"];

        public bool CanLoad(FileInfo projectFile) =>
            projectFile.Exists
            && string.Equals(projectFile.Extension, ".mockproj", StringComparison.OrdinalIgnoreCase);

        public ProjectDefinition Load(FileInfo projectFile) => new(
            Id,
            Path.GetFileNameWithoutExtension(projectFile.Name),
            "1.0",
            projectFile,
            RootDirectory: projectFile.Directory);
    }

    private sealed class LateProjectProvider : IProjectProvider, IProjectFileFormatProvider
    {
        public const string ProviderId = "tests.late-project";

        public string Id => ProviderId;
        public IReadOnlyList<string> ProjectFilePatterns { get; } = ["*.lateproj"];

        public bool CanLoad(FileInfo projectFile) =>
            projectFile.Exists
            && string.Equals(projectFile.Extension, ".lateproj", StringComparison.OrdinalIgnoreCase);

        public ProjectDefinition Load(FileInfo projectFile) => new(
            Id,
            Path.GetFileNameWithoutExtension(projectFile.Name),
            "1.0",
            projectFile,
            RootDirectory: projectFile.Directory);
    }
}
