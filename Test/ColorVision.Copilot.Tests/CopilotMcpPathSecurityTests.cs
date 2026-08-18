using ColorVision.Copilot.Mcp;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotMcpPathSecurityTests
{
    [Fact]
    public async Task ListAllowedDirectoryRejectsRelativeTraversalOutsideWorkspace()
    {
        var root = CreateRoot();
        try
        {
            var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
            {
                WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
                {
                    SolutionDirectoryPath = root,
                    SearchRootPaths = [root],
                },
            });

            var result = await dispatcher.CallAsync(
                "list_allowed_directory",
                new Dictionary<string, JsonElement>
                {
                    ["path"] = JsonSerializer.SerializeToElement(@"..\Outside"),
                },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("path_not_allowed", result.ErrorCode);
            Assert.Contains("outside", result.Text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ListAllowedDirectoryRejectsReparsePointPath()
    {
        var root = CreateRoot();
        var outside = Path.Combine(root, "Outside");
        var linkedDirectory = Path.Combine(root, "LinkedDirectory");
        Directory.CreateDirectory(outside);
        Directory.CreateSymbolicLink(linkedDirectory, outside);

        try
        {
            var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
            {
                WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
                {
                    SolutionDirectoryPath = root,
                    SearchRootPaths = [root],
                },
            });

            var result = await dispatcher.CallAsync(
                "list_allowed_directory",
                new Dictionary<string, JsonElement>
                {
                    ["path"] = JsonSerializer.SerializeToElement(linkedDirectory),
                },
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("path_not_allowed", result.ErrorCode);
            Assert.Contains("reparse point", result.Text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(linkedDirectory, recursive: false);
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ColorVision.UI.Tests",
            nameof(CopilotMcpPathSecurityTests),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
