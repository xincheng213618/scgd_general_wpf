using ColorVision.Copilot;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLocalPathAuthorizationTests
{
    [Theory]
    [InlineData("ListDirectory", "missing")]
    [InlineData("ListDirectory", "missing/deeper")]
    [InlineData("GrepText", "workspace/logs/line-a")]
    [InlineData("SearchFiles", "missing/deeper")]
    [InlineData("ReadLocalFile", "missing/deeper/camera.json")]
    public async Task MissingWorkspacePathsReportAbsenceWithoutClaimingAnEscape(string toolName, string path)
    {
        using var fixture = new PathResolutionFixture();
        ICopilotTool tool = toolName switch
        {
            "ListDirectory" => new CopilotListDirectoryTool(),
            "GrepText" => new CopilotGrepTextTool(),
            "SearchFiles" => new CopilotSearchFilesTool(),
            _ => new CopilotReadLocalFileTool(),
        };
        var request = new CopilotAgentRequest { WorkspacePath = fixture.Workspace, SearchRootPaths = [fixture.Workspace] };
        var result = await tool.ExecuteAsync(request, new() { Path = path, Query = "camera" }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("escapes", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("outside", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reparse point", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'.'", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.SuccessfullyReadLocalFilePaths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AbsoluteAndRelativeMissingPathsHaveTheSameDiagnosis(bool absolute)
    {
        using var fixture = new PathResolutionFixture();
        var relative = "missing/deeper/camera.json";
        var path = absolute ? Path.GetFullPath(relative, fixture.Workspace) : relative;
        Assert.False(CopilotWorkspaceSearchSupport.TryResolveExistingPathWithinRoots(path, [fixture.Workspace], out var resolved, out var error));
        Assert.Empty(resolved);
        Assert.Contains("does not exist", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../outside/camera.json")]
    [InlineData("../outside/missing/camera.json")]
    public void TraversalRemainsAnAuthorizationFailureEvenWhenTheTargetIsMissing(string relative)
    {
        using var fixture = new PathResolutionFixture();
        Assert.False(CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots(relative, [fixture.Workspace], out var resolved, out var error));
        Assert.Empty(resolved);
        Assert.Contains("outside", error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("does not exist", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("camera.json")]
    [InlineData("missing/deeper/camera.json")]
    public void ReparsePointsAreReportedBeforeInspectingMissingDescendants(string child)
    {
        using var fixture = new PathResolutionFixture();
        var link = Path.Combine(fixture.Workspace, "linked");
        Directory.CreateSymbolicLink(link, fixture.Outside);
        try
        {
            Assert.False(CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots("linked/" + child, [fixture.Workspace], out var resolved, out var error));
            Assert.Empty(resolved);
            Assert.Contains("reparse point", error, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("does not exist", error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("protected", File.ReadAllText(Path.Combine(fixture.Outside, "camera.json")));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public void AFileCannotBeMistakenForAMissingParentDirectory()
    {
        using var fixture = new PathResolutionFixture();
        File.WriteAllText(Path.Combine(fixture.Workspace, "occupied"), "data");
        Assert.False(CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots("occupied/camera.json", [fixture.Workspace], out _, out var error));
        Assert.Contains("parent component is a file", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingObjectsOfTheWrongKindAreNotReportedMissing()
    {
        using var fixture = new PathResolutionFixture();
        File.WriteAllText(Path.Combine(fixture.Workspace, "camera.json"), "{}");
        Assert.False(CopilotWorkspaceSearchSupport.TryResolveExistingDirectoryWithinRoots("camera.json", [fixture.Workspace], out _, out var directoryError));
        Assert.Contains("a directory is required", directoryError, StringComparison.OrdinalIgnoreCase);
        Assert.False(CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots(".", [fixture.Workspace], out _, out var fileError));
        Assert.Contains("a file is required", fileError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultipleUnresolvedRootsProduceBoundedCandidateSpecificErrors()
    {
        using var fixture = new PathResolutionFixture();
        var roots = Enumerable.Range(0, 5).Select(i => Directory.CreateDirectory(Path.Combine(fixture.Root, $"root-{i}")).FullName).ToArray();
        Assert.False(CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots("missing/deeper/camera.json", roots, out _, out var error));
        foreach (var root in roots.Take(3)) Assert.Contains(root, error, StringComparison.Ordinal);
        foreach (var root in roots.Skip(3)) Assert.DoesNotContain(root, error, StringComparison.Ordinal);
        Assert.Contains("2 additional roots", error, StringComparison.Ordinal);
        Assert.DoesNotContain("outside", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultipleRootsRetainUniqueResolutionAndAmbiguityRules()
    {
        using var fixture = new PathResolutionFixture();
        var second = Directory.CreateDirectory(Path.Combine(fixture.Root, "second")).FullName;
        Directory.CreateDirectory(Path.Combine(second, "nested"));
        var file = Path.Combine(second, "nested", "camera.json");
        File.WriteAllText(file, "{}");
        Assert.True(CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots("nested/camera.json", [fixture.Workspace, second], out var resolved, out var error), error);
        Assert.Equal(file, resolved);
        Directory.CreateDirectory(Path.Combine(fixture.Workspace, "nested"));
        File.WriteAllText(Path.Combine(fixture.Workspace, "nested", "camera.json"), "{}");
        Assert.False(CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots("nested/camera.json", [fixture.Workspace, second], out _, out error));
        Assert.Contains("ambiguous", error, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class PathResolutionFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("copilot-path-resolution-").FullName;
        public string Workspace { get; }
        public string Outside { get; }
        public PathResolutionFixture()
        {
            Workspace = Directory.CreateDirectory(Path.Combine(Root, "workspace")).FullName;
            Outside = Directory.CreateDirectory(Path.Combine(Root, "outside")).FullName;
            File.WriteAllText(Path.Combine(Outside, "camera.json"), "protected");
        }
        public void Dispose()
        {
            var fullPath = Path.GetFullPath(Root);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), fullPath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("copilot-path-resolution-", Path.GetFileName(fullPath));
            Directory.Delete(fullPath, recursive: true);
        }
    }

    [Fact]
    public void AgentRequestFreezesPathAuthorizationAndEvidenceRequirements()
    {
        var searchRoots = new List<string> { @"C:\search" };
        var trustedRoots = new List<string> { @"C:\trusted" };
        var readableFiles = new List<string> { @"C:\read\file.txt" };
        var readableDirectories = new List<string> { @"C:\read" };
        var writableRoots = new List<string> { @"C:\write" };
        var writableFiles = new List<string> { @"C:\write\file.txt" };
        var requiredTools = new List<string> { "DelegateExplore" };
        var request = new CopilotAgentRequest
        {
            SearchRootPaths = searchRoots,
            TrustedProjectRootPaths = trustedRoots,
            ReadableLocalFilePaths = readableFiles,
            ReadableLocalDirectoryPaths = readableDirectories,
            WritableLocalRootPaths = writableRoots,
            WritableLocalFilePaths = writableFiles,
            RequiredSuccessfulToolNames = requiredTools,
        };

        searchRoots.Clear();
        trustedRoots.Clear();
        readableFiles.Clear();
        readableDirectories.Clear();
        writableRoots.Clear();
        writableFiles.Clear();
        requiredTools.Clear();

        Assert.Equal(@"C:\search", Assert.Single(request.SearchRootPaths));
        Assert.Equal(@"C:\trusted", Assert.Single(request.TrustedProjectRootPaths));
        Assert.Equal(@"C:\read\file.txt", Assert.Single(request.ReadableLocalFilePaths));
        Assert.Equal(@"C:\read", Assert.Single(request.ReadableLocalDirectoryPaths));
        Assert.Equal(@"C:\write", Assert.Single(request.WritableLocalRootPaths));
        Assert.Equal(@"C:\write\file.txt", Assert.Single(request.WritableLocalFilePaths));
        Assert.Equal("DelegateExplore", Assert.Single(request.RequiredSuccessfulToolNames));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)request.WritableLocalRootPaths).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)request.RequiredSuccessfulToolNames).Clear());
    }

    [Fact]
    public void ExplicitAllowListRequiresAnExactFullyQualifiedPath()
    {
        var allowedPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "copilot-explicit-path", "sample.cs"));

        Assert.True(CopilotWorkspaceSearchSupport.IsExplicitlyAllowedPath(
            allowedPath.ToUpperInvariant(),
            [allowedPath]));
        Assert.False(CopilotWorkspaceSearchSupport.IsExplicitlyAllowedPath("sample.cs", [allowedPath]));
        Assert.False(CopilotWorkspaceSearchSupport.IsExplicitlyAllowedPath(
            Path.Combine(Path.GetDirectoryName(allowedPath)!, "sibling.cs"),
            [allowedPath]));
    }

    [Fact]
    public async Task BareDirectoryBeforeChinesePunctuationRemainsAuthorizedAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-local-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "sample.cs"), "namespace Sample;");
        try
        {
            var userText = $"只读审计 {root}，列出至少 30 条可验证的问题；不要修改任何文件。";
            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: null,
                attachments: null);

            var plan = CopilotAgentRequestFactory.Prepare(userText, CopilotAgentMode.Auto, hostContext);

            Assert.Equal([root], plan.ReadableLocalDirectoryPaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([root], plan.SearchRootPaths, StringComparer.OrdinalIgnoreCase);

            var request = new CopilotAgentRequest
            {
                UserText = userText,
                Mode = CopilotAgentMode.Auto,
                ReadableLocalDirectoryPaths = plan.ReadableLocalDirectoryPaths,
                SearchRootPaths = plan.SearchRootPaths,
            };
            var result = await new CopilotListDirectoryTool().ExecuteAsync(
                request,
                new CopilotAgentToolInput { Path = root },
                CancellationToken.None);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Contains("sample.cs", result.Content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void QuotedPathWithSpacesAndPunctuationDoesNotProduceAPartialBarePath()
    {
        var expected = Path.GetFullPath(@"C:\workspace folder\sample,one.cs");

        var paths = CopilotLocalFileToolSupport.ExtractExplicitLocalFilePaths(
            "检查 “C:\\workspace folder\\sample,one.cs”，然后说明结果。");

        Assert.Equal([expected], paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkdownLinkWithForwardSlashesProducesAWindowsPath()
    {
        var expected = Path.GetFullPath(@"C:\workspace\sample.cs");

        var paths = CopilotLocalFileToolSupport.ExtractExplicitLocalFilePaths(
            "See [sample.cs](<C:/workspace/sample.cs:42>) for the verified branch.");

        Assert.Equal([expected], paths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingAbsoluteFileKeepsItsExistingParentSearchable()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-missing-file-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var missingFile = Path.Combine(root, "GeneratedLater.cs");
        try
        {
            var plan = CopilotAgentRequestFactory.Prepare(
                $"检查文件 {missingFile}",
                CopilotAgentMode.Auto,
                new CopilotAgentHostContextSnapshot(
                    activeDocumentPath: null,
                    solutionDirectoryPath: null,
                    attachments: null));

            Assert.Equal([missingFile], plan.ReadableLocalFilePaths, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([root], plan.SearchRootPaths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
