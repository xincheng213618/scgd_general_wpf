using ColorVision.Solution;
using ColorVision.Solution.Explorer;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class SolutionCreationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateOnlyAddsTheSolutionFileWhenItsNameIsAvailable(bool existingSolution)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-NewSolution-");
        string solutionPath = Path.Combine(directory.FullName, directory.Name + ".cvsln");
        string notesPath = Path.Combine(directory.FullName, "notes.txt");
        File.WriteAllText(notesPath, "keep these notes");
        const string originalSolution = "{\"SchemaVersion\":4,\"StartupProject\":\"existing.cvproj\"}";
        if (existingSolution) File.WriteAllText(solutionPath, originalSolution);

        using var cancellation = new CancellationTokenSource();
        var manager = new SolutionManager(restoreLastWorkspace: false);
        string? createdPath = null;
        manager.SolutionCreated += (sender, _) =>
        {
            createdPath = sender as string;
            cancellation.Cancel(); // Observe creation without activating the workspace.
        };

        try
        {
            if (existingSolution)
            {
                Exception? error = await Record.ExceptionAsync(() => manager.CreateSolutionAsync(directory.FullName, cancellation.Token));
                Assert.Equal(originalSolution, File.ReadAllText(solutionPath));
                Assert.IsType<IOException>(error);
                Assert.Null(createdPath);
            }
            else
            {
                Assert.False(await manager.CreateSolutionAsync(directory.FullName, cancellation.Token));
                Assert.Equal(solutionPath, createdPath);
                Assert.Equal(SolutionProjectMode.Explicit, SolutionConfigStore.Load(solutionPath).Config.ProjectMode);
            }

            Assert.Equal("keep these notes", File.ReadAllText(notesPath));
            Assert.Equal(2, directory.EnumerateFileSystemInfos().Count());
            Assert.Null(manager.CurrentSolutionExplorer);
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    [Fact]
    public async Task CancelledCreateLeavesNoWorkspaceDirectory()
    {
        DirectoryInfo parent = Directory.CreateTempSubdirectory("ColorVision-CancelSolution-");
        string path = Path.Combine(parent.FullName, "NewSolution");
        var manager = new SolutionManager(restoreLastWorkspace: false);
        manager.SolutionCreated += (_, _) => throw new InvalidOperationException("A cancelled creation cannot publish an event.");
        try
        {
            Assert.False(await manager.CreateSolutionAsync(path, new CancellationToken(canceled: true)));
            Assert.Empty(parent.EnumerateFileSystemInfos());
        }
        finally
        {
            if (Directory.Exists(path))
            {
                foreach (FileInfo file in new DirectoryInfo(path).EnumerateFiles()) file.Delete();
                Directory.Delete(path);
            }
            parent.Delete();
        }
    }

    [Fact]
    public void NewProjectRejectsATemplateThatProducesAnUnloadableProject()
    {
        DirectoryInfo parent = Directory.CreateTempSubdirectory("ColorVision-NewProject-");
        string projectPath = Path.Combine(parent.FullName, "Sample");
        string descriptorPath = Path.Combine(projectPath, "Sample.cvproj");
        try
        {
            var template = new BrokenProjectTemplate();
            Assert.False(ProjectTemplateRegistry.TryCreateFromTemplate(template, parent.FullName, "Sample", out var project, out string error));
            Assert.Null(project);
            Assert.Contains("模板没有创建可加载的项目", error);
            Assert.Contains("MissingRoot", error);
            Assert.Empty(parent.EnumerateFileSystemInfos());
        }
        finally
        {
            if (Directory.Exists(projectPath))
            {
                File.Delete(descriptorPath);
                Directory.Delete(projectPath);
            }
            parent.Delete();
        }
    }

    [Theory]
    [InlineData("Workspace.cvsln", "Workspace.cvsln")]
    [InlineData("Workspace.cvsln...", "Workspace.cvsln")]
    [InlineData("Camera.cvproj", "Camera.cvproj")]
    [InlineData("Application.CSPROJ", "Application.CSPROJ")]
    public void CreatingAnItemCannotOverwriteWorkspaceOrProjectMetadata(string requestedName, string existingName)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-NewItem-");
        string existingPath = Path.Combine(directory.FullName, existingName);
        byte[] original = [0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}'];
        try
        {
            File.WriteAllBytes(existingPath, original);
            var result = SolutionPhysicalItemOperations.CreateFromTemplate(new TextFileTemplate(), directory.FullName, requestedName, overwrite: true);

            Assert.Equal(original, File.ReadAllBytes(existingPath));
            Assert.False(result.IsComplete);
            Assert.Contains("解决方案或项目文件", Assert.Single(result.Failures).Message);
            Assert.Equal(existingPath, Assert.Single(Directory.GetFiles(directory.FullName)));
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    [Fact]
    public void CreatingASolutionItemRejectsNewMetadataAndStillAllowsOrdinaryOverwrite()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-NewSolutionItem-");
        try
        {
            var template = new TextFileTemplate();
            var invalid = SolutionPhysicalItemOperations.CreateFromTemplate(template, directory.FullName, "New.cvproj",
                overwrite: false, asSolutionItem: true);
            Assert.False(invalid.IsComplete);
            Assert.Empty(directory.EnumerateFiles());

            string textPath = Path.Combine(directory.FullName, "Notes.txt");
            File.WriteAllText(textPath, "previous text");
            var valid = SolutionPhysicalItemOperations.CreateFromTemplate(template, directory.FullName, "Notes.txt",
                overwrite: true, asSolutionItem: true);
            Assert.True(valid.IsComplete);
            Assert.Equal(template.GetDefaultContent("Notes.txt"), File.ReadAllText(textPath));
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private sealed class BrokenProjectTemplate : IProjectTemplate
    {
        public string Name => "Broken project";
        public string Category => "Test";
        public string Description => "Creates a descriptor with a missing root directory.";
        public int Order => 0;
        public System.Windows.Media.ImageSource? Icon => null;
        public string ProjectProviderId => FolderProjectProvider.ProviderId;

        public void CreateProject(string projectDir, string projectName) =>
            FolderProjectProvider.CreateProjectFile(Path.Combine(projectDir, projectName + ".cvproj"), projectName, rootPath: "MissingRoot");
    }
}
