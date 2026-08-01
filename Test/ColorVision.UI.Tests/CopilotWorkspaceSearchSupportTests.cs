using ColorVision.Copilot;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotWorkspaceSearchSupportTests
{
    [Fact]
    public void SearchRootsAndChildrenRejectReparsePoints()
    {
        var root = CreateRoot();
        var outside = Path.Combine(root, "Outside");
        var linkedRoot = Path.Combine(root, "LinkedRoot");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(linkedRoot, outside);

        try
        {
            Assert.Empty(CopilotWorkspaceSearchSupport.NormalizeSearchRoots([linkedRoot]));
            Assert.False(CopilotWorkspaceSearchSupport.IsPathWithinRoots(
                Path.Combine(linkedRoot, "outside.txt"),
                [root]));
        }
        finally
        {
            Directory.Delete(linkedRoot, recursive: false);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TextReadsRejectFileAndParentDirectoryReparsePoints()
    {
        var root = CreateRoot();
        var outside = Path.Combine(root, "Outside");
        var outsideFile = Path.Combine(outside, "outside.txt");
        var linkedFile = Path.Combine(root, "LinkedFile.txt");
        var linkedDirectory = Path.Combine(root, "LinkedDirectory");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(outsideFile, "outside");
        File.CreateSymbolicLink(linkedFile, outsideFile);
        Directory.CreateSymbolicLink(linkedDirectory, outside);

        try
        {
            var linkedFileResult = await CopilotLocalFileToolSupport.ReadTextFileAsync(
                linkedFile,
                CancellationToken.None);
            var linkedDirectoryResult = await CopilotLocalFileToolSupport.ReadTextFileAsync(
                Path.Combine(linkedDirectory, "outside.txt"),
                CancellationToken.None);

            Assert.False(linkedFileResult.Success);
            Assert.Contains("reparse point", linkedFileResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(linkedDirectoryResult.Success);
            Assert.Contains("reparse point", linkedDirectoryResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(linkedFile);
            Directory.Delete(linkedDirectory, recursive: false);
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.UI.Tests",
            nameof(CopilotWorkspaceSearchSupportTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
