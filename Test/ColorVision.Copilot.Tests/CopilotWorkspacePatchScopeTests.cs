using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspacePatchScopeTests
{
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
}
