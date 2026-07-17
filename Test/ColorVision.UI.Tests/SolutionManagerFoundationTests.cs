using ColorVision.Solution;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Explorer;
using ColorVision.Solution.FileMeta;
using ColorVision.Solution.FolderMeta;
using ColorVision.Solution.Terminal;
using ColorVision.Solution.Workspace;
using ColorVision.UI.Menus;
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
    public void EditorManager_UsesRegisteredFactoryForEditorsWithDependencies()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string extension = $".factory{suffix}";
        string editorId = $"tests.factory.{suffix}";
        string invalidFactoryId = $"tests.factory.invalid.{suffix}";
        const string dependency = "injected dependency";
        EditorManager.Instance.RegisterEditor(CreateFileDescriptor<FactoryOnlyEditor>(
            editorId,
            isDefault: true,
            extension: extension,
            factory: () => FactoryOnlyEditor.Create(dependency)));
        EditorManager.Instance.RegisterEditor(CreateFileDescriptor<FactoryOnlyEditor>(
            invalidFactoryId,
            extension: extension,
            factory: () => new TrackingEditor()));
        string directoryPath = CreateTemporaryDirectory();
        string filePath = Path.Combine(directoryPath, $"Example{extension}");
        File.WriteAllText(filePath, "test");

        try
        {
            FactoryOnlyEditor.LastDependency = null;
            FactoryOnlyEditor.LastOpenedPath = null;

            Assert.True(EditorManager.Instance.OpenFileWith(
                filePath,
                editorId,
                out string errorMessage),
                errorMessage);
            Assert.Equal(dependency, FactoryOnlyEditor.LastDependency);
            Assert.Equal(filePath, FactoryOnlyEditor.LastOpenedPath, ignoreCase: true);

            Assert.False(EditorManager.Instance.OpenFileWith(
                filePath,
                invalidFactoryId,
                out string invalidFactoryError));
            Assert.Contains("声明类型不一致", invalidFactoryError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ResourceOpenService_SeparatesProjectActivationFromExplicitEditing()
    {
        string directoryPath = CreateTemporaryDirectory();
        string projectPath = Path.Combine(directoryPath, "Example.cvproj");
        File.WriteAllText(projectPath, "{}");

        try
        {
            Assert.Equal(ResourceOpenKind.Project, ResourceOpenService.Classify(projectPath));

            IReadOnlyList<EditorDescriptor> projectEditors =
                ResourceOpenService.Instance.GetOpenWithEditors(projectPath);
            IReadOnlyList<EditorDescriptor> folderEditors =
                ResourceOpenService.Instance.GetOpenWithEditors(directoryPath);

            Assert.Contains(projectEditors, descriptor =>
                descriptor.ResourceKind == EditorResourceKind.File
                && descriptor.EditorType == typeof(SystemEditor));
            Assert.NotEmpty(folderEditors);
            Assert.All(folderEditors, descriptor =>
                Assert.Equal(EditorResourceKind.Folder, descriptor.ResourceKind));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ResourceOpenService_IsolatesBrokenEditorsAndPreservesDefaultAssociation()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string extension = $".open{suffix}";
        string trackingId = $"tests.open.tracking.{suffix}";
        string throwingOpenId = $"tests.open.throwing.{suffix}";
        string throwingConstructorId = $"tests.open.constructor.{suffix}";
        EditorManager manager = EditorManager.Instance;
        manager.RegisterEditor(CreateFileDescriptor<TrackingEditor>(
            trackingId,
            isDefault: true,
            extension: extension));
        manager.RegisterEditor(CreateFileDescriptor<ThrowingOpenEditor>(
            throwingOpenId,
            priority: 100,
            extension: extension));
        manager.RegisterEditor(CreateFileDescriptor<ThrowingConstructorEditor>(
            throwingConstructorId,
            priority: 90,
            extension: extension));
        string directoryPath = CreateTemporaryDirectory();
        string filePath = Path.Combine(directoryPath, $"Example{extension}");
        File.WriteAllText(filePath, "test");

        try
        {
            Assert.Equal(trackingId, manager.GetDefaultFileEditorDescriptor(extension)?.Id);

            ResourceOpenResult throwingOpenResult = ResourceOpenService.Instance.OpenWith(
                filePath,
                throwingOpenId,
                setAsDefault: true);
            Assert.False(throwingOpenResult.Succeeded);
            Assert.False(throwingOpenResult.DefaultEditorUpdated);
            Assert.Contains("Open failed", throwingOpenResult.ErrorMessage, StringComparison.Ordinal);
            Assert.Equal(trackingId, manager.GetDefaultFileEditorDescriptor(extension)?.Id);

            ResourceOpenResult throwingConstructorResult = ResourceOpenService.Instance.OpenWith(
                filePath,
                throwingConstructorId);
            Assert.False(throwingConstructorResult.Succeeded);
            Assert.Contains("Constructor failed", throwingConstructorResult.ErrorMessage, StringComparison.Ordinal);

            TrackingEditor.LastOpenedPath = null;
            ResourceOpenResult successfulResult = ResourceOpenService.Instance.OpenWith(
                filePath,
                trackingId);
            Assert.True(successfulResult.Succeeded);
            Assert.Null(successfulResult.DefaultEditorUpdated);
            Assert.Equal(filePath, TrackingEditor.LastOpenedPath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ResourceOpenService_BatchOpensFilesWithoutReplacingWorkspaces()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string extension = $".batch{suffix}";
        string failingExtension = $".batchfail{suffix}";
        EditorManager manager = EditorManager.Instance;
        manager.RegisterEditor(CreateFileDescriptor<BatchRecordingEditor>(
            $"tests.batch.{suffix}",
            isDefault: true,
            extension: extension));
        manager.RegisterEditor(CreateFileDescriptor<ThrowingOpenEditor>(
            $"tests.batch.fail.{suffix}",
            isDefault: true,
            extension: failingExtension));
        string directoryPath = CreateTemporaryDirectory();
        string firstPath = Path.Combine(directoryPath, $"First{extension}");
        string secondPath = Path.Combine(directoryPath, $"Second{extension}");
        string failingPath = Path.Combine(directoryPath, $"Failed{failingExtension}");
        string solutionPath = Path.Combine(directoryPath, "Nested.cvsln");
        File.WriteAllText(firstPath, "first");
        File.WriteAllText(secondPath, "second");
        File.WriteAllText(failingPath, "failed");
        File.WriteAllText(solutionPath, "{}");

        try
        {
            BatchRecordingEditor.OpenedPaths.Clear();
            ResourceOpenService service = ResourceOpenService.Instance;

            Assert.True(ResourceOpenService.CanOpenTogether([firstPath, secondPath]));
            Assert.False(ResourceOpenService.CanOpenTogether([firstPath, solutionPath]));

            ResourceOpenBatchResult result = service.OpenMany(
                [firstPath, solutionPath, secondPath, failingPath]);

            Assert.False(result.IsComplete);
            Assert.Equal(4, result.RequestedCount);
            Assert.Equal([firstPath, secondPath], result.SuccessfulPaths);
            Assert.Equal([firstPath, secondPath], BatchRecordingEditor.OpenedPaths);
            Assert.Contains(result.Failures, failure =>
                failure.Kind == ResourceOpenKind.Solution
                && failure.ResourcePath == solutionPath);
            Assert.Contains(result.Failures, failure =>
                failure.Kind == ResourceOpenKind.File
                && failure.ResourcePath == failingPath
                && failure.Message.Contains("Open failed", StringComparison.Ordinal));
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

        Assert.False(SolutionClipboardFileOperations.IsSafeDirectoryCopyDestination(sourcePath, sourcePath));
        Assert.False(SolutionClipboardFileOperations.IsSafeDirectoryCopyDestination(sourcePath, descendantPath));
        Assert.True(SolutionClipboardFileOperations.IsSafeDirectoryCopyDestination(sourcePath, siblingPath));
    }

    [Fact]
    public void ClipboardFileOperations_ReportPartialMovesAndKeepFailedSources()
    {
        string rootPath = CreateTemporaryDirectory();
        string sourceDirectory = Path.Combine(rootPath, "Source");
        string targetDirectory = Path.Combine(rootPath, "Target");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(targetDirectory);
        string firstSource = Path.Combine(sourceDirectory, "first.txt");
        string secondSource = Path.Combine(sourceDirectory, "second.txt");
        File.WriteAllText(firstSource, "first");
        File.WriteAllText(secondSource, "second");
        File.WriteAllText(Path.Combine(targetDirectory, "second.txt"), "existing");

        try
        {
            SolutionFileOperationResult moveResult = SolutionClipboardFileOperations.Execute(
                [firstSource, secondSource],
                targetDirectory,
                isMove: true);

            Assert.Equal(2, moveResult.RequestedCount);
            Assert.Equal(1, moveResult.SucceededCount);
            Assert.False(moveResult.IsComplete);
            SolutionFileOperationFailure failure = Assert.Single(moveResult.Failures);
            Assert.Equal(secondSource, failure.SourcePath, ignoreCase: true);
            Assert.False(File.Exists(firstSource));
            Assert.True(File.Exists(Path.Combine(targetDirectory, "first.txt")));
            Assert.True(File.Exists(secondSource));
            Assert.Equal("existing", File.ReadAllText(Path.Combine(targetDirectory, "second.txt")));

            SolutionFileOperationResult copyResult = SolutionClipboardFileOperations.Execute(
                [Path.Combine(targetDirectory, "first.txt")],
                targetDirectory,
                isMove: false);

            Assert.True(copyResult.IsComplete);
            Assert.Empty(copyResult.Failures);
            Assert.True(File.Exists(Path.Combine(targetDirectory, "first - Copy (1).txt")));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void PhysicalItemCreation_IsAtomicAndPreservesExistingFilesOnFailure()
    {
        string rootPath = CreateTemporaryDirectory();
        string filePath = Path.Combine(rootPath, "new_file.txt");
        var textTemplate = new TextFileTemplate();

        try
        {
            SolutionPhysicalItemResult created = SolutionPhysicalItemOperations.CreateFromTemplate(
                textTemplate,
                rootPath,
                "new_file.txt",
                overwrite: false);

            Assert.True(created.IsComplete);
            Assert.Equal(filePath, Assert.Single(created.NewlyCreatedPaths), ignoreCase: true);
            File.WriteAllText(filePath, "original");

            SolutionPhysicalItemResult rejected = SolutionPhysicalItemOperations.CreateFromTemplate(
                textTemplate,
                rootPath,
                "new_file.txt",
                overwrite: false);
            Assert.False(rejected.IsComplete);
            Assert.Equal("original", File.ReadAllText(filePath));

            SolutionPhysicalItemResult failedOverwrite = SolutionPhysicalItemOperations.CreateFromTemplate(
                new ThrowingNewItemTemplate(),
                rootPath,
                "new_file.txt",
                overwrite: true);
            Assert.False(failedOverwrite.IsComplete);
            Assert.Equal("original", File.ReadAllText(filePath));

            SolutionPhysicalItemResult overwritten = SolutionPhysicalItemOperations.CreateFromTemplate(
                textTemplate,
                rootPath,
                "new_file.txt",
                overwrite: true);
            Assert.True(overwritten.IsComplete);
            Assert.Empty(overwritten.NewlyCreatedPaths);
            Assert.Equal(string.Empty, File.ReadAllText(filePath));

            File.Delete(filePath);
            Directory.CreateDirectory(filePath);
            Assert.Equal(
                "new_file(2).txt",
                SolutionPhysicalItemOperations.GetAvailableFileName(textTemplate, rootPath));

            Assert.False(SolutionPhysicalItemOperations.TryGetAvailableFileName(
                new BrokenMetadataNewItemTemplate(),
                rootPath,
                out _,
                out string metadataError));
            Assert.Contains("读取新建项模板失败", metadataError, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void PhysicalItemImport_RequiresExplicitOverwriteAndRejectsProjectMetadata()
    {
        string rootPath = CreateTemporaryDirectory();
        string sourceDirectory = Path.Combine(rootPath, "Source");
        string targetDirectory = Path.Combine(rootPath, "Target");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(targetDirectory);
        string sourcePath = Path.Combine(sourceDirectory, "item.txt");
        string targetPath = Path.Combine(targetDirectory, "item.txt");
        string projectPath = Path.Combine(sourceDirectory, "Nested.cvproj");
        File.WriteAllText(sourcePath, "incoming");
        File.WriteAllText(targetPath, "existing");
        File.WriteAllText(projectPath, "{}");

        try
        {
            Assert.Equal(
                targetPath,
                Assert.Single(SolutionPhysicalItemOperations.GetImportConflictPaths(
                    [sourcePath],
                    targetDirectory)),
                ignoreCase: true);

            SolutionPhysicalItemResult rejected = SolutionPhysicalItemOperations.ImportFiles(
                [sourcePath],
                targetDirectory,
                overwrite: false);
            Assert.False(rejected.IsComplete);
            Assert.Equal("existing", File.ReadAllText(targetPath));

            SolutionPhysicalItemResult overwritten = SolutionPhysicalItemOperations.ImportFiles(
                [sourcePath],
                targetDirectory,
                overwrite: true);
            Assert.True(overwritten.IsComplete);
            Assert.Equal("incoming", File.ReadAllText(targetPath));

            SolutionPhysicalItemResult alreadyPresent = SolutionPhysicalItemOperations.ImportFiles(
                [targetPath],
                targetDirectory,
                overwrite: false);
            Assert.True(alreadyPresent.IsComplete);
            Assert.Empty(alreadyPresent.ChangedPaths);

            SolutionPhysicalItemResult rejectedProject = SolutionPhysicalItemOperations.ImportFiles(
                [projectPath],
                targetDirectory,
                overwrite: false);
            Assert.False(rejectedProject.IsComplete);
            Assert.False(File.Exists(Path.Combine(targetDirectory, "Nested.cvproj")));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
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
    public void SelectionService_ContextSelectionPreservesBatchAndReplacesOutsideNode()
    {
        var first = CreateNode("first");
        var second = CreateNode("second");
        var outside = CreateNode("outside");
        var service = new SolutionSelectionService();
        service.SelectMany([first, second], first);

        service.PreserveOrSelectForContext(second);

        Assert.Equal([first, second], service.SelectedNodes);
        Assert.Same(first, service.AnchorNode);

        service.PreserveOrSelectForContext(outside);

        Assert.Equal([outside], service.SelectedNodes);
        Assert.Same(outside, service.AnchorNode);
        Assert.False(first.IsMultiSelected);
        Assert.False(second.IsMultiSelected);
        Assert.True(outside.IsMultiSelected);
    }

    [Fact]
    public void SelectionService_SiblingScopeUsesCurrentHierarchyAndSkipsLoadingPlaceholder()
    {
        var firstRoot = CreateNode("first-root");
        var secondRoot = CreateNode("second-root");
        var parent = CreateNode("parent");
        var firstChild = CreateNode("first-child");
        var secondChild = CreateNode("second-child");
        parent.AddChild(firstChild);
        parent.AddChild(new LazyLoadingNode());
        parent.AddChild(secondChild);

        IReadOnlyList<SolutionNode> childScope =
            SolutionSelectionService.GetSiblingSelectionScope(
                secondChild,
                [firstRoot, secondRoot]);
        IReadOnlyList<SolutionNode> rootScope =
            SolutionSelectionService.GetSiblingSelectionScope(
                firstRoot,
                [firstRoot, secondRoot]);

        Assert.Equal([firstChild, secondChild], childScope);
        Assert.Equal([firstRoot, secondRoot], rootScope);
    }

    [Fact]
    public void SolutionMenuContributionsComposeAndHonorSelectionPolicies()
    {
        string prefix = $"tests.solution-menu.{Guid.NewGuid():N}";
        var singleContribution = new TestSolutionMenuContribution(
            $"{prefix}.single",
            "TestSingleMenu",
            SolutionMenuSelectionPolicy.SingleOnly);
        var multipleContribution = new TestSolutionMenuContribution(
            $"{prefix}.multiple",
            "TestMultipleMenu",
            SolutionMenuSelectionPolicy.MultipleOnly);
        var anyContribution = new TestSolutionMenuContribution(
            $"{prefix}.any",
            "TestAnyMenu",
            SolutionMenuSelectionPolicy.Any);
        var lowerPriorityDuplicate = new TestSolutionMenuContribution(
            $"{prefix}.duplicate",
            "TestAnyMenu",
            SolutionMenuSelectionPolicy.Any);

        try
        {
            SolutionMenuContributionRegistry.Register(singleContribution, priority: 30);
            SolutionMenuContributionRegistry.Register(multipleContribution, priority: 20);
            SolutionMenuContributionRegistry.Register(anyContribution, priority: 10);
            SolutionMenuContributionRegistry.Register(lowerPriorityDuplicate, priority: 5);
            var first = CreateNode("first");
            var second = CreateNode("second");

            IReadOnlyList<MenuItemMetadata> singleItems =
                SolutionMenuContributionRegistry.GetMenuItems(new SolutionMenuContext([first]));
            IReadOnlyList<MenuItemMetadata> multipleItems =
                SolutionMenuContributionRegistry.GetMenuItems(new SolutionMenuContext([first, second]));

            Assert.Contains(singleItems, item => item.GuidId == "TestSingleMenu");
            Assert.Contains(singleItems, item => item.GuidId == "TestAnyMenu");
            Assert.Single(singleItems, item => item.GuidId == "TestAnyMenu");
            Assert.Contains(singleItems, item => item.Header?.ToString() == anyContribution.Id);
            Assert.DoesNotContain(singleItems, item => item.GuidId == "TestMultipleMenu");
            Assert.Contains(multipleItems, item => item.GuidId == "TestMultipleMenu");
            Assert.Contains(multipleItems, item => item.GuidId == "TestAnyMenu");
            Assert.DoesNotContain(multipleItems, item => item.GuidId == "TestSingleMenu");
        }
        finally
        {
            SolutionMenuContributionRegistry.Unregister(singleContribution.Id);
            SolutionMenuContributionRegistry.Unregister(multipleContribution.Id);
            SolutionMenuContributionRegistry.Unregister(anyContribution.Id);
            SolutionMenuContributionRegistry.Unregister(lowerPriorityDuplicate.Id);
        }
    }

    [Fact]
    public void SolutionContextMenuAddsDynamicContributionsAtOpening()
    {
        var node = CreateNode("C:\\Workspace\\sample.txt");
        node.Initialize();
        var nodeItems = new List<MenuItemMetadata>();
        node.CollectMenuItems(nodeItems);

        List<MenuItemMetadata> openingItems = SolutionContextMenuService.CreateMenuMetadata([node]);

        Assert.DoesNotContain(nodeItems, item => item.GuidId == "CopyFullPath");
        Assert.DoesNotContain(nodeItems, item => item.GuidId == SolutionCommandIds.Delete);
        Assert.Single(openingItems, item => item.GuidId == "CopyFullPath");
        Assert.Single(openingItems, item => item.GuidId == SolutionCommandIds.Delete);
    }

    [Fact]
    public void ResourceNodeMenusComposeFixedActionsDynamicallyAndPreserveMetaExtensions()
    {
        string rootPath = CreateTemporaryDirectory();
        string filePath = Path.Combine(rootPath, "sample.txt");
        string folderPath = Path.Combine(rootPath, "Folder");
        string solutionPath = Path.Combine(rootPath, "Example.cvsln");
        File.WriteAllText(filePath, "sample");
        Directory.CreateDirectory(folderPath);
        SolutionConfigStore.Save(solutionPath, new SolutionConfig { RootPath = "." });

        try
        {
            using var explorer = CreateSolutionExplorer(rootPath, solutionPath);
            var fileNode = new FileNode(new TestFileMenuMeta(
                new FileInfo(filePath),
                "TestFileMetaAction"));
            using var folderNode = new FolderNode(new TestFolderMenuMeta(
                new DirectoryInfo(folderPath),
                "TestFolderMetaAction"));
            using var searchResult = new SolutionSearchResultNode(
                explorer,
                fileNode,
                "sample.txt",
                ownsTarget: false);
            var fileNodeItems = new List<MenuItemMetadata>();
            var folderNodeItems = new List<MenuItemMetadata>();
            fileNode.CollectMenuItems(fileNodeItems);
            folderNode.CollectMenuItems(folderNodeItems);

            List<MenuItemMetadata> fileMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([fileNode]);
            List<MenuItemMetadata> folderMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([folderNode]);
            List<MenuItemMetadata> searchMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([searchResult]);
            List<MenuItemMetadata> multiMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([fileNode, folderNode]);

            Assert.Contains(fileNodeItems, item => item.GuidId == "TestFileMetaAction");
            Assert.Contains(folderNodeItems, item => item.GuidId == "TestFolderMetaAction");
            Assert.DoesNotContain(fileNodeItems, item => item.GuidId is
                "AskCopilotExplainFile" or "AskCopilotDiagnoseFile" or "OpenContainingFolder");
            Assert.DoesNotContain(folderNodeItems, item => item.GuidId is
                "AskCopilotSummarizeFolder" or "Fusion" or "MenuOpenFileInExplorer" or "OpenInCmdCommad");

            Assert.Contains(fileMenuItems, item => item.GuidId == "TestFileMetaAction");
            Assert.Same(
                fileNode.AskCopilotExplainFileCommand,
                Assert.Single(fileMenuItems, item => item.GuidId == "AskCopilotExplainFile").Command);
            Assert.Same(
                fileNode.AskCopilotDiagnoseFileCommand,
                Assert.Single(fileMenuItems, item => item.GuidId == "AskCopilotDiagnoseFile").Command);
            Assert.Same(
                fileNode.OpenContainingFolderCommand,
                Assert.Single(fileMenuItems, item => item.GuidId == "OpenContainingFolder").Command);

            Assert.Contains(folderMenuItems, item => item.GuidId == "TestFolderMetaAction");
            Assert.Same(
                folderNode.AskCopilotSummarizeFolderCommand,
                Assert.Single(folderMenuItems, item => item.GuidId == "AskCopilotSummarizeFolder").Command);
            Assert.Same(
                folderNode.OpenFusionCommand,
                Assert.Single(folderMenuItems, item => item.GuidId == "Fusion").Command);
            Assert.Same(
                folderNode.OpenFileInExplorerCommand,
                Assert.Single(folderMenuItems, item => item.GuidId == "MenuOpenFileInExplorer").Command);
            Assert.Same(
                folderNode.OpenInCmdCommand,
                Assert.Single(folderMenuItems, item => item.GuidId == "OpenInCmdCommad").Command);

            Assert.Contains(searchMenuItems, item => item.GuidId == "TestFileMetaAction");
            Assert.Same(
                fileNode.OpenContainingFolderCommand,
                Assert.Single(searchMenuItems, item => item.GuidId == "OpenContainingFolder").Command);
            Assert.DoesNotContain(multiMenuItems, item => item.GuidId is
                "AskCopilotExplainFile" or "AskCopilotDiagnoseFile" or "OpenContainingFolder"
                or "AskCopilotSummarizeFolder" or "Fusion" or "MenuOpenFileInExplorer" or "OpenInCmdCommad");
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void SolutionContextMenuContributionsOverrideLegacyNodeItemsById()
    {
        string suffix = Guid.NewGuid().ToString("N");
        string menuId = $"TestOverride.{suffix}";
        var contribution = new TestSolutionMenuContribution(
            $"tests.override.{suffix}",
            menuId,
            SolutionMenuSelectionPolicy.SingleOnly);
        SolutionMenuContributionRegistry.Register(contribution, priority: 1000);

        try
        {
            var node = new LegacyMenuNode(menuId);

            List<MenuItemMetadata> menuItems =
                SolutionContextMenuService.CreateMenuMetadata([node]);

            MenuItemMetadata item = Assert.Single(menuItems, item => item.GuidId == menuId);
            Assert.Equal(contribution.Id, item.Header?.ToString());
        }
        finally
        {
            SolutionMenuContributionRegistry.Unregister(contribution.Id);
        }
    }

    [Fact]
    public void ProjectMenus_AreComposedFromCurrentProjectState()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(solutionDirectory, "App");
        string projectPath = Path.Combine(projectDirectory, "App.cvproj");
        string startupProjectDirectory = Path.Combine(solutionDirectory, "Launcher");
        string startupProjectPath = Path.Combine(startupProjectDirectory, "Launcher.cvproj");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        const string solutionFolderId = "logical";
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(startupProjectDirectory);
        FolderProjectProvider.CreateProjectFile(
            projectPath,
            "App",
            commands: new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [ProjectCapabilityIds.Run] = new("dotnet run", "."),
                ["publish"] = new("dotnet publish", "."),
            },
            excludedPaths: ["Output/**"]);
        FolderProjectProvider.CreateProjectFile(
            startupProjectPath,
            "Launcher",
            commands: new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [ProjectCapabilityIds.Run] = new("dotnet run", "."),
            });
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects =
            [
                Path.GetRelativePath(solutionDirectory, projectPath),
                Path.GetRelativePath(solutionDirectory, startupProjectPath),
            ],
            StartupProject = Path.GetRelativePath(solutionDirectory, startupProjectPath),
            SolutionFolders =
            [
                new SolutionFolderDefinition { Id = solutionFolderId, Name = "Logical" },
            ],
        });

        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            ProjectNode projectNode = Assert.Single(
                explorer.VisualChildren.OfType<ProjectNode>(),
                node => string.Equals(node.Project.Name, "App", StringComparison.Ordinal));
            var nodeItems = new List<MenuItemMetadata>();
            projectNode.CollectMenuItems(nodeItems);

            List<MenuItemMetadata> initialItems =
                SolutionContextMenuService.CreateMenuMetadata([projectNode]);

            Assert.DoesNotContain(nodeItems, item => item.GuidId is
                SolutionProjectCommands.EditProjectFileId
                or SolutionProjectCommands.ShowAllFilesId
                or SolutionProjectCommands.SetStartupProjectId
                or "ProjectCapability.run"
                or "ProjectCapability.publish"
                or "MoveProjectToSolutionFolder");
            Assert.Single(initialItems, item =>
                item.GuidId == SolutionProjectCommands.EditProjectFileId);
            Assert.False(Assert.Single(initialItems, item =>
                item.GuidId == SolutionProjectCommands.ShowAllFilesId).IsChecked);
            Assert.False(Assert.Single(initialItems, item =>
                item.GuidId == SolutionProjectCommands.SetStartupProjectId).IsChecked);
            Assert.Same(
                SolutionProjectCommands.Run,
                Assert.Single(initialItems, item => item.GuidId == "ProjectCapability.run").Command);
            Assert.IsType<ColorVision.Common.MVVM.RelayCommand>(Assert.Single(initialItems, item =>
                item.GuidId == "ProjectCapability.publish").Command);
            Assert.Single(initialItems, item => item.GuidId == "MoveProjectToSolutionFolder");
            Assert.Single(initialItems, item =>
                item.GuidId == $"MoveProjectToSolutionFolder.{solutionFolderId}");

            projectNode.ToggleShowAllFilesCommand.Execute(null);
            Assert.True(Assert.Single(
                SolutionContextMenuService.CreateMenuMetadata([projectNode]),
                item => item.GuidId == SolutionProjectCommands.ShowAllFilesId).IsChecked);

            Assert.True(explorer.SetStartupProject(projectNode.Project));
            Assert.True(Assert.Single(
                SolutionContextMenuService.CreateMenuMetadata([projectNode]),
                item => item.GuidId == SolutionProjectCommands.SetStartupProjectId).IsChecked);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionRootMenus_AreComposedFromCurrentConfiguration()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        ProjectDefinition project = CreateBuildProject(solutionDirectory, "App");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects = [Path.GetRelativePath(solutionDirectory, project.ProjectFile.FullName)],
            ActiveConfiguration = "Debug",
        });

        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            var nodeItems = new List<MenuItemMetadata>();
            explorer.CollectMenuItems(nodeItems);

            List<MenuItemMetadata> initialItems =
                SolutionContextMenuService.CreateMenuMetadata([explorer]);

            Assert.DoesNotContain(nodeItems, item => item.GuidId is
                SolutionProjectCommands.BuildSolutionId
                or SolutionProjectCommands.RunStartupProjectId
                or SolutionProjectCommands.DebugStartupProjectId
                or SolutionProjectCommands.ActiveConfigurationId
                or SolutionProjectCommands.ConfigurationManagerId
                or "Edit"
                or "MenuOpenFileInExplorer");
            Assert.Same(
                SolutionProjectCommands.BuildSolution,
                Assert.Single(initialItems, item =>
                    item.GuidId == SolutionProjectCommands.BuildSolutionId).Command);
            Assert.Same(
                SolutionProjectCommands.Run,
                Assert.Single(initialItems, item =>
                    item.GuidId == SolutionProjectCommands.RunStartupProjectId).Command);
            Assert.Same(
                SolutionProjectCommands.Debug,
                Assert.Single(initialItems, item =>
                    item.GuidId == SolutionProjectCommands.DebugStartupProjectId).Command);
            Assert.Equal(
                "活动配置: Debug",
                Assert.Single(initialItems, item =>
                    item.GuidId == SolutionProjectCommands.ActiveConfigurationId).Header);
            Assert.True(Assert.Single(initialItems, item =>
                item.GuidId == "SolutionConfiguration.Debug").IsChecked);
            MenuItemMetadata releaseItem = Assert.Single(initialItems, item =>
                item.GuidId == "SolutionConfiguration.Release");
            Assert.False(releaseItem.IsChecked);
            Assert.IsType<ColorVision.Common.MVVM.RelayCommand>(releaseItem.Command);
            Assert.Same(
                SolutionProjectCommands.ConfigurationManager,
                Assert.Single(initialItems, item =>
                    item.GuidId == SolutionProjectCommands.ConfigurationManagerId).Command);

            releaseItem.Command!.Execute(null);
            List<MenuItemMetadata> updatedItems =
                SolutionContextMenuService.CreateMenuMetadata([explorer]);

            Assert.Equal("Release", explorer.ActiveConfiguration);
            Assert.Equal(
                "活动配置: Release",
                Assert.Single(updatedItems, item =>
                    item.GuidId == SolutionProjectCommands.ActiveConfigurationId).Header);
            Assert.True(Assert.Single(updatedItems, item =>
                item.GuidId == "SolutionConfiguration.Release").IsChecked);
            Assert.False(Assert.Single(updatedItems, item =>
                item.GuidId == "SolutionConfiguration.Debug").IsChecked);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
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
        string solutionDirectory = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig { RootPath = "." });
        var parent = CreateNode("parent");
        var child = CreateNode("child");
        parent.AddChild(child);
        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            using var parentResult = new SolutionSearchResultNode(explorer, parent, "Project\\parent", ownsTarget: false);
            using var duplicateParentResult = new SolutionSearchResultNode(explorer, parent, "Duplicate\\parent", ownsTarget: false);
            using var childResult = new SolutionSearchResultNode(explorer, child, "Project\\parent\\child", ownsTarget: false);
            var service = new SolutionSelectionService();

            service.SelectMany([parentResult, duplicateParentResult, childResult]);

            Assert.Equal([parent, child], service.CommandNodes);
            Assert.Equal([parent], service.GetTopLevelNodes(_ => true));
            Assert.Equal([parentResult, duplicateParentResult, childResult], service.SelectedNodes);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SolutionTreeNavigation_LoadsLazyFoldersAndResolvesPhysicalFile()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string targetPath = Path.Combine(solutionDirectory, "Deep", "Nested", "target.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.WriteAllText(targetPath, "target");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.AutoDiscover,
        });

        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            FileNode detachedTarget = SolutionNodeFactory.CreateFileNode(new FileInfo(targetPath));

            SolutionNode? resolvedNode = await SolutionTreeNavigationService.ResolveNodeAsync(
                explorer,
                detachedTarget);

            FileNode resolvedFile = Assert.IsType<FileNode>(resolvedNode);
            Assert.NotSame(detachedTarget, resolvedFile);
            Assert.Equal(targetPath, resolvedFile.FullPath, ignoreCase: true);
            FolderNode nestedFolder = Assert.IsType<FolderNode>(resolvedFile.Parent);
            FolderNode deepFolder = Assert.IsType<FolderNode>(nestedFolder.Parent);
            Assert.True(deepFolder.AreChildrenLoaded);
            Assert.True(nestedFolder.AreChildrenLoaded);
            Assert.True(deepFolder.IsExpanded);
            Assert.True(nestedFolder.IsExpanded);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SolutionTreeNavigation_ResolvesFilesInExternalExplicitProjectOnly()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string externalDirectory = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(externalDirectory, "ExternalProject");
        string projectPath = Path.Combine(projectDirectory, "ExternalProject.cvproj");
        string targetPath = Path.Combine(projectDirectory, "Source", "target.txt");
        string unregisteredPath = Path.Combine(externalDirectory, "Outside", "target.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(unregisteredPath)!);
        File.WriteAllText(targetPath, "target");
        File.WriteAllText(unregisteredPath, "outside");
        FolderProjectProvider.CreateProjectFile(projectPath, "ExternalProject");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects = [Path.GetRelativePath(solutionDirectory, projectPath)],
        });

        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            ProjectNode projectNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            List<MenuItemMetadata> projectMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([projectNode]);
            Assert.Equal(projectPath, projectNode.EditorResourcePath, ignoreCase: true);
            Assert.Same(
                SolutionResourceCommands.OpenWith,
                Assert.Single(projectMenuItems, item =>
                    item.GuidId == SolutionResourceCommands.OpenWithId).Command);
            FileNode projectTarget = SolutionNodeFactory.CreateFileNode(new FileInfo(targetPath));
            projectTarget.Parent = projectNode;
            FileNode unregisteredTarget = SolutionNodeFactory.CreateFileNode(new FileInfo(unregisteredPath));

            SolutionNode? resolvedProjectNode = await SolutionTreeNavigationService.ResolveNodeAsync(
                explorer,
                projectTarget);
            SolutionNode? resolvedUnregisteredNode = await SolutionTreeNavigationService.ResolveNodeAsync(
                explorer,
                unregisteredTarget);

            Assert.IsType<FileNode>(resolvedProjectNode);
            Assert.Equal(targetPath, resolvedProjectNode.FullPath, ignoreCase: true);
            Assert.Null(resolvedUnregisteredNode);
            Assert.True(projectNode.AreChildrenLoaded);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
            Directory.Delete(externalDirectory, recursive: true);
        }
    }

    [Fact]
    public void ContextMenu_ComposesRoutedOpenCommandsForPhysicalResources()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string filePath = Path.Combine(solutionDirectory, "sample.txt");
        string secondFilePath = Path.Combine(solutionDirectory, "second.txt");
        File.WriteAllText(filePath, "sample");
        File.WriteAllText(secondFilePath, "second");
        string folderPath = Path.Combine(solutionDirectory, "Folder");
        Directory.CreateDirectory(folderPath);
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig { RootPath = "." });

        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            FileNode fileNode = SolutionNodeFactory.CreateFileNode(new FileInfo(filePath));
            FileNode secondFileNode = SolutionNodeFactory.CreateFileNode(new FileInfo(secondFilePath));
            using FolderNode folderNode = SolutionNodeFactory.CreateFolderNode(
                new DirectoryInfo(folderPath),
                explorer);
            using var searchResult = new SolutionSearchResultNode(
                explorer,
                fileNode,
                "sample.txt",
                ownsTarget: false);
            using var duplicateSearchResult = new SolutionSearchResultNode(
                explorer,
                fileNode,
                "duplicate/sample.txt",
                ownsTarget: false);
            using var folderSearchResult = new SolutionSearchResultNode(
                explorer,
                folderNode,
                "Folder",
                ownsTarget: false);
            using var duplicateFolderSearchResult = new SolutionSearchResultNode(
                explorer,
                folderNode,
                "duplicate/Folder",
                ownsTarget: false);
            var searchNodeItems = new List<MenuItemMetadata>();
            searchResult.CollectMenuItems(searchNodeItems);
            List<MenuItemMetadata> fileMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([fileNode]);
            List<MenuItemMetadata> folderMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([folderNode]);
            List<MenuItemMetadata> searchMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([searchResult]);
            List<MenuItemMetadata> solutionMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([explorer]);
            List<MenuItemMetadata> multiFileMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([fileNode, secondFileNode]);
            List<MenuItemMetadata> mixedMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([fileNode, folderNode]);
            List<MenuItemMetadata> duplicateSearchMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([searchResult, duplicateSearchResult]);
            List<MenuItemMetadata> duplicateFolderSearchMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([folderSearchResult, duplicateFolderSearchResult]);
            var duplicateSearchContext = new SolutionMenuContext([searchResult, duplicateSearchResult]);

            Assert.Same(
                SolutionResourceCommands.Open,
                Assert.Single(fileMenuItems, item => item.GuidId == SolutionResourceCommands.OpenId).Command);
            Assert.Same(
                SolutionResourceCommands.OpenWith,
                Assert.Single(fileMenuItems, item => item.GuidId == SolutionResourceCommands.OpenWithId).Command);
            Assert.Same(
                SolutionResourceCommands.Open,
                Assert.Single(folderMenuItems, item => item.GuidId == SolutionResourceCommands.OpenId).Command);
            Assert.Same(
                SolutionResourceCommands.OpenWith,
                Assert.Single(folderMenuItems, item => item.GuidId == SolutionResourceCommands.OpenWithId).Command);
            Assert.Same(
                SolutionResourceCommands.Open,
                Assert.Single(searchMenuItems, item => item.GuidId == SolutionResourceCommands.OpenId).Command);
            Assert.Same(
                SolutionResourceCommands.OpenWith,
                Assert.Single(searchMenuItems, item => item.GuidId == SolutionResourceCommands.OpenWithId).Command);
            Assert.Same(
                SolutionResourceCommands.Open,
                Assert.Single(solutionMenuItems, item => item.GuidId == SolutionResourceCommands.OpenId).Command);
            Assert.Same(
                SolutionResourceCommands.OpenWith,
                Assert.Single(solutionMenuItems, item => item.GuidId == SolutionResourceCommands.OpenWithId).Command);
            Assert.Same(
                SolutionResourceCommands.Open,
                Assert.Single(multiFileMenuItems, item => item.GuidId == SolutionResourceCommands.OpenId).Command);
            Assert.DoesNotContain(multiFileMenuItems, item =>
                item.GuidId == SolutionResourceCommands.OpenWithId);
            Assert.DoesNotContain(mixedMenuItems, item =>
                item.GuidId == SolutionResourceCommands.OpenId
                || item.GuidId == SolutionResourceCommands.OpenWithId);
            Assert.Same(
                ApplicationCommands.Delete,
                Assert.Single(fileMenuItems, item => item.GuidId == SolutionCommandIds.Delete).Command);
            Assert.Same(
                ApplicationCommands.Delete,
                Assert.Single(multiFileMenuItems, item => item.GuidId == SolutionCommandIds.Delete).Command);
            Assert.DoesNotContain(solutionMenuItems, item => item.GuidId == SolutionCommandIds.Delete);
            Assert.Same(
                Commands.ReName,
                Assert.Single(fileMenuItems, item => item.GuidId == SolutionCommandIds.Rename).Command);
            Assert.Same(
                ApplicationCommands.Properties,
                Assert.Single(fileMenuItems, item => item.GuidId == SolutionCommandIds.Properties).Command);
            Assert.DoesNotContain(fileMenuItems, item => item.GuidId == SolutionCommandIds.Refresh);
            Assert.Same(
                NavigationCommands.Refresh,
                Assert.Single(folderMenuItems, item => item.GuidId == SolutionCommandIds.Refresh).Command);
            Assert.Same(
                ApplicationCommands.Properties,
                Assert.Single(folderMenuItems, item => item.GuidId == SolutionCommandIds.Properties).Command);
            Assert.Same(
                NavigationCommands.Refresh,
                Assert.Single(solutionMenuItems, item => item.GuidId == SolutionCommandIds.Refresh).Command);
            Assert.Same(
                ApplicationCommands.Properties,
                Assert.Single(solutionMenuItems, item => item.GuidId == SolutionCommandIds.Properties).Command);
            Assert.DoesNotContain(solutionMenuItems, item => item.GuidId == SolutionCommandIds.Rename);
            Assert.DoesNotContain(searchNodeItems, item =>
                item.GuidId == SolutionNavigationCommands.RevealInTreeId);
            Assert.Same(
                SolutionNavigationCommands.RevealInTree,
                Assert.Single(searchMenuItems, item =>
                    item.GuidId == SolutionNavigationCommands.RevealInTreeId).Command);
            Assert.DoesNotContain(duplicateSearchMenuItems, item =>
                item.GuidId == SolutionNavigationCommands.RevealInTreeId);
            Assert.True(duplicateSearchContext.IsMultipleSelection);
            Assert.Equal(2, duplicateSearchContext.VisualNodes.Count);
            Assert.Single(duplicateSearchContext.Nodes);
            Assert.DoesNotContain(duplicateSearchMenuItems, item => item.GuidId is
                SolutionResourceCommands.OpenWithId or SolutionCommandIds.Rename or SolutionCommandIds.Properties
                or "AskCopilotExplainFile" or "AskCopilotDiagnoseFile" or "OpenContainingFolder");
            Assert.DoesNotContain(duplicateFolderSearchMenuItems, item => item.GuidId is
                SolutionResourceCommands.OpenWithId or SolutionCommandIds.Paste
                or "AskCopilotSummarizeFolder" or "Fusion" or "MenuOpenFileInExplorer" or "OpenInCmdCommad");
            Assert.Equal(filePath, fileNode.EditorResourcePath, ignoreCase: true);
            Assert.Equal(folderPath, folderNode.EditorResourcePath, ignoreCase: true);
            Assert.Equal(solutionPath, explorer.EditorResourcePath, ignoreCase: true);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionNode_DefaultsDoNotAdvertiseUnsupportedManagementCommands()
    {
        var node = new SolutionNode();

        List<MenuItemMetadata> menuItems = SolutionContextMenuService.CreateMenuMetadata([node]);

        Assert.False(node.CanRefresh);
        Assert.False(node.CanReName);
        Assert.False(node.CanShowProperties);
        Assert.False(node.CanCopy);
        Assert.False(node.CanCut);
        Assert.DoesNotContain(menuItems, item => item.GuidId == SolutionCommandIds.Refresh);
        Assert.DoesNotContain(menuItems, item => item.GuidId == SolutionCommandIds.Rename);
        Assert.DoesNotContain(menuItems, item => item.GuidId == SolutionCommandIds.Properties);
        Assert.DoesNotContain(menuItems, item => item.GuidId == SolutionCommandIds.Copy);
        Assert.DoesNotContain(menuItems, item => item.GuidId == SolutionCommandIds.Cut);
    }

    [Fact]
    public void ContainerCommands_AreContributedFromExplicitContainerCapabilities()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string folderPath = Path.Combine(solutionDirectory, "Folder");
        string filePath = Path.Combine(solutionDirectory, "sample.txt");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(filePath, "sample");
        ProjectDefinition project = CreateBuildProject(solutionDirectory, "Project");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig { RootPath = "." });

        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            using FolderNode folderNode = SolutionNodeFactory.CreateFolderNode(
                new DirectoryInfo(folderPath),
                explorer);
            using ProjectNode projectNode = SolutionNodeFactory.CreateProjectNode(project, explorer);
            FileNode fileNode = SolutionNodeFactory.CreateFileNode(new FileInfo(filePath));
            var solutionFolderNode = new SolutionFolderNode(
                explorer,
                new SolutionFolderDefinition { Name = "Logical" });

            List<MenuItemMetadata> rootMenu = SolutionContextMenuService.CreateMenuMetadata([explorer]);
            List<MenuItemMetadata> folderMenu = SolutionContextMenuService.CreateMenuMetadata([folderNode]);
            List<MenuItemMetadata> fileMenu = SolutionContextMenuService.CreateMenuMetadata([fileNode]);
            List<MenuItemMetadata> projectMenu = SolutionContextMenuService.CreateMenuMetadata([projectNode]);
            List<MenuItemMetadata> solutionFolderMenu =
                SolutionContextMenuService.CreateMenuMetadata([solutionFolderNode]);
            List<MenuItemMetadata> physicalMultiMenu =
                SolutionContextMenuService.CreateMenuMetadata([fileNode, folderNode]);

            Assert.True(explorer.CanAdd);
            Assert.True(folderNode.CanAdd);
            Assert.True(projectNode.CanAdd);
            Assert.True(solutionFolderNode.CanAdd);
            Assert.False(fileNode.CanAdd);
            Assert.False(new SolutionNode().CanAdd);

            Assert.True(explorer.CanPaste);
            Assert.True(folderNode.CanPaste);
            Assert.True(projectNode.CanPaste);
            Assert.False(fileNode.CanPaste);
            Assert.False(solutionFolderNode.CanPaste);
            Assert.False(new SolutionNode().CanPaste);
            Assert.Equal(filePath, fileNode.ClipboardResourcePath, ignoreCase: true);
            Assert.Equal(folderPath, folderNode.ClipboardResourcePath, ignoreCase: true);
            Assert.Equal(project.ProjectDirectory.FullName, projectNode.ClipboardResourcePath, ignoreCase: true);
            Assert.Null(explorer.ClipboardResourcePath);
            Assert.Null(solutionFolderNode.ClipboardResourcePath);
            Assert.Equal(
                solutionDirectory,
                Assert.IsAssignableFrom<ISolutionPhysicalContainer>(explorer).PhysicalContainerPath,
                ignoreCase: true);
            Assert.Equal(
                folderPath,
                Assert.IsAssignableFrom<ISolutionPhysicalContainer>(folderNode).PhysicalContainerPath,
                ignoreCase: true);
            Assert.Equal(
                project.ProjectDirectory.FullName,
                Assert.IsAssignableFrom<ISolutionPhysicalContainer>(projectNode).PhysicalContainerPath,
                ignoreCase: true);

            Assert.Same(
                ApplicationCommands.Paste,
                Assert.Single(rootMenu, item => item.GuidId == SolutionCommandIds.Paste).Command);
            Assert.Same(
                ApplicationCommands.Paste,
                Assert.Single(folderMenu, item => item.GuidId == SolutionCommandIds.Paste).Command);
            Assert.Same(
                ApplicationCommands.Paste,
                Assert.Single(projectMenu, item => item.GuidId == SolutionCommandIds.Paste).Command);
            Assert.DoesNotContain(fileMenu, item => item.GuidId == SolutionCommandIds.Paste);
            Assert.DoesNotContain(solutionFolderMenu, item => item.GuidId == SolutionCommandIds.Paste);
            Assert.Same(
                ApplicationCommands.Copy,
                Assert.Single(fileMenu, item => item.GuidId == SolutionCommandIds.Copy).Command);
            Assert.Same(
                ApplicationCommands.Cut,
                Assert.Single(fileMenu, item => item.GuidId == SolutionCommandIds.Cut).Command);
            Assert.Same(
                ApplicationCommands.Copy,
                Assert.Single(folderMenu, item => item.GuidId == SolutionCommandIds.Copy).Command);
            Assert.Same(
                ApplicationCommands.Cut,
                Assert.Single(folderMenu, item => item.GuidId == SolutionCommandIds.Cut).Command);
            Assert.DoesNotContain(rootMenu, item => item.GuidId == SolutionCommandIds.Copy);
            Assert.DoesNotContain(projectMenu, item => item.GuidId == SolutionCommandIds.Copy);
            Assert.DoesNotContain(solutionFolderMenu, item => item.GuidId == SolutionCommandIds.Copy);
            Assert.Same(
                ApplicationCommands.Copy,
                Assert.Single(physicalMultiMenu, item =>
                    item.GuidId == SolutionCommandIds.Copy).Command);
            Assert.Same(
                ApplicationCommands.Cut,
                Assert.Single(physicalMultiMenu, item =>
                    item.GuidId == SolutionCommandIds.Cut).Command);
            Assert.DoesNotContain(physicalMultiMenu, item =>
                item.GuidId == SolutionCommandIds.Paste);

            Assert.Same(
                SolutionContainerCommands.AddNewItem,
                Assert.Single(folderMenu, item =>
                    item.GuidId == SolutionContainerCommands.AddNewItemId).Command);
            Assert.Same(
                SolutionContainerCommands.AddExistingItem,
                Assert.Single(folderMenu, item =>
                    item.GuidId == SolutionContainerCommands.AddExistingItemId).Command);
            Assert.Same(
                SolutionContainerCommands.CreateFolder,
                Assert.Single(folderMenu, item =>
                    item.GuidId == SolutionContainerCommands.CreateFolderId).Command);
            Assert.DoesNotContain(folderMenu, item =>
                item.GuidId == SolutionContainerCommands.AddNewProjectId);
            Assert.Same(
                SolutionContainerCommands.AddNewItem,
                Assert.Single(projectMenu, item =>
                    item.GuidId == SolutionContainerCommands.AddNewItemId).Command);
            Assert.DoesNotContain(projectMenu, item =>
                item.GuidId == SolutionContainerCommands.AddNewProjectId);
            Assert.DoesNotContain(solutionFolderMenu, item =>
                item.GuidId == SolutionContainerCommands.CreateFolderId);
            Assert.Same(
                SolutionContainerCommands.CreateSolutionFolder,
                Assert.Single(solutionFolderMenu, item =>
                    item.GuidId == SolutionContainerCommands.CreateSolutionFolderId).Command);
            Assert.Same(
                SolutionContainerCommands.AddNewProject,
                Assert.Single(rootMenu, item =>
                    item.GuidId == SolutionContainerCommands.AddNewProjectId).Command);
            Assert.Same(
                SolutionContainerCommands.AddExistingProject,
                Assert.Single(rootMenu, item =>
                    item.GuidId == SolutionContainerCommands.AddExistingProjectId).Command);

            folderNode.CanAdd = false;
            folderNode.CanPaste = false;
            List<MenuItemMetadata> disabledFolderMenu =
                SolutionContextMenuService.CreateMenuMetadata([folderNode]);
            Assert.DoesNotContain(disabledFolderMenu, item =>
                item.GuidId == SolutionContainerCommands.AddMenuId);
            Assert.DoesNotContain(disabledFolderMenu, item =>
                item.GuidId == SolutionCommandIds.Paste);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public void SolutionCache_AddDirectoryTreeIndexesExistingDescendants()
    {
        string rootPath = CreateTemporaryDirectory();
        string importedDirectory = Path.Combine(rootPath, "Imported");
        string nestedFilePath = Path.Combine(importedDirectory, "Nested", "indexed-needle.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedFilePath)!);
        File.WriteAllText(nestedFilePath, "indexed");
        string solutionPath = Path.Combine(rootPath, "Example.cvsln");

        try
        {
            using var cache = new SolutionCache(solutionPath);

            cache.AddDirectoryTree(importedDirectory, rootPath);
            List<FileTreeCacheEntry> results = cache.Search(["indexed-needle"], rootPath: rootPath);

            Assert.Contains(results, entry =>
                !entry.IsDirectory
                && string.Equals(entry.FullPath, nestedFilePath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void SolutionCache_SearchPrunesStaleEntriesBeforeApplyingLimit()
    {
        string rootPath = CreateTemporaryDirectory();
        string staleDirectory = Path.Combine(rootPath, "aaa-needle-stale");
        string validFilePath = Path.Combine(rootPath, "zzz-needle-valid.txt");
        Directory.CreateDirectory(staleDirectory);
        File.WriteAllText(validFilePath, "valid");
        string solutionPath = Path.Combine(rootPath, "Example.cvsln");

        try
        {
            using var cache = new SolutionCache(solutionPath);
            cache.AddDirectory(staleDirectory, rootPath);
            cache.AddFile(validFilePath, rootPath);
            Directory.Delete(staleDirectory);

            FileTreeCacheEntry result = Assert.Single(cache.Search(
                ["needle"],
                maxResults: 1,
                rootPath: rootPath));

            Assert.Equal(validFilePath, result.FullPath, ignoreCase: true);
            Assert.DoesNotContain(cache.Search(["needle"], rootPath: rootPath), entry =>
                string.Equals(entry.FullPath, staleDirectory, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void DirectoryIndexBatchKeepsOnlyTopLevelPendingRoots()
    {
        string rootPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SolutionIndexRoot"));
        string nestedPath = Path.Combine(rootPath, "Nested");
        string deeplyNestedPath = Path.Combine(nestedPath, "Deep");
        string siblingPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SolutionIndexSibling"));
        var pendingIndexes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [deeplyNestedPath] = nestedPath,
            [rootPath] = Path.GetDirectoryName(rootPath)!,
            [nestedPath] = rootPath,
            [siblingPath] = Path.GetDirectoryName(siblingPath)!,
        };

        List<KeyValuePair<string, string>> roots = SolutionExplorer.ReduceDirectoryIndexRoots(pendingIndexes);

        Assert.Equal(2, roots.Count);
        Assert.Contains(roots, entry => string.Equals(entry.Key, rootPath, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(roots, entry => string.Equals(entry.Key, siblingPath, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExternalProjectChangesUpdateLoadedTreeIncrementally()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string externalDirectory = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(externalDirectory, "ExternalProject");
        string sourceDirectory = Path.Combine(projectDirectory, "Source");
        string projectPath = Path.Combine(projectDirectory, "ExternalProject.cvproj");
        Directory.CreateDirectory(sourceDirectory);
        FolderProjectProvider.CreateProjectFile(projectPath, "ExternalProject");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects = [Path.GetRelativePath(solutionDirectory, projectPath)],
        });

        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);
            ProjectNode projectNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            await projectNode.EnsureChildrenLoadedAsync();
            FolderNode sourceNode = Assert.Single(projectNode.VisualChildren.OfType<FolderNode>(), node =>
                string.Equals(node.FullPath, sourceDirectory, StringComparison.OrdinalIgnoreCase));
            await sourceNode.EnsureChildrenLoadedAsync();
            string createdPath = Path.Combine(sourceDirectory, "created.txt");
            string renamedPath = Path.Combine(sourceDirectory, "renamed.txt");

            File.WriteAllText(createdPath, "created");
            projectNode.ApplyExternalCreated(createdPath);
            Assert.Contains(sourceNode.VisualChildren, node =>
                string.Equals(node.FullPath, createdPath, StringComparison.OrdinalIgnoreCase));

            File.Move(createdPath, renamedPath);
            projectNode.ApplyExternalRenamed(createdPath, renamedPath);
            Assert.DoesNotContain(sourceNode.VisualChildren, node =>
                string.Equals(node.FullPath, createdPath, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(sourceNode.VisualChildren, node =>
                string.Equals(node.FullPath, renamedPath, StringComparison.OrdinalIgnoreCase));

            File.Delete(renamedPath);
            projectNode.ApplyExternalDeleted(renamedPath);
            Assert.DoesNotContain(sourceNode.VisualChildren, node =>
                string.Equals(node.FullPath, renamedPath, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
            Directory.Delete(externalDirectory, recursive: true);
        }
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
    public async Task SolutionSearch_PreservesBestMatchAcrossSolutions()
    {
        string crowdedSolutionDirectory = CreateTemporaryDirectory();
        string exactSolutionDirectory = CreateTemporaryDirectory();
        string crowdedSolutionPath = Path.Combine(crowdedSolutionDirectory, "Crowded.cvsln");
        string exactSolutionPath = Path.Combine(exactSolutionDirectory, "Exact.cvsln");
        for (int index = 0; index < 8; index++)
            File.WriteAllText(Path.Combine(crowdedSolutionDirectory, $"needle-{index:D2}.txt"), index.ToString());
        string exactPath = Path.Combine(exactSolutionDirectory, "needle");
        File.WriteAllText(exactPath, "exact");
        SolutionConfigStore.Save(crowdedSolutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.AutoDiscover,
        });
        SolutionConfigStore.Save(exactSolutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.AutoDiscover,
        });

        try
        {
            using var crowdedExplorer = CreateSolutionExplorer(crowdedSolutionDirectory, crowdedSolutionPath);
            using var exactExplorer = CreateSolutionExplorer(exactSolutionDirectory, exactSolutionPath);
            crowdedExplorer.Cache!.RebuildCache(crowdedSolutionDirectory);
            exactExplorer.Cache!.RebuildCache(exactSolutionDirectory);

            SolutionSearchResult result = await SolutionSearchService.SearchAsync(
                [crowdedExplorer, exactExplorer],
                "needle",
                maxResults: 1);

            SolutionSearchHit hit = Assert.Single(result.Hits);
            Assert.Equal(exactPath, hit.FullPath, ignoreCase: true);
            Assert.True(result.IsTruncated);
        }
        finally
        {
            Directory.Delete(crowdedSolutionDirectory, recursive: true);
            Directory.Delete(exactSolutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SolutionSearch_PreservesBestMatchAcrossExternalProjectRoots()
    {
        string solutionDirectory = CreateTemporaryDirectory();
        string externalContainer = CreateTemporaryDirectory();
        string crowdedProjectDirectory = Path.Combine(externalContainer, "CrowdedProject");
        string exactProjectDirectory = Path.Combine(externalContainer, "ExactProject");
        Directory.CreateDirectory(crowdedProjectDirectory);
        Directory.CreateDirectory(exactProjectDirectory);
        for (int index = 0; index < 8; index++)
            File.WriteAllText(Path.Combine(crowdedProjectDirectory, $"needle-{index:D2}.txt"), index.ToString());
        string exactPath = Path.Combine(exactProjectDirectory, "needle");
        File.WriteAllText(exactPath, "exact");
        string crowdedProjectPath = Path.Combine(crowdedProjectDirectory, "CrowdedProject.cvproj");
        string exactProjectPath = Path.Combine(exactProjectDirectory, "ExactProject.cvproj");
        FolderProjectProvider.CreateProjectFile(crowdedProjectPath, "CrowdedProject");
        FolderProjectProvider.CreateProjectFile(exactProjectPath, "ExactProject");
        string solutionPath = Path.Combine(solutionDirectory, "Example.cvsln");
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects =
            [
                Path.GetRelativePath(solutionDirectory, crowdedProjectPath),
                Path.GetRelativePath(solutionDirectory, exactProjectPath),
            ],
        });

        try
        {
            using var explorer = CreateSolutionExplorer(solutionDirectory, solutionPath);

            SolutionSearchResult result = await SolutionSearchService.SearchAsync(
                [explorer],
                "needle",
                maxResults: 1);

            SolutionSearchHit hit = Assert.Single(result.Hits);
            Assert.Equal(exactPath, hit.FullPath, ignoreCase: true);
            Assert.True(result.IsTruncated);
        }
        finally
        {
            Directory.Delete(solutionDirectory, recursive: true);
            Directory.Delete(externalContainer, recursive: true);
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
            List<MenuItemMetadata> includedMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([includedSource]);
            Assert.Contains(includedMenuItems, item =>
                item.GuidId == SolutionProjectCommands.ExcludeFromProjectId
                && item.Command == SolutionProjectCommands.ExcludeFromProject);

            Assert.False(projectNode.ShowAllFiles);
            projectNode.ToggleShowAllFilesCommand.Execute(null);
            Assert.True(projectNode.ShowAllFiles);
            projectNode.VisualChildren.Clear();
            await SolutionNodeFactory.PopulateChildren(projectNode, projectNode.DirectoryInfo);
            SolutionNode excludedOutput = Assert.Single(projectNode.VisualChildren, child => child.Name == "Output");
            Assert.True(excludedOutput.IsExcludedFromProject);
            List<MenuItemMetadata> excludedMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([excludedOutput]);
            Assert.Contains(excludedMenuItems, item =>
                item.GuidId == SolutionProjectCommands.IncludeInProjectId
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
    public void OpenSolutionWindow_ShowsOnlySupportedWorkspaceHistoryEntries()
    {
        string directoryPath = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(directoryPath, "Example.cvsln");
        string legacyFolderWorkspacePath = Path.Combine(directoryPath, SolutionManager.FolderWorkspaceFileName);
        string ordinaryFilePath = Path.Combine(directoryPath, "notes.txt");
        try
        {
            File.WriteAllText(solutionPath, "{}");
            File.WriteAllText(legacyFolderWorkspacePath, "{}");
            File.WriteAllText(ordinaryFilePath, "text");

            Assert.True(OpenSolutionWindow.TryCreateSolutionInfo(solutionPath, out SolutionInfo solutionInfo));
            Assert.Equal(solutionPath, solutionInfo.FullName);
            Assert.True(OpenSolutionWindow.TryCreateSolutionInfo(
                legacyFolderWorkspacePath,
                out SolutionInfo folderInfo));
            Assert.Equal(directoryPath, folderInfo.FullName);
            Assert.False(OpenSolutionWindow.TryCreateSolutionInfo(ordinaryFilePath, out _));
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
    public async Task RegisteringProjectProvider_ReplacesUnavailableProjectInOpenSolution()
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
            var nodeMenuItems = new List<MenuItemMetadata>();
            unavailableNode.CollectMenuItems(nodeMenuItems);
            List<MenuItemMetadata> unavailableMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([unavailableNode]);
            using var searchResult = new SolutionSearchResultNode(
                explorer,
                unavailableNode,
                unavailableNode.Name,
                ownsTarget: false);
            List<MenuItemMetadata> searchMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([searchResult]);
            var secondUnavailableNode = new UnavailableProjectNode(
                explorer,
                "Other.lateproj",
                Path.Combine(solutionDirectory, "Other.lateproj"));
            List<MenuItemMetadata> multiMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([unavailableNode, secondUnavailableNode]);

            Assert.DoesNotContain(nodeMenuItems, item => item.GuidId is
                "ShowUnavailableProjectError" or "OpenUnavailableProjectContainer");
            Assert.Contains(unavailableMenuItems, item => item.GuidId == SolutionCommandIds.Refresh);
            Assert.Same(
                unavailableNode.ShowLoadErrorCommand,
                Assert.Single(unavailableMenuItems, item => item.GuidId == "ShowUnavailableProjectError").Command);
            Assert.Same(
                unavailableNode.OpenContainingFolderCommand,
                Assert.Single(unavailableMenuItems, item => item.GuidId == "OpenUnavailableProjectContainer").Command);
            Assert.Contains(searchMenuItems, item => item.GuidId == SolutionNavigationCommands.RevealInTreeId);
            Assert.Same(
                unavailableNode.ShowLoadErrorCommand,
                Assert.Single(searchMenuItems, item => item.GuidId == "ShowUnavailableProjectError").Command);
            Assert.DoesNotContain(multiMenuItems, item => item.GuidId is
                "ShowUnavailableProjectError" or "OpenUnavailableProjectContainer");
            Assert.Contains(unavailableMenuItems, item =>
                item.GuidId == SolutionCommandIds.Delete
                && ReferenceEquals(item.Command, ApplicationCommands.Delete)
                && string.Equals(item.Header?.ToString(), "从解决方案中移除(_V)", StringComparison.Ordinal));

            ProjectProviderRegistry.Register(new LateProjectProvider(), priority: 1000);
            ProjectRefreshResult refreshResult = await explorer.RefreshExplicitProjectStateAsync();
            Assert.True(refreshResult.Succeeded, refreshResult.ErrorMessage);

            ProjectNode projectNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            Assert.Equal(LateProjectProvider.ProviderId, projectNode.Project.ProviderId);
            Assert.Empty(explorer.VisualChildren.OfType<UnavailableProjectNode>());
            Assert.Equal(ResourceOpenKind.Project, ResourceOpenService.Classify(projectPath));
            List<MenuItemMetadata> projectMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([projectNode]);
            Assert.Contains(projectMenuItems, item =>
                item.GuidId == SolutionCommandIds.Delete
                && string.Equals(item.Header?.ToString(), "从解决方案中移除(_V)", StringComparison.Ordinal));
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
            SolutionFolderNode folderNode = Assert.Single(explorer.VisualChildren.OfType<SolutionFolderNode>());
            Assert.Equal(folder.Id, folderNode.FolderId);
            List<MenuItemMetadata> folderMenuItems =
                SolutionContextMenuService.CreateMenuMetadata([folderNode]);
            Assert.Same(
                Commands.ReName,
                Assert.Single(folderMenuItems, item => item.GuidId == SolutionCommandIds.Rename).Command);
            Assert.DoesNotContain(folderMenuItems, item => item.GuidId == SolutionCommandIds.Properties);
            Assert.DoesNotContain(folderMenuItems, item => item.GuidId == SolutionCommandIds.Refresh);
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
    public void SolutionBatchDelete_UsesOneRecycleOperationForPhysicalNodes()
    {
        string rootPath = CreateTemporaryDirectory();
        string firstFilePath = Path.Combine(rootPath, "first.txt");
        string secondFilePath = Path.Combine(rootPath, "second.txt");
        string folderPath = Path.Combine(rootPath, "Folder");
        File.WriteAllText(firstFilePath, "first");
        File.WriteAllText(secondFilePath, "second");
        Directory.CreateDirectory(folderPath);

        try
        {
            var parent = new SolutionNode();
            FileNode firstFile = SolutionNodeFactory.CreateFileNode(new FileInfo(firstFilePath));
            FileNode secondFile = SolutionNodeFactory.CreateFileNode(new FileInfo(secondFilePath));
            FolderNode folder = SolutionNodeFactory.CreateFolderNode(new DirectoryInfo(folderPath));
            parent.AddChild(firstFile);
            parent.AddChild(secondFile);
            parent.AddChild(folder);
            int recycleCalls = 0;
            string[] recycledPaths = [];

            IReadOnlyList<SolutionNode> failures = SolutionBatchDeleteService.Delete(
                [firstFile, firstFile, secondFile, folder],
                paths =>
                {
                    recycleCalls++;
                    recycledPaths = paths;
                    return 0;
                });

            Assert.Empty(failures);
            Assert.Equal(1, recycleCalls);
            Assert.Empty(parent.VisualChildren);
            Assert.Equal(
                [firstFilePath, secondFilePath, folderPath],
                recycledPaths,
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void SolutionBatchDelete_PreservesNodesWhenRecycleOperationFails()
    {
        string rootPath = CreateTemporaryDirectory();
        string filePath = Path.Combine(rootPath, "kept.txt");
        File.WriteAllText(filePath, "kept");

        try
        {
            var parent = new SolutionNode();
            FileNode file = SolutionNodeFactory.CreateFileNode(new FileInfo(filePath));
            parent.AddChild(file);

            IReadOnlyList<SolutionNode> failures = SolutionBatchDeleteService.Delete(
                [file],
                _ => 5);

            Assert.Equal([file], failures);
            Assert.Equal([file], parent.VisualChildren);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void SolutionBatchDelete_RejectsNodesWithoutDeleteCapability()
    {
        string rootPath = CreateTemporaryDirectory();
        string filePath = Path.Combine(rootPath, "protected.txt");
        File.WriteAllText(filePath, "protected");

        try
        {
            var parent = new SolutionNode();
            FileNode file = SolutionNodeFactory.CreateFileNode(new FileInfo(filePath));
            file.CanDelete = false;
            parent.AddChild(file);
            int recycleCalls = 0;

            IReadOnlyList<SolutionNode> failures = SolutionBatchDeleteService.Delete(
                [file],
                _ =>
                {
                    recycleCalls++;
                    return 0;
                });

            Assert.Equal([file], failures);
            Assert.Equal(0, recycleCalls);
            Assert.Equal([file], parent.VisualChildren);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void MultiDelete_RecordsHistoryInEachOwningSolution()
    {
        string firstDirectory = CreateTemporaryDirectory();
        string secondDirectory = CreateTemporaryDirectory();
        string firstSolutionPath = Path.Combine(firstDirectory, "First.cvsln");
        string secondSolutionPath = Path.Combine(secondDirectory, "Second.cvsln");
        SolutionConfigStore.Save(firstSolutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            SolutionFolders = [new SolutionFolderDefinition { Id = "first", Name = "First" }],
        });
        SolutionConfigStore.Save(secondSolutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            SolutionFolders = [new SolutionFolderDefinition { Id = "second", Name = "Second" }],
        });

        try
        {
            using var firstExplorer = CreateSolutionExplorer(firstDirectory, firstSolutionPath);
            using var secondExplorer = CreateSolutionExplorer(secondDirectory, secondSolutionPath);
            SolutionFolderNode firstFolder = Assert.Single(firstExplorer.VisualChildren.OfType<SolutionFolderNode>());
            SolutionFolderNode secondFolder = Assert.Single(secondExplorer.VisualChildren.OfType<SolutionFolderNode>());

            IReadOnlyList<SolutionNode> failures = TreeViewControl.DeleteNodesByOwningSolution(
                [firstFolder, secondFolder]);

            Assert.Empty(failures);
            Assert.Empty(firstExplorer.Config.SolutionFolders);
            Assert.Empty(secondExplorer.Config.SolutionFolders);
            Assert.True(firstExplorer.CanUndoSolutionOperation);
            Assert.True(secondExplorer.CanUndoSolutionOperation);
        }
        finally
        {
            Directory.Delete(firstDirectory, recursive: true);
            Directory.Delete(secondDirectory, recursive: true);
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
    public async Task FolderNode_RefreshRejectsStaleChildrenAndUsesLatestSnapshot()
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

            rootNode.Refresh();
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
            var nodeItems = new List<MenuItemMetadata>();
            appsNode.CollectMenuItems(nodeItems);
            Assert.DoesNotContain(nodeItems, item => item.GuidId == "MoveSolutionFolder");
            List<MenuItemMetadata> menuItems =
                SolutionContextMenuService.CreateMenuMetadata([appsNode]);
            Assert.Contains(menuItems, item => item.GuidId == $"MoveSolutionFolder.{toolsFolderId}");
            Assert.DoesNotContain(menuItems, item => item.GuidId == $"MoveSolutionFolder.{testsFolderId}");
            Assert.Contains(menuItems, item =>
                item.GuidId == SolutionCommandIds.Delete
                && string.Equals(item.Header?.ToString(), "移除解决方案文件夹(_V)", StringComparison.Ordinal));

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
            Assert.True(Assert.Single(
                SolutionContextMenuService.CreateMenuMetadata([appsNode]),
                item => item.GuidId == $"MoveSolutionFolder.{toolsFolderId}").IsChecked);
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
            var nodeItems = new List<MenuItemMetadata>();
            solutionItemNode.CollectMenuItems(nodeItems);
            Assert.DoesNotContain(nodeItems, item => item.GuidId == "MoveSolutionItem");
            List<MenuItemMetadata> menuItems =
                SolutionContextMenuService.CreateMenuMetadata([solutionItemNode]);
            Assert.Contains(menuItems, item => item.GuidId == "MoveSolutionItem.Root");
            Assert.True(solutionItemNode.CanCopy);
            Assert.False(solutionItemNode.CanCut);
            Assert.False(solutionItemNode.CanPaste);
            Assert.False(solutionItemNode.CanReName);
            Assert.Same(
                ApplicationCommands.Copy,
                Assert.Single(menuItems, item => item.GuidId == SolutionCommandIds.Copy).Command);
            Assert.DoesNotContain(menuItems, item => item.GuidId is
                SolutionCommandIds.Cut or SolutionCommandIds.Paste or SolutionCommandIds.Rename);
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
            Assert.True(Assert.Single(
                SolutionContextMenuService.CreateMenuMetadata([solutionItemNode]),
                item => item.GuidId == "MoveSolutionItem.Root").IsChecked);

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
    public void MsBuildProjectProvider_LoadsSdkMetadataReferencesAndCommands()
    {
        string directoryPath = CreateTemporaryDirectory();
        string projectPath = Path.Combine(directoryPath, "Sample.csproj");
        try
        {
            File.WriteAllText(projectPath, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <AssemblyName>Sample.Application</AssemblyName>
                    <Version>2.3.4</Version>
                    <Configurations>Debug;Release;Staging</Configurations>
                  </PropertyGroup>
                  <ItemGroup>
                    <ProjectReference Include="..\Library\Library.csproj" />
                  </ItemGroup>
                </Project>
                """);

            var provider = new MsBuildProjectProvider();
            ProjectDefinition project = provider.Load(new FileInfo(projectPath));
            ProjectDefinition releaseProject = project.ForConfiguration("release");

            Assert.True(provider.CanLoad(new FileInfo(projectPath)));
            Assert.Equal(MsBuildProjectProvider.ProviderId, project.ProviderId);
            Assert.Equal("Sample.Application", project.Name);
            Assert.Equal("2.3.4", project.Version);
            Assert.Equal(["..\\Library\\Library.csproj"], project.Dependencies);
            Assert.Equal(["Debug", "Release", "Staging"], project.Configurations!.Keys);
            Assert.False(project.ItemRules!.Includes(project.ProjectDirectory, Path.Combine(directoryPath, "bin")));
            Assert.Equal(
                [ProjectCapabilityIds.Build, ProjectCapabilityIds.Run],
                provider.GetCapabilities(releaseProject).Select(capability => capability.Id));
            Assert.True(provider.TryCreateInvocation(
                releaseProject,
                ProjectCapabilityIds.Build,
                out ProjectCommandInvocation? buildInvocation));
            Assert.Equal(
                $"dotnet build \"{projectPath}\" --configuration \"Release\"",
                buildInvocation?.Command);
            Assert.True(provider.TryCreateInvocation(
                releaseProject,
                ProjectCapabilityIds.Run,
                out ProjectCommandInvocation? runInvocation));
            Assert.Equal(
                $"dotnet run --project \"{projectPath}\" --configuration \"Release\"",
                runInvocation?.Command);
            Assert.False(provider.CanExecuteCapability(releaseProject, ProjectCapabilityIds.Debug));

            Assert.Contains("*.csproj", ProjectProviderRegistry.GetProjectFilePatterns(), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("*.fsproj", ProjectProviderRegistry.GetProjectFilePatterns(), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("*.vbproj", ProjectProviderRegistry.GetProjectFilePatterns(), StringComparer.OrdinalIgnoreCase);
            Assert.True(ProjectProviderRegistry.TryLoadProject(new FileInfo(projectPath), out ProjectDefinition? registeredProject));
            Assert.Equal(MsBuildProjectProvider.ProviderId, registeredProject?.ProviderId);
            Assert.Equal(ResourceOpenKind.Project, ResourceOpenService.Classify(projectPath));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void MsBuildProjectProvider_UsesMsBuildForLegacyProjectsAndReportsMalformedXml()
    {
        string directoryPath = CreateTemporaryDirectory();
        string legacyProjectPath = Path.Combine(directoryPath, "Legacy.vbproj");
        string malformedProjectPath = Path.Combine(directoryPath, "Broken.fsproj");
        try
        {
            File.WriteAllText(legacyProjectPath, """
                <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup>
                    <AssemblyName>Legacy.Library</AssemblyName>
                    <OutputType>Library</OutputType>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(malformedProjectPath, "<Project>");

            var provider = new MsBuildProjectProvider();
            ProjectDefinition legacyProject = provider.Load(new FileInfo(legacyProjectPath)).ForConfiguration(null);
            ProjectDefinition malformedProject = provider.Load(new FileInfo(malformedProjectPath));

            Assert.Equal("Legacy.Library", legacyProject.Name);
            Assert.Equal([ProjectCapabilityIds.Build], provider.GetCapabilities(legacyProject).Select(capability => capability.Id));
            Assert.True(provider.TryCreateInvocation(
                legacyProject,
                ProjectCapabilityIds.Build,
                out ProjectCommandInvocation? invocation));
            Assert.Equal(
                $"msbuild \"{legacyProjectPath}\" /t:Build /p:Configuration=\"Debug\"",
                invocation?.Command);
            Assert.NotEmpty(malformedProject.LoadError!);
            Assert.Empty(provider.GetCapabilities(malformedProject));
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void VisualStudioSolutionFileProvider_LoadsSlnStructureAndConfigurationMappings()
    {
        string directoryPath = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(directoryPath, "src", "App");
        string projectPath = Path.Combine(projectDirectory, "App.csproj");
        string readmePath = Path.Combine(directoryPath, "README.md");
        string solutionPath = Path.Combine(directoryPath, "Example.sln");
        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(readmePath, "readme");
            File.WriteAllText(solutionPath, """
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                VisualStudioVersion = 17.0.31903.59
                MinimumVisualStudioVersion = 10.0.40219.1
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "src\App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "src", "src", "{22222222-2222-2222-2222-222222222222}"
                    ProjectSection(SolutionItems) = preProject
                        README.md = README.md
                    EndProjectSection
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                        {11111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU
                        {11111111-1111-1111-1111-111111111111}.Release|Any CPU.ActiveCfg = Release|Any CPU
                        {11111111-1111-1111-1111-111111111111}.Release|Any CPU.Build.0 = Release|Any CPU
                    EndGlobalSection
                    GlobalSection(NestedProjects) = preSolution
                        {11111111-1111-1111-1111-111111111111} = {22222222-2222-2222-2222-222222222222}
                    EndGlobalSection
                EndGlobal
                """);

            var provider = new VisualStudioSolutionFileProvider();
            SolutionFileDefinition definition = provider.Load(new FileInfo(solutionPath));
            SolutionFileProject project = Assert.Single(definition.Projects);
            SolutionFileFolder folder = Assert.Single(definition.Folders);

            Assert.Equal(VisualStudioSolutionFileProvider.ProviderId, definition.ProviderId);
            Assert.Equal("Example", definition.Name);
            Assert.Equal(["Debug", "Release"], definition.Configurations);
            Assert.Equal("src\\App\\App.csproj", project.Path);
            Assert.Equal(folder.Path, project.SolutionFolderPath);
            Assert.Equal("Release", project.Configurations["Release"]);
            Assert.Equal("src", folder.Name);
            Assert.Contains("README.md", folder.Files, StringComparer.OrdinalIgnoreCase);
            Assert.True(SolutionFileProviderRegistry.TryLoadSolution(
                new FileInfo(solutionPath),
                out SolutionFileDefinition? registeredDefinition,
                out string errorMessage),
                errorMessage);
            Assert.Equal(VisualStudioSolutionFileProvider.ProviderId, registeredDefinition?.ProviderId);
            Assert.Equal(ResourceOpenKind.Solution, ResourceOpenService.Classify(solutionPath));
            Assert.Contains("*.sln", SolutionFileProviderRegistry.GetSolutionFilePatterns(), StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ImportedSlnxSolution_UsesPrivateWorkspaceAndPreservesSourceFile()
    {
        string directoryPath = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(directoryPath, "src", "App");
        string projectPath = Path.Combine(projectDirectory, "App.csproj");
        string readmePath = Path.Combine(directoryPath, "README.md");
        string solutionPath = Path.Combine(directoryPath, "Example.slnx");
        string solutionContents = """
            <Solution>
              <Folder Name="/Solution Items/">
                <File Path="README.md" />
              </Folder>
              <Folder Name="/src/">
                <Project Path="src\App\App.csproj" />
              </Folder>
            </Solution>
            """;
        string? importedWorkspacePath = null;
        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(readmePath, "readme");
            File.WriteAllText(solutionPath, solutionContents);

            Assert.True(SolutionManager.TryCreateImportedSolution(
                new FileInfo(solutionPath),
                out importedWorkspacePath,
                out string displayName));

            Assert.Equal("Example", displayName);
            Assert.True(File.Exists(importedWorkspacePath));
            Assert.False(importedWorkspacePath.StartsWith(
                Path.TrimEndingDirectorySeparator(directoryPath) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
            Assert.Equal(solutionContents, File.ReadAllText(solutionPath));

            SolutionConfig config = SolutionConfigStore.Load(importedWorkspacePath).Config;
            string projectReference = Assert.Single(config.Projects);
            Assert.Equal(Path.Combine("src", "App", "App.csproj"), projectReference, ignoreCase: true);
            Assert.Equal(SolutionProjectMode.Explicit, config.ProjectMode);
            Assert.Equal(directoryPath, config.RootPath, ignoreCase: true);
            SolutionFolderDefinition sourceFolder = Assert.Single(config.SolutionFolders, folder => folder.Name == "src");
            Assert.Equal(sourceFolder.Id, config.ProjectSolutionFolders[projectReference]);
            SolutionItemDefinition solutionItem = Assert.Single(config.SolutionItems);
            Assert.Equal("README.md", solutionItem.Path, ignoreCase: true);
            Assert.Equal(
                VisualStudioSolutionFileProvider.ProviderId,
                config.ExtensionData!["ImportedSolutionProvider"]!.Value<string>());
            Assert.Equal(solutionPath, config.ExtensionData["ImportedSolutionSource"]!.Value<string>(), ignoreCase: true);
            Assert.Contains("*.slnx", SolutionManager.GetSolutionFileDialogPattern(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(importedWorkspacePath))
            {
                File.Delete(importedWorkspacePath);
                File.Delete($"{importedWorkspacePath}.bak");
            }
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task ImportedSolutionRefresh_ReimportsSourceAndPreservesWorkspacePreferences()
    {
        string directoryPath = CreateTemporaryDirectory();
        string appDirectory = Path.Combine(directoryPath, "src", "App");
        string libraryDirectory = Path.Combine(directoryPath, "src", "Library");
        string appProjectPath = Path.Combine(appDirectory, "App.csproj");
        string libraryProjectPath = Path.Combine(libraryDirectory, "Library.csproj");
        string solutionPath = Path.Combine(directoryPath, "Example.slnx");
        string initialContents = """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src\App\App.csproj" />
              </Folder>
            </Solution>
            """;
        string updatedContents = """
            <Solution>
              <Folder Name="/docs/">
                <File Path="README.md" />
              </Folder>
              <Folder Name="/src/">
                <Project Path="src\App\App.csproj" />
                <Project Path="src\Library\Library.csproj" />
              </Folder>
            </Solution>
            """;
        string? importedWorkspacePath = null;
        try
        {
            Directory.CreateDirectory(appDirectory);
            Directory.CreateDirectory(libraryDirectory);
            File.WriteAllText(appProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(libraryProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(Path.Combine(directoryPath, "README.md"), "readme");
            File.WriteAllText(solutionPath, initialContents);

            Assert.True(SolutionManager.TryCreateImportedSolution(
                new FileInfo(solutionPath),
                out importedWorkspacePath,
                out _));
            SolutionConfig config = SolutionConfigStore.Load(importedWorkspacePath).Config;
            string appReference = Assert.Single(config.Projects);
            config.StartupProject = appReference;
            config.ActiveConfiguration = "Release";
            SolutionConfigStore.Save(importedWorkspacePath, config);

            using var explorer = CreateSolutionExplorer(directoryPath, importedWorkspacePath);
            Assert.True(explorer.IsImportedSolution);
            Assert.True(explorer.IsImportedSolutionSourcePath(solutionPath));
            Assert.False(explorer.IsImportedSolutionSourcePath(appProjectPath));

            File.WriteAllText(solutionPath, "<Solution><Folder>");
            ImportedSolutionWorkspaceResult malformedResult = await explorer.RefreshImportedSolutionStateAsync();
            Assert.False(malformedResult.Succeeded);
            Assert.Contains(
                VisualStudioSolutionFileProvider.ProviderId,
                malformedResult.ErrorMessage,
                StringComparison.OrdinalIgnoreCase);
            Assert.Single(explorer.Config.Projects);

            File.WriteAllText(solutionPath, updatedContents);
            ImportedSolutionWorkspaceResult refreshResult = await explorer.RefreshImportedSolutionStateAsync();
            Assert.True(refreshResult.Succeeded, refreshResult.ErrorMessage);

            Assert.Equal(2, explorer.Config.Projects.Count);
            Assert.Equal(appReference, explorer.Config.StartupProject, ignoreCase: true);
            Assert.Equal("Release", explorer.Config.ActiveConfiguration);
            Assert.Equal(2, explorer.VisualChildren.GetAllVisualChildren().OfType<ProjectNode>().Count());
            Assert.Contains(explorer.Config.SolutionFolders, folder => folder.Name == "docs");
            Assert.Single(explorer.Config.SolutionItems);
            Assert.Equal(updatedContents, File.ReadAllText(solutionPath));
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(importedWorkspacePath))
            {
                File.Delete(importedWorkspacePath);
                File.Delete($"{importedWorkspacePath}.bak");
                File.Delete($"{importedWorkspacePath}.cache.db");
            }
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task SolutionFileProviderAsyncLoad_UsesAsyncContractAndPropagatesCancellation()
    {
        string directoryPath = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(directoryPath, "Cancel.asyncsln");
        File.WriteAllText(solutionPath, string.Empty);
        var provider = new ControlledAsyncSolutionProvider(Path.GetFileName(solutionPath));
        SolutionFileProviderRegistry.Register(provider, priority: 1000);
        using var cancellation = new CancellationTokenSource();
        try
        {
            Task<SolutionFileLoadResult> loadTask = SolutionFileProviderRegistry.LoadSolutionAsync(
                new FileInfo(solutionPath),
                cancellation.Token);
            await provider.WaitUntilStartedAsync(solutionPath);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loadTask);
            Assert.Equal(0, provider.SyncLoadCount);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task SolutionFileProviderAsyncLoad_CancelsLegacySynchronousProviderPromptly()
    {
        string directoryPath = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(directoryPath, "Legacy.legacysln");
        File.WriteAllText(solutionPath, string.Empty);
        var provider = new BlockingLegacySolutionProvider();
        SolutionFileProviderRegistry.Register(provider, priority: 1000);
        using var cancellation = new CancellationTokenSource();
        try
        {
            Task<SolutionFileLoadResult> loadTask = SolutionFileProviderRegistry.LoadSolutionAsync(
                new FileInfo(solutionPath),
                cancellation.Token);
            await provider.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loadTask);
            Assert.Equal(1, provider.SyncLoadCount);
        }
        finally
        {
            provider.Release();
            await provider.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task ProjectProviderAsyncLoad_UsesAsyncContractAndPropagatesCancellation()
    {
        string directoryPath = CreateTemporaryDirectory();
        string extension = $".cancelproj{Guid.NewGuid():N}";
        string projectPath = Path.Combine(directoryPath, $"Cancel{extension}");
        File.WriteAllText(projectPath, string.Empty);
        var provider = new ControlledAsyncProjectProvider(extension, "Cancel");
        ProjectProviderRegistry.Register(provider, priority: 1000);
        Task loadStarted = provider.BlockNextLoad(ignoreCancellation: false);
        using var cancellation = new CancellationTokenSource();
        try
        {
            Task<ProjectLoadResult> loadTask = ProjectProviderRegistry.LoadProjectAsync(
                new FileInfo(projectPath),
                cancellation.Token);
            await loadStarted.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loadTask);
            await provider.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, provider.SyncLoadCount);
        }
        finally
        {
            await provider.ReleaseBlockedLoadAsync();
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task ExplicitProjectRefreshAsync_NewerRequestCancelsOlderAndAppliesLatest()
    {
        string directoryPath = CreateTemporaryDirectory();
        string extension = $".refreshproj{Guid.NewGuid():N}";
        string projectPath = Path.Combine(directoryPath, $"App{extension}");
        string solutionPath = Path.Combine(directoryPath, "Example.cvsln");
        File.WriteAllText(projectPath, string.Empty);
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects = [Path.GetFileName(projectPath)],
        });
        var provider = new ControlledAsyncProjectProvider(extension, "Initial");
        ProjectProviderRegistry.Register(provider, priority: 1000);
        var manager = new SolutionManager(restoreLastWorkspace: false);
        try
        {
            SolutionOpenOperationResult openResult = await manager.OpenSolutionAsync(solutionPath);
            Assert.True(openResult.Succeeded, openResult.ErrorMessage);
            SolutionExplorer explorer = Assert.IsType<SolutionExplorer>(manager.CurrentSolutionExplorer);
            ProjectNode originalNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            Assert.Equal("Initial", originalNode.Project.Name);

            provider.ProjectName = "Stale";
            Task staleLoadStarted = provider.BlockNextLoad(ignoreCancellation: true);
            Task<ProjectRefreshResult> staleRefresh = explorer.RefreshExplicitProjectStateAsync();
            await staleLoadStarted.WaitAsync(TimeSpan.FromSeconds(10));

            provider.ProjectName = "Latest";
            Task<ProjectRefreshResult> latestRefresh = explorer.RefreshExplicitProjectStateAsync();
            ProjectRefreshResult latestResult = await latestRefresh;
            ProjectRefreshResult staleResult = await staleRefresh;

            Assert.True(latestResult.Succeeded, latestResult.ErrorMessage);
            Assert.True(staleResult.Canceled);
            ProjectNode refreshedNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            Assert.Same(originalNode, refreshedNode);
            Assert.Equal("Latest", refreshedNode.Project.Name);
            Assert.Equal(0, provider.SyncLoadCount);
        }
        finally
        {
            await provider.ReleaseBlockedLoadAsync();
            foreach (SolutionExplorer explorer in manager.SolutionExplorers.ToList())
                explorer.Dispose();
            manager.SolutionHistory.RemoveFile(solutionPath);
            File.Delete(SolutionConfigStore.GetBackupPath(solutionPath));
            File.Delete($"{solutionPath}.cache.db");
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task ExplicitProjectMutationAndCommands_UseLoadedDefinitionsWithoutReloadingProvider()
    {
        string directoryPath = CreateTemporaryDirectory();
        string extension = $".loadedproj{Guid.NewGuid():N}";
        string projectPath = Path.Combine(directoryPath, $"App{extension}");
        string solutionPath = Path.Combine(directoryPath, "Loaded.cvsln");
        File.WriteAllText(projectPath, string.Empty);
        SolutionConfigStore.Save(solutionPath, new SolutionConfig
        {
            RootPath = ".",
            ProjectMode = SolutionProjectMode.Explicit,
            Projects = [Path.GetFileName(projectPath)],
        });
        var provider = new ControlledAsyncProjectProvider(extension, "Initial");
        ProjectProviderRegistry.Register(provider, priority: 1000);
        var manager = new SolutionManager(restoreLastWorkspace: false);
        try
        {
            SolutionOpenOperationResult openResult = await manager.OpenSolutionAsync(solutionPath);
            Assert.True(openResult.Succeeded, openResult.ErrorMessage);
            SolutionExplorer explorer = Assert.IsType<SolutionExplorer>(manager.CurrentSolutionExplorer);
            ProjectNode originalNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            int asyncLoadCount = provider.AsyncLoadCount;

            ProjectDefinition updatedProject = originalNode.Project with { Name = "Updated" };
            explorer.ApplyProjectMutation(updatedProject);
            IReadOnlyList<ProjectDefinition> configurationProjects =
                explorer.LoadProjectsForConfigurationEditing();
            ProjectBuildPlan buildPlan = explorer.CreateBuildPlan(updatedProject);

            ProjectNode updatedNode = Assert.Single(explorer.VisualChildren.OfType<ProjectNode>());
            Assert.Same(originalNode, updatedNode);
            Assert.Equal("Updated", updatedNode.Project.Name);
            Assert.Equal("Updated", Assert.Single(configurationProjects).Name);
            Assert.Contains(buildPlan.OrderedProjects, project => project.Name == "Updated");
            Assert.Equal(asyncLoadCount, provider.AsyncLoadCount);
            Assert.Equal(0, provider.SyncLoadCount);
        }
        finally
        {
            foreach (SolutionExplorer explorer in manager.SolutionExplorers.ToList())
                explorer.Dispose();
            manager.SolutionHistory.RemoveFile(solutionPath);
            File.Delete(SolutionConfigStore.GetBackupPath(solutionPath));
            File.Delete($"{solutionPath}.cache.db");
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task ImportedSolutionRefresh_CancelsActiveProjectRefreshAndAppliesOneSnapshot()
    {
        string directoryPath = CreateTemporaryDirectory();
        string extension = $".crossrefresh{Guid.NewGuid():N}";
        string projectPath = Path.Combine(directoryPath, $"App{extension}");
        string solutionPath = Path.Combine(directoryPath, "Cross.asyncsln");
        File.WriteAllText(projectPath, string.Empty);
        File.WriteAllText(solutionPath, string.Empty);
        var solutionProvider = new ControlledAsyncSolutionProvider(
            "NeverBlocked.asyncsln",
            Path.GetFileName(projectPath));
        var projectProvider = new ControlledAsyncProjectProvider(extension, "Initial");
        SolutionFileProviderRegistry.Register(solutionProvider, priority: 1000);
        ProjectProviderRegistry.Register(projectProvider, priority: 1000);
        var manager = new SolutionManager(restoreLastWorkspace: false);
        string? workspacePath = null;
        try
        {
            SolutionOpenOperationResult openResult = await manager.OpenSolutionAsync(solutionPath);
            Assert.True(openResult.Succeeded, openResult.ErrorMessage);
            SolutionExplorer explorer = Assert.IsType<SolutionExplorer>(manager.CurrentSolutionExplorer);
            Assert.True(explorer.IsImportedSolution);
            workspacePath = explorer.ConfigFileInfo.FullName;

            projectProvider.ProjectName = "Stale";
            Task staleLoadStarted = projectProvider.BlockNextLoad(ignoreCancellation: true);
            Task<ProjectRefreshResult> staleRefresh = explorer.RefreshExplicitProjectStateAsync();
            await staleLoadStarted.WaitAsync(TimeSpan.FromSeconds(10));

            projectProvider.ProjectName = "ImportedLatest";
            Task<ImportedSolutionWorkspaceResult> importedRefresh =
                explorer.RefreshImportedSolutionStateAsync();
            ImportedSolutionWorkspaceResult importedResult = await importedRefresh;
            ProjectRefreshResult staleResult = await staleRefresh;

            Assert.True(importedResult.Succeeded, importedResult.ErrorMessage);
            Assert.True(staleResult.Canceled);
            Assert.Equal(
                "ImportedLatest",
                Assert.Single(explorer.VisualChildren.GetAllVisualChildren().OfType<ProjectNode>()).Project.Name);
            Assert.Equal(0, projectProvider.SyncLoadCount);
        }
        finally
        {
            await projectProvider.ReleaseBlockedLoadAsync();
            foreach (SolutionExplorer explorer in manager.SolutionExplorers.ToList())
                explorer.Dispose();
            manager.SolutionHistory.RemoveFile(solutionPath);
            if (!string.IsNullOrWhiteSpace(workspacePath))
            {
                File.Delete(workspacePath);
                File.Delete(SolutionConfigStore.GetBackupPath(workspacePath));
                File.Delete($"{workspacePath}.cache.db");
            }
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task WorkspaceOpenAsync_NewerRequestCancelsOlderWithoutReplacingItLater()
    {
        string directoryPath = CreateTemporaryDirectory();
        string firstSolutionPath = Path.Combine(directoryPath, "First.asyncsln");
        string secondSolutionPath = Path.Combine(directoryPath, "Second.asyncsln");
        string projectPath = Path.Combine(directoryPath, "App.asyncproj");
        File.WriteAllText(firstSolutionPath, string.Empty);
        File.WriteAllText(secondSolutionPath, string.Empty);
        File.WriteAllText(projectPath, string.Empty);
        var provider = new ControlledAsyncSolutionProvider(
            Path.GetFileName(firstSolutionPath),
            Path.GetFileName(projectPath));
        var projectProvider = new CountingProjectProvider();
        SolutionFileProviderRegistry.Register(provider, priority: 1000);
        ProjectProviderRegistry.Register(projectProvider, priority: 1000);
        var manager = new SolutionManager(restoreLastWorkspace: false);
        string? workspacePath = null;
        try
        {
            Task<SolutionOpenOperationResult> firstOpen = manager.OpenSolutionAsync(firstSolutionPath);
            await provider.WaitUntilStartedAsync(firstSolutionPath);
            Assert.True(manager.IsOpeningWorkspace);
            Assert.Contains("First.asyncsln", manager.WorkspaceOpenStatus, StringComparison.OrdinalIgnoreCase);

            Task<SolutionOpenOperationResult> secondOpen = manager.OpenSolutionAsync(secondSolutionPath);
            SolutionOpenOperationResult secondResult = await secondOpen;
            SolutionOpenOperationResult firstResult = await firstOpen;

            Assert.True(secondResult.Succeeded, secondResult.ErrorMessage);
            Assert.True(firstResult.Canceled);
            Assert.False(manager.IsOpeningWorkspace);
            Assert.Equal(string.Empty, manager.OpeningWorkspacePath);
            SolutionExplorer explorer = Assert.IsType<SolutionExplorer>(manager.CurrentSolutionExplorer);
            Assert.Equal(secondSolutionPath, explorer.ImportedSolutionSourcePath, ignoreCase: true);
            Assert.Single(explorer.VisualChildren);
            Assert.Single(explorer.VisualChildren.GetAllVisualChildren().OfType<ProjectNode>());
            Assert.Equal(1, projectProvider.LoadCount);
            workspacePath = explorer.ConfigFileInfo.FullName;
            Assert.Equal(0, provider.SyncLoadCount);

            SolutionOpenOperationResult failedResult = await manager.OpenSolutionAsync(
                Path.Combine(directoryPath, "Missing.asyncsln"));
            Assert.False(failedResult.Succeeded);
            Assert.Same(explorer, manager.CurrentSolutionExplorer);
            Assert.False(manager.IsOpeningWorkspace);
        }
        finally
        {
            foreach (SolutionExplorer explorer in manager.SolutionExplorers.ToList())
                explorer.Dispose();
            manager.SolutionHistory.RemoveFile(firstSolutionPath);
            manager.SolutionHistory.RemoveFile(secondSolutionPath);
            if (!string.IsNullOrWhiteSpace(workspacePath))
            {
                File.Delete(workspacePath);
                File.Delete($"{workspacePath}.bak");
                File.Delete($"{workspacePath}.cache.db");
            }
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public async Task WorkspaceLifecycle_CanceledTransitionKeepsCurrentAndCloseClearsState()
    {
        string containerPath = CreateTemporaryDirectory();
        string firstRoot = Path.Combine(containerPath, "First");
        string secondRoot = Path.Combine(containerPath, "Second");
        string externalRoot = Path.Combine(containerPath, "External");
        string firstSolutionPath = Path.Combine(firstRoot, "First.cvsln");
        string secondSolutionPath = Path.Combine(secondRoot, "Second.cvsln");
        string projectPath = Path.Combine(externalRoot, "App.mockproj");
        string solutionItemPath = Path.Combine(externalRoot, "Notes.txt");
        Directory.CreateDirectory(firstRoot);
        Directory.CreateDirectory(secondRoot);
        Directory.CreateDirectory(externalRoot);
        File.WriteAllText(projectPath, string.Empty);
        File.WriteAllText(solutionItemPath, "notes");

        var firstConfig = new SolutionConfig
        {
            RootPath = firstRoot,
            ProjectMode = SolutionProjectMode.Explicit,
        };
        firstConfig.Projects.Add(Path.GetRelativePath(firstRoot, projectPath));
        firstConfig.SolutionItems.Add(new SolutionItemDefinition
        {
            Path = Path.GetRelativePath(firstRoot, solutionItemPath),
        });
        SolutionConfigStore.Save(firstSolutionPath, firstConfig);
        SolutionConfigStore.Save(secondSolutionPath, new SolutionConfig
        {
            RootPath = secondRoot,
            ProjectMode = SolutionProjectMode.Explicit,
        });
        ProjectProviderRegistry.Register(new MockProjectProvider(), priority: 1000);

        bool allowClose = false;
        int closeAttempts = 0;
        IReadOnlyList<string> documentRoots = Array.Empty<string>();
        var manager = new SolutionManager(
            restoreLastWorkspace: false,
            tryCloseWorkspaceDocuments: explorer =>
            {
                closeAttempts++;
                documentRoots = explorer.GetDocumentResourceRoots();
                return allowClose;
            });
        object? closedSender = null;
        SolutionWorkspaceEventArgs? closedArgs = null;
        int closedCount = 0;
        manager.SolutionClosed += (sender, args) =>
        {
            closedSender = sender;
            closedArgs = args;
            closedCount++;
        };

        try
        {
            SolutionOpenOperationResult firstResult = await manager.OpenSolutionAsync(firstSolutionPath);
            Assert.True(firstResult.Succeeded, firstResult.ErrorMessage);
            SolutionExplorer firstExplorer = Assert.IsType<SolutionExplorer>(manager.CurrentSolutionExplorer);
            Assert.Equal(firstSolutionPath, manager.CurrentWorkspacePath, ignoreCase: true);
            Assert.True(manager.CanCloseSolution);

            SolutionOpenOperationResult sameWorkspaceResult = await manager.OpenSolutionAsync(firstSolutionPath);
            Assert.True(sameWorkspaceResult.Succeeded, sameWorkspaceResult.ErrorMessage);
            Assert.Same(firstExplorer, manager.CurrentSolutionExplorer);
            Assert.Equal(0, closeAttempts);

            SolutionOpenOperationResult secondResult = await manager.OpenSolutionAsync(secondSolutionPath);
            Assert.False(secondResult.Succeeded);
            Assert.True(secondResult.Canceled);
            Assert.Same(firstExplorer, manager.CurrentSolutionExplorer);
            Assert.Equal(firstSolutionPath, manager.CurrentWorkspacePath, ignoreCase: true);
            Assert.Equal(1, closeAttempts);
            Assert.Contains(firstRoot, documentRoots, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(firstSolutionPath, documentRoots, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(externalRoot, documentRoots, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(projectPath, documentRoots, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(solutionItemPath, documentRoots, StringComparer.OrdinalIgnoreCase);

            Assert.False(manager.TryCloseSolution());
            Assert.Same(firstExplorer, manager.CurrentSolutionExplorer);
            Assert.Equal(2, closeAttempts);
            Assert.Equal(0, closedCount);

            allowClose = true;
            Assert.True(manager.TryCloseSolution());
            Assert.Null(manager.CurrentSolutionExplorer);
            Assert.Empty(manager.SolutionExplorers);
            Assert.Equal(string.Empty, manager.CurrentWorkspacePath);
            Assert.False(manager.CanCloseSolution);
            Assert.Equal(3, closeAttempts);
            Assert.Equal(1, closedCount);
            Assert.Same(manager, closedSender);
            Assert.Equal(firstSolutionPath, closedArgs?.WorkspacePath, ignoreCase: true);
            Assert.True(File.Exists(firstSolutionPath));

            Assert.True(manager.TryCloseSolution());
            Assert.Equal(1, closedCount);
        }
        finally
        {
            foreach (SolutionExplorer explorer in manager.SolutionExplorers.ToList())
                explorer.Dispose();
            manager.SolutionHistory.RemoveFile(firstSolutionPath);
            manager.SolutionHistory.RemoveFile(secondSolutionPath);
            foreach (string solutionPath in new[] { firstSolutionPath, secondSolutionPath })
            {
                File.Delete(SolutionConfigStore.GetBackupPath(solutionPath));
                File.Delete($"{solutionPath}.cache.db");
            }
            Directory.Delete(containerPath, recursive: true);
        }
    }

    [Fact]
    public void ImportedSolutionRefresh_ThreeWayMergesProjectConfigurationOverrides()
    {
        string directoryPath = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(directoryPath, "src", "App");
        string projectPath = Path.Combine(projectDirectory, "App.csproj");
        string solutionPath = Path.Combine(directoryPath, "Example.sln");
        string? importedWorkspacePath = null;
        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(solutionPath, CreateSolutionContents("Debug", "Release", includeStaging: false));

            Assert.True(SolutionManager.TryCreateImportedSolution(
                new FileInfo(solutionPath),
                out importedWorkspacePath,
                out _));
            SolutionConfig config = SolutionConfigStore.Load(importedWorkspacePath).Config;
            string projectReference = Assert.Single(config.Projects);
            config.ProjectConfigurations[projectReference]["Release"] = "LocalRelease";
            config.ActiveConfiguration = "Release";
            SolutionConfigStore.Save(importedWorkspacePath, config);

            File.WriteAllText(solutionPath, CreateSolutionContents("Debug2", "Release2", includeStaging: true));
            Assert.True(ImportedSolutionWorkspaceService.TryRefresh(
                new FileInfo(importedWorkspacePath),
                config,
                out SolutionConfig? firstRefresh,
                out string firstError),
                firstError);
            Assert.NotNull(firstRefresh);
            Dictionary<string, string> firstMappings = firstRefresh.ProjectConfigurations[projectReference];
            Assert.Equal("Debug2", firstMappings["Debug"]);
            Assert.Equal("LocalRelease", firstMappings["Release"]);
            Assert.Equal("Staging", firstMappings["Staging"]);
            Assert.Equal("Release", firstRefresh.ActiveConfiguration);
            JObject firstBaseline = Assert.IsType<JObject>(
                firstRefresh.ExtensionData![ImportedSolutionWorkspaceService.ConfigurationBaselineExtensionKey]);
            Assert.Equal("Debug2", firstBaseline[projectReference]!["Debug"]!.Value<string>());
            Assert.Equal("Release2", firstBaseline[projectReference]!["Release"]!.Value<string>());

            File.WriteAllText(solutionPath, CreateSolutionContents("Debug3", "Release3", includeStaging: false));
            Assert.True(ImportedSolutionWorkspaceService.TryRefresh(
                new FileInfo(importedWorkspacePath),
                firstRefresh,
                out SolutionConfig? secondRefresh,
                out string secondError),
                secondError);
            Assert.NotNull(secondRefresh);
            Dictionary<string, string> secondMappings = secondRefresh.ProjectConfigurations[projectReference];
            Assert.Equal("Debug3", secondMappings["Debug"]);
            Assert.Equal("LocalRelease", secondMappings["Release"]);
            Assert.DoesNotContain("Staging", secondMappings.Keys, StringComparer.OrdinalIgnoreCase);

            secondMappings["Release"] = "Release3";
            SolutionConfigStore.Save(importedWorkspacePath, secondRefresh);
            File.WriteAllText(solutionPath, CreateSolutionContents("Debug4", "Release4", includeStaging: false));
            Assert.True(ImportedSolutionWorkspaceService.TryRefresh(
                new FileInfo(importedWorkspacePath),
                secondRefresh,
                out SolutionConfig? thirdRefresh,
                out string thirdError),
                thirdError);
            Assert.NotNull(thirdRefresh);
            Assert.Equal("Debug4", thirdRefresh.ProjectConfigurations[projectReference]["Debug"]);
            Assert.Equal("Release4", thirdRefresh.ProjectConfigurations[projectReference]["Release"]);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(importedWorkspacePath))
            {
                File.Delete(importedWorkspacePath);
                File.Delete($"{importedWorkspacePath}.bak");
            }
            Directory.Delete(directoryPath, recursive: true);
        }

        static string CreateSolutionContents(
            string debugMapping,
            string releaseMapping,
            bool includeStaging)
        {
            string template = """
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio Version 17
                VisualStudioVersion = 17.0.31903.59
                MinimumVisualStudioVersion = 10.0.40219.1
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "src\App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Global
                    GlobalSection(SolutionConfigurationPlatforms) = preSolution
                        Debug|Any CPU = Debug|Any CPU
                        Release|Any CPU = Release|Any CPU
                $SOLUTION_STAGING$
                    EndGlobalSection
                    GlobalSection(ProjectConfigurationPlatforms) = postSolution
                        {11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = $DEBUG$|Any CPU
                        {11111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = $DEBUG$|Any CPU
                        {11111111-1111-1111-1111-111111111111}.Release|Any CPU.ActiveCfg = $RELEASE$|Any CPU
                        {11111111-1111-1111-1111-111111111111}.Release|Any CPU.Build.0 = $RELEASE$|Any CPU
                $PROJECT_STAGING$
                    EndGlobalSection
                EndGlobal
                """;
            string solutionStaging = includeStaging
                ? "        Staging|Any CPU = Staging|Any CPU"
                : string.Empty;
            string projectStaging = includeStaging
                ? string.Join(Environment.NewLine,
                    "        {11111111-1111-1111-1111-111111111111}.Staging|Any CPU.ActiveCfg = Staging|Any CPU",
                    "        {11111111-1111-1111-1111-111111111111}.Staging|Any CPU.Build.0 = Staging|Any CPU")
                : string.Empty;
            return template
                .Replace("$DEBUG$", debugMapping, StringComparison.Ordinal)
                .Replace("$RELEASE$", releaseMapping, StringComparison.Ordinal)
                .Replace("$SOLUTION_STAGING$", solutionStaging, StringComparison.Ordinal)
                .Replace("$PROJECT_STAGING$", projectStaging, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MalformedVisualStudioSolution_ProvidesActionableOpenError()
    {
        string directoryPath = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(directoryPath, "Broken.slnx");
        try
        {
            File.WriteAllText(solutionPath, "<Solution><Folder>");

            Assert.False(SolutionManager.TryResolveOpenTarget(
                solutionPath,
                out string workspacePath,
                out string historyPath,
                out string displayName,
                out string errorMessage));

            Assert.Empty(workspacePath);
            Assert.Empty(historyPath);
            Assert.Empty(displayName);
            Assert.Contains(VisualStudioSolutionFileProvider.ProviderId, errorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("加载失败", errorMessage, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ImportedSolution_DisablesSourceControlledStructureMutations()
    {
        string directoryPath = CreateTemporaryDirectory();
        string projectDirectory = Path.Combine(directoryPath, "src", "App");
        string projectPath = Path.Combine(projectDirectory, "App.csproj");
        string readmePath = Path.Combine(directoryPath, "README.md");
        string solutionPath = Path.Combine(directoryPath, "Example.slnx");
        string? importedWorkspacePath = null;
        try
        {
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            File.WriteAllText(readmePath, "readme");
            File.WriteAllText(solutionPath, """
                <Solution>
                  <Folder Name="/Solution Items/">
                    <File Path="README.md" />
                  </Folder>
                  <Folder Name="/src/">
                    <Project Path="src\App\App.csproj" />
                  </Folder>
                </Solution>
                """);

            Assert.True(SolutionManager.TryCreateImportedSolution(
                new FileInfo(solutionPath),
                out importedWorkspacePath,
                out _));
            using var explorer = CreateSolutionExplorer(directoryPath, importedWorkspacePath);
            SolutionFolderNode sourceFolder = Assert.Single(
                explorer.VisualChildren.GetAllVisualChildren().OfType<SolutionFolderNode>(),
                folder => folder.Name == "src");
            ProjectNode projectNode = Assert.Single(
                explorer.VisualChildren.GetAllVisualChildren().OfType<ProjectNode>());
            SolutionItemNode solutionItemNode = Assert.Single(
                explorer.VisualChildren.GetAllVisualChildren().OfType<SolutionItemNode>());

            Assert.True(explorer.IsImportedSolution);
            Assert.False(explorer.CanModifySolutionStructure);
            Assert.Equal(solutionPath, explorer.ImportedSolutionSourcePath, ignoreCase: true);
            Assert.Equal(solutionPath, explorer.EditorResourcePath, ignoreCase: true);
            Assert.False(string.Equals(
                importedWorkspacePath,
                explorer.EditorResourcePath,
                StringComparison.OrdinalIgnoreCase));
            Assert.Equal(SolutionContainerAction.None, explorer.SupportedContainerActions);
            Assert.Equal(SolutionContainerAction.None, sourceFolder.SupportedContainerActions);
            Assert.False(sourceFolder.CanReName);
            Assert.False(sourceFolder.CanDelete);
            Assert.False(projectNode.CanDelete);
            Assert.False(solutionItemNode.CanDelete);

            List<MenuItemMetadata> rootMenu = SolutionContextMenuService.CreateMenuMetadata([explorer]);
            Assert.DoesNotContain(rootMenu, item => item.GuidId == SolutionContainerCommands.AddMenuId);
            Assert.DoesNotContain(rootMenu, item => item.GuidId == "Edit");
            Assert.Contains(rootMenu, item => item.GuidId == SolutionResourceCommands.OpenWithId);
            Assert.Contains(rootMenu, item => item.GuidId == SolutionResourceCommands.ImportedSourceMenuId);
            Assert.Contains(rootMenu, item => item.GuidId == SolutionResourceCommands.EditImportedSourceId
                && ReferenceEquals(item.Command, explorer.OpenImportedSolutionSourceCommand));
            Assert.Contains(rootMenu, item => item.GuidId == SolutionResourceCommands.RevealImportedSourceId
                && ReferenceEquals(item.Command, explorer.RevealImportedSolutionSourceCommand));
            Assert.Contains(rootMenu, item => item.GuidId == SolutionResourceCommands.CopyImportedSourcePathId
                && ReferenceEquals(item.Command, explorer.CopyImportedSolutionSourcePathCommand));
            Assert.True(explorer.OpenImportedSolutionSourceCommand.CanExecute(null));
            Assert.True(explorer.RevealImportedSolutionSourceCommand.CanExecute(null));
            Assert.True(explorer.CopyImportedSolutionSourcePathCommand.CanExecute(null));
            List<MenuItemMetadata> projectMenu = SolutionContextMenuService.CreateMenuMetadata([projectNode]);
            Assert.DoesNotContain(projectMenu, item => item.GuidId == SolutionCommandIds.Delete);
            Assert.DoesNotContain(projectMenu, item => item.GuidId == "MoveProjectToSolutionFolder");
            List<MenuItemMetadata> itemMenu = SolutionContextMenuService.CreateMenuMetadata([solutionItemNode]);
            Assert.DoesNotContain(itemMenu, item => item.GuidId == SolutionCommandIds.Delete);
            Assert.DoesNotContain(itemMenu, item => item.GuidId == "MoveSolutionItem");

            Assert.False(explorer.CanMoveSolutionItemsToFolder(
                [projectNode.Project],
                [],
                sourceFolder.FolderId,
                out string moveError));
            Assert.Equal(SolutionExplorer.ImportedStructureReadOnlyMessage, moveError);
            Assert.False(explorer.RegisterSolutionItems(
                [readmePath],
                sourceFolder.FolderId,
                out string registerError));
            Assert.Equal(SolutionExplorer.ImportedStructureReadOnlyMessage, registerError);
            Assert.False(explorer.RemoveProject(projectNode.Project));
            Assert.False(explorer.RemoveSolutionItem(solutionItemNode.ItemId));
            Assert.False(explorer.TryRenameSolutionFolder(sourceFolder.FolderId, "renamed"));
            InvalidOperationException createError = Assert.Throws<InvalidOperationException>(
                () => explorer.CreateSolutionFolder());
            Assert.Equal(SolutionExplorer.ImportedStructureReadOnlyMessage, createError.Message);

            SolutionConfig persistedConfig = SolutionConfigStore.Load(importedWorkspacePath).Config;
            Assert.Single(persistedConfig.Projects);
            Assert.Equal(2, persistedConfig.SolutionFolders.Count);
            Assert.Single(persistedConfig.SolutionItems);
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(importedWorkspacePath))
            {
                File.Delete(importedWorkspacePath);
                File.Delete($"{importedWorkspacePath}.bak");
                File.Delete($"{importedWorkspacePath}.cache.db");
            }
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void NativeSolutionRoot_KeepsWorkspaceEditorAndEditableMenu()
    {
        string directoryPath = CreateTemporaryDirectory();
        string solutionPath = Path.Combine(directoryPath, "Example.cvsln");
        try
        {
            SolutionConfigStore.Save(solutionPath, new SolutionConfig
            {
                RootPath = directoryPath,
                ProjectMode = SolutionProjectMode.Explicit,
            });
            using var explorer = CreateSolutionExplorer(directoryPath, solutionPath);

            Assert.False(explorer.IsImportedSolution);
            Assert.True(explorer.CanModifySolutionStructure);
            Assert.Null(explorer.ImportedSolutionSourcePath);
            Assert.Equal(solutionPath, explorer.EditorResourcePath, ignoreCase: true);
            Assert.NotEqual(SolutionContainerAction.None, explorer.SupportedContainerActions);
            List<MenuItemMetadata> rootMenu = SolutionContextMenuService.CreateMenuMetadata([explorer]);
            Assert.Contains(rootMenu, item => item.GuidId == "Edit"
                && ReferenceEquals(item.Command, explorer.EditCommand));
            Assert.DoesNotContain(rootMenu, item => item.GuidId == SolutionResourceCommands.ImportedSourceMenuId);
        }
        finally
        {
            File.Delete($"{solutionPath}.cache.db");
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
        int priority = 0,
        string extension = ".test",
        Func<IEditor>? factory = null)
    {
        return new EditorDescriptor(
            id,
            typeof(T),
            EditorResourceKind.File,
            isGeneric ? [] : [extension],
            isGeneric,
            isDefault,
            priority,
            IsVisibleInOpenWith: true)
        {
            Factory = factory,
        };
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

    private sealed class ThrowingNewItemTemplate : INewItemTemplate
    {
        public string Name => "Throwing";
        public string Category => "Tests";
        public string? Extension => ".txt";
        public int Order => 0;
        public System.Windows.Media.ImageSource? Icon => null;

        public string? GetDefaultContent(string fileName)
        {
            throw new InvalidOperationException("Template failed.");
        }
    }

    private sealed class BrokenMetadataNewItemTemplate : INewItemTemplate
    {
        public string Name => "Broken";
        public string Category => "Tests";
        public string? Extension => ".txt";
        public int Order => 0;
        public System.Windows.Media.ImageSource? Icon => null;

        public string? GetDefaultContent(string fileName) => string.Empty;

        public string? GetDefaultFileName()
        {
            throw new InvalidOperationException("Template metadata failed.");
        }
    }

    private sealed class ThrowingOpenEditor : IEditor
    {
        public void Open(string filePath)
        {
            throw new InvalidOperationException("Open failed.");
        }
    }

    private sealed class FactoryOnlyEditor : IEditor
    {
        private readonly string _dependency;

        public static string? LastDependency { get; set; }
        public static string? LastOpenedPath { get; set; }

        private FactoryOnlyEditor(string dependency)
        {
            _dependency = dependency;
        }

        public static FactoryOnlyEditor Create(string dependency) => new(dependency);

        public void Open(string filePath)
        {
            LastDependency = _dependency;
            LastOpenedPath = filePath;
        }
    }

    private sealed class BatchRecordingEditor : IEditor
    {
        public static List<string> OpenedPaths { get; } = new();

        public void Open(string filePath)
        {
            OpenedPaths.Add(filePath);
        }
    }

    private sealed class ThrowingConstructorEditor : IEditor
    {
        public ThrowingConstructorEditor()
        {
            throw new InvalidOperationException("Constructor failed.");
        }

        public void Open(string filePath)
        {
        }
    }

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

    private sealed class LegacyMenuNode : SolutionNode
    {
        private readonly string _menuId;

        public LegacyMenuNode(string menuId)
        {
            _menuId = menuId;
        }

        public override void InitMenuItem()
        {
            MenuItemMetadatas.Clear();
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = _menuId,
                Header = "legacy",
            });
        }
    }

    private sealed class TestSolutionMenuContribution : ISolutionMenuContribution
    {
        private readonly string _menuId;

        public string Id { get; }
        public SolutionMenuSelectionPolicy SelectionPolicy { get; }

        public TestSolutionMenuContribution(
            string id,
            string menuId,
            SolutionMenuSelectionPolicy selectionPolicy)
        {
            Id = id;
            _menuId = menuId;
            SelectionPolicy = selectionPolicy;
        }

        public bool IsApplicable(SolutionMenuContext context) => true;

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            return
            [
                new MenuItemMetadata
                {
                    GuidId = _menuId,
                    Order = 500,
                    Header = Id,
                },
            ];
        }
    }

    private sealed class TestFileMenuMeta : FileMetaBase
    {
        private readonly string _menuId;

        public TestFileMenuMeta(FileInfo fileInfo, string menuId)
        {
            FileInfo = fileInfo;
            Name = fileInfo.Name;
            _menuId = menuId;
        }

        public override IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return
            [
                new MenuItemMetadata
                {
                    GuidId = _menuId,
                    Order = 40,
                    Header = _menuId,
                },
            ];
        }
    }

    private sealed class TestFolderMenuMeta : FolderMetaBase
    {
        private readonly string _menuId;

        public TestFolderMenuMeta(DirectoryInfo directoryInfo, string menuId)
        {
            DirectoryInfo = directoryInfo;
            _menuId = menuId;
        }

        public override IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return
            [
                new MenuItemMetadata
                {
                    GuidId = _menuId,
                    Order = 40,
                    Header = _menuId,
                },
            ];
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

    private sealed class ControlledAsyncSolutionProvider : ISolutionFileProvider
    {
        public const string ProviderId = "tests.controlled-async-solution";
        private readonly string _blockedFileName;
        private readonly string? _projectReference;
        private readonly object _syncRoot = new();
        private readonly Dictionary<string, TaskCompletionSource<bool>> _started =
            new(StringComparer.OrdinalIgnoreCase);
        private int _syncLoadCount;

        public ControlledAsyncSolutionProvider(
            string blockedFileName,
            string? projectReference = null)
        {
            _blockedFileName = blockedFileName;
            _projectReference = projectReference;
        }

        public string Id => ProviderId;
        public IReadOnlyList<string> SolutionFilePatterns { get; } = ["*.asyncsln"];
        public int SyncLoadCount => Volatile.Read(ref _syncLoadCount);

        public bool CanLoad(FileInfo solutionFile) =>
            solutionFile.Exists
            && string.Equals(solutionFile.Extension, ".asyncsln", StringComparison.OrdinalIgnoreCase);

        public SolutionFileDefinition Load(FileInfo solutionFile)
        {
            Interlocked.Increment(ref _syncLoadCount);
            return CreateDefinition(solutionFile);
        }

        public async Task<SolutionFileDefinition> LoadAsync(
            FileInfo solutionFile,
            CancellationToken cancellationToken)
        {
            GetStartedSignal(solutionFile.FullName).TrySetResult(true);
            if (string.Equals(solutionFile.Name, _blockedFileName, StringComparison.OrdinalIgnoreCase))
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return CreateDefinition(solutionFile);
        }

        public async Task WaitUntilStartedAsync(string solutionPath)
        {
            await GetStartedSignal(solutionPath).Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        private TaskCompletionSource<bool> GetStartedSignal(string solutionPath)
        {
            lock (_syncRoot)
            {
                if (!_started.TryGetValue(solutionPath, out TaskCompletionSource<bool>? signal))
                {
                    signal = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _started[solutionPath] = signal;
                }
                return signal;
            }
        }

        private SolutionFileDefinition CreateDefinition(FileInfo solutionFile)
        {
            IReadOnlyList<SolutionFileProject> projects = string.IsNullOrWhiteSpace(_projectReference)
                ? Array.Empty<SolutionFileProject>()
                :
                [
                    new SolutionFileProject(
                        _projectReference,
                        null,
                        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Debug"] = "Debug",
                            ["Release"] = "Release",
                        }),
                ];
            return new SolutionFileDefinition(
                Id,
                Path.GetFileNameWithoutExtension(solutionFile.Name),
                solutionFile,
                solutionFile.Directory!,
                projects,
                Array.Empty<SolutionFileFolder>(),
                ["Debug", "Release"]);
        }
    }

    private sealed class CountingProjectProvider : IProjectProvider, IProjectFileFormatProvider
    {
        private int _loadCount;

        public string Id => "tests.counting-async-project";
        public IReadOnlyList<string> ProjectFilePatterns { get; } = ["*.asyncproj"];
        public int LoadCount => Volatile.Read(ref _loadCount);

        public bool CanLoad(FileInfo projectFile) =>
            projectFile.Exists
            && string.Equals(projectFile.Extension, ".asyncproj", StringComparison.OrdinalIgnoreCase);

        public ProjectDefinition Load(FileInfo projectFile)
        {
            Interlocked.Increment(ref _loadCount);
            return new ProjectDefinition(
                Id,
                Path.GetFileNameWithoutExtension(projectFile.Name),
                "1.0",
                projectFile,
                RootDirectory: projectFile.Directory);
        }
    }

    private sealed class ControlledAsyncProjectProvider : IProjectProvider, IProjectFileFormatProvider
    {
        private sealed record LoadGate(
            bool IgnoreCancellation,
            TaskCompletionSource<bool> Started,
            TaskCompletionSource<bool> Release,
            TaskCompletionSource<bool> Completed);

        private readonly string _extension;
        private readonly object _syncRoot = new();
        private readonly TaskCompletionSource<bool> _cancellationObserved = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private LoadGate? _nextGate;
        private LoadGate? _activeGate;
        private string _projectName;
        private int _syncLoadCount;
        private int _asyncLoadCount;

        public ControlledAsyncProjectProvider(string extension, string projectName)
        {
            _extension = extension;
            _projectName = projectName;
            Id = $"tests.controlled-async-project.{Guid.NewGuid():N}";
            ProjectFilePatterns = [$"*{extension}"];
        }

        public string Id { get; }
        public IReadOnlyList<string> ProjectFilePatterns { get; }
        public int SyncLoadCount => Volatile.Read(ref _syncLoadCount);
        public int AsyncLoadCount => Volatile.Read(ref _asyncLoadCount);
        public Task CancellationObserved => _cancellationObserved.Task;
        public string ProjectName
        {
            get => Volatile.Read(ref _projectName);
            set => Volatile.Write(ref _projectName, value);
        }

        public bool CanLoad(FileInfo projectFile) =>
            projectFile.Exists
            && projectFile.Name.EndsWith(_extension, StringComparison.OrdinalIgnoreCase);

        public ProjectDefinition Load(FileInfo projectFile)
        {
            Interlocked.Increment(ref _syncLoadCount);
            return CreateDefinition(projectFile, ProjectName);
        }

        public async Task<ProjectDefinition> LoadAsync(
            FileInfo projectFile,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _asyncLoadCount);
            LoadGate? gate;
            string projectName = ProjectName;
            lock (_syncRoot)
            {
                gate = _nextGate;
                _nextGate = null;
                if (gate != null)
                    _activeGate = gate;
            }

            if (gate != null)
            {
                gate.Started.TrySetResult(true);
                try
                {
                    if (gate.IgnoreCancellation)
                        await gate.Release.Task;
                    else
                        await gate.Release.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _cancellationObserved.TrySetResult(true);
                    throw;
                }
                finally
                {
                    gate.Completed.TrySetResult(true);
                }
            }

            if (gate?.IgnoreCancellation != true)
                cancellationToken.ThrowIfCancellationRequested();
            return CreateDefinition(projectFile, projectName);
        }

        public Task<bool> BlockNextLoad(bool ignoreCancellation)
        {
            lock (_syncRoot)
            {
                if (_nextGate != null)
                    throw new InvalidOperationException("已有等待中的项目加载门闩。");
                _nextGate = new LoadGate(
                    ignoreCancellation,
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
                    new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
                return _nextGate.Started.Task;
            }
        }

        public async Task ReleaseBlockedLoadAsync()
        {
            LoadGate? gate;
            lock (_syncRoot)
            {
                gate = _activeGate ?? _nextGate;
                _nextGate = null;
            }
            if (gate == null)
                return;

            gate.Release.TrySetResult(true);
            await gate.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));
            lock (_syncRoot)
            {
                if (ReferenceEquals(_activeGate, gate))
                    _activeGate = null;
            }
        }

        private ProjectDefinition CreateDefinition(FileInfo projectFile, string projectName) => new(
            Id,
            projectName,
            "1.0",
            projectFile,
            RootDirectory: projectFile.Directory);
    }

    private sealed class BlockingLegacySolutionProvider : ISolutionFileProvider
    {
        private readonly TaskCompletionSource<bool> _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _syncLoadCount;

        public string Id => "tests.blocking-legacy-solution";
        public IReadOnlyList<string> SolutionFilePatterns { get; } = ["*.legacysln"];
        public int SyncLoadCount => Volatile.Read(ref _syncLoadCount);
        public TaskCompletionSource<bool> Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Completed { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CanLoad(FileInfo solutionFile) =>
            solutionFile.Exists
            && string.Equals(solutionFile.Extension, ".legacysln", StringComparison.OrdinalIgnoreCase);

        public SolutionFileDefinition Load(FileInfo solutionFile)
        {
            Interlocked.Increment(ref _syncLoadCount);
            Started.TrySetResult(true);
            try
            {
                _release.Task.GetAwaiter().GetResult();
                return new SolutionFileDefinition(
                    Id,
                    Path.GetFileNameWithoutExtension(solutionFile.Name),
                    solutionFile,
                    solutionFile.Directory!,
                    Array.Empty<SolutionFileProject>(),
                    Array.Empty<SolutionFileFolder>(),
                    ["Debug", "Release"]);
            }
            finally
            {
                Completed.TrySetResult(true);
            }
        }

        public void Release()
        {
            _release.TrySetResult(true);
        }
    }
}
