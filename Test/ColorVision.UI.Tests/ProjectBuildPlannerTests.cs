using ColorVision.Solution.Explorer;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class ProjectBuildPlannerTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ColorVisionProjectBuildPlannerTests");

    [Fact]
    public void ResolvesFileAndDirectoryReferencesFromTheOwningProjectFile()
    {
        var library = Project("Library", "config/lib/library.cvproj", "sources/library");
        var duplicate = library with { Name = "Duplicate" };
        var app = Project("App", "config/app/app.cvproj", "sources/app",
            "../lib/LIBRARY.CVPROJ", "../../sources/library");

        ProjectBuildPlan plan = ProjectBuildPlanner.Create([app, library, duplicate], [app]);

        Assert.True(plan.IsValid);
        Assert.Equal(new[] { library, app }, plan.OrderedProjects);
    }

    [Fact]
    public void PreservesAmbiguousMissingAndCircularDependencyDiagnostics()
    {
        var app = Project("App", "app.cvproj", null, "shared", "\0", "peer.cvproj");
        var peer = Project("Peer", "peer.cvproj", null, "app.cvproj");
        var firstShared = Project("First", "first.cvproj", "shared");
        var secondShared = Project("Second", "second.cvproj", "shared");

        ProjectBuildPlan plan = ProjectBuildPlanner.Create([app, peer, firstShared, secondShared], [app]);

        Assert.Equal(new[] { peer, app }, plan.OrderedProjects);
        Assert.Equal(
            new[] { ProjectBuildDiagnosticKind.AmbiguousDependency, ProjectBuildDiagnosticKind.MissingDependency, ProjectBuildDiagnosticKind.CircularDependency },
            plan.Diagnostics.Select(diagnostic => diagnostic.Kind));
        Assert.Contains("App → Peer → App", plan.Diagnostics[2].Message, StringComparison.Ordinal);
    }

    private static ProjectDefinition Project(string name, string file, string? directory, params string[] dependencies)
        => new("folder", name, "", new FileInfo(Path.Combine(Root, file)),
            RootDirectory: directory == null ? null : new DirectoryInfo(Path.Combine(Root, directory)),
            Dependencies: dependencies);
}
