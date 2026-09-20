using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspacePatchScopeTests
{
    [Theory]
    [InlineData("camera.json")]
    [InlineData(".\\camera.json")]
    [InlineData("profiles\\camera.json")]
    public void FileOnlyGrantResolvesRelativeToActiveWorkspace(string relativePath)
    {
        using var fixture = new FileGrantFixture();
        var target = Path.GetFullPath(relativePath, fixture.Workspace);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "{\"gain\":1}");
        var request = new CopilotAgentRequest { WorkspacePath = fixture.Workspace, WritableLocalFilePaths = [target] };

        Assert.True(CopilotWorkspacePatchScope.TryResolve(request, relativePath, 20_000, out var resolved, out var error), error);
        Assert.Equal(target, resolved);
        Assert.Empty(request.WritableLocalRootPaths);
    }

    [Fact]
    public void WorkspaceAnchorDoesNotGrantSiblingOrNewFileWrites()
    {
        using var fixture = new FileGrantFixture();
        var allowed = Path.Combine(fixture.Workspace, "camera.json");
        var sibling = Path.Combine(fixture.Workspace, "protected.json");
        var newFile = Path.Combine(fixture.Workspace, "new.json");
        File.WriteAllText(allowed, "{}");
        File.WriteAllText(sibling, "{\"protected\":true}");
        var request = new CopilotAgentRequest { WorkspacePath = fixture.Workspace, WritableLocalFilePaths = [allowed, newFile] };

        Assert.False(CopilotWorkspacePatchScope.TryResolve(request, "protected.json", 20_000, out _, out var error));
        Assert.Contains("neither explicitly writable", error, StringComparison.Ordinal);
        Assert.False(CopilotWorkspacePatchScope.TryResolveNewFile(request, "new.json", out _, out _, out _));
        Assert.False(CopilotWorkspacePatchScope.TryResolveNewFile(request, newFile, out _, out _, out _));
        Assert.Equal("{\"protected\":true}", File.ReadAllText(sibling));
        Assert.False(File.Exists(newFile));
    }

    [Fact]
    public void RelativeTraversalCannotSelectAnOutsideExactGrant()
    {
        using var fixture = new FileGrantFixture();
        var outside = Path.Combine(fixture.Root, "outside.json");
        File.WriteAllText(outside, "{}");
        var request = new CopilotAgentRequest { WorkspacePath = fixture.Workspace, WritableLocalFilePaths = [outside] };

        Assert.False(CopilotWorkspacePatchScope.TryResolve(request, "..\\outside.json", 20_000, out _, out var error));
        Assert.Contains("outside the allowed workspace roots", error, StringComparison.Ordinal);
        Assert.True(CopilotWorkspacePatchScope.TryResolve(request, outside, 20_000, out _, out error), error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(".")]
    public void MissingAbsoluteWorkspaceDoesNotUseProcessWorkingDirectory(string? workspace)
    {
        using var fixture = new FileGrantFixture();
        var target = Path.Combine(fixture.Workspace, "camera.json");
        File.WriteAllText(target, "{}");
        var request = new CopilotAgentRequest { WorkspacePath = workspace!, WritableLocalFilePaths = [target] };

        Assert.False(CopilotWorkspacePatchScope.TryResolve(request, "camera.json", 20_000, out _, out var relativeError));
        Assert.Contains("fully qualified", relativeError, StringComparison.Ordinal);
        Assert.True(CopilotWorkspacePatchScope.TryResolve(request, target, 20_000, out _, out var error), error);
    }

    [Fact]
    public void ActiveWorkspaceDoesNotResolveAmbiguityAcrossWritableRoots()
    {
        using var fixture = new FileGrantFixture();
        var secondRoot = Directory.CreateDirectory(Path.Combine(fixture.Root, "second")).FullName;
        File.WriteAllText(Path.Combine(fixture.Workspace, "camera.json"), "{}");
        File.WriteAllText(Path.Combine(secondRoot, "camera.json"), "{}");
        var request = new CopilotAgentRequest { WorkspacePath = fixture.Workspace, WritableLocalRootPaths = [fixture.Workspace, secondRoot] };

        Assert.False(CopilotWorkspacePatchScope.TryResolve(request, "camera.json", 20_000, out _, out var error));
        Assert.Contains("ambiguous", error, StringComparison.Ordinal);
    }

    [Fact]
    public void FileOnlyRelativeGrantStillRejectsReparsePointParents()
    {
        using var fixture = new FileGrantFixture();
        var outside = Directory.CreateDirectory(Path.Combine(fixture.Root, "outside")).FullName;
        File.WriteAllText(Path.Combine(outside, "camera.json"), "{}");
        var link = Path.Combine(fixture.Workspace, "linked");
        Directory.CreateSymbolicLink(link, outside);
        try
        {
            var request = new CopilotAgentRequest { WorkspacePath = fixture.Workspace, WritableLocalFilePaths = [Path.Combine(link, "camera.json")] };
            Assert.False(CopilotWorkspacePatchScope.TryResolve(request, "linked\\camera.json", 20_000, out _, out var error));
            Assert.NotEmpty(error);
            Assert.Equal("{}", File.ReadAllText(Path.Combine(outside, "camera.json")));
        }
        finally { Directory.Delete(link, recursive: false); }
    }

    [Fact]
    public void ExplicitFileOutsideWorkspaceRejectsReparsePointParent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"copilot-patch-scope-{Guid.NewGuid():N}");
        var workspace = Path.Combine(root, "Workspace");
        var outside = Path.Combine(root, "Outside");
        var linkedParent = Path.Combine(root, "LinkedParent");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(outside);
        var target = Path.Combine(linkedParent, "sample.cs");
        File.WriteAllText(Path.Combine(outside, "sample.cs"), "class Sample { }");
        Directory.CreateSymbolicLink(linkedParent, outside);

        try
        {
            var request = new CopilotAgentRequest
            {
                WritableLocalRootPaths = [workspace],
                WritableLocalFilePaths = [target],
            };

            var resolved = CopilotWorkspacePatchScope.TryResolve(
                request,
                target,
                maxFileBytes: 20_000,
                out _,
                out var error);

            Assert.False(resolved);
            Assert.Contains("reparse point", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(linkedParent, recursive: false);
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FileGrantFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotFileGrant-").FullName;
        public string Workspace { get; }

        public FileGrantFixture() => Workspace = Directory.CreateDirectory(Path.Combine(Root, "workspace")).FullName;

        public void Dispose()
        {
            var resolved = Path.GetFullPath(Root);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), resolved, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("CopilotFileGrant-", Path.GetFileName(resolved));
            Directory.Delete(resolved, recursive: true);
        }
    }
}
