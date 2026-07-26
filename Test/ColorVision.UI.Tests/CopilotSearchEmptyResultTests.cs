using System.IO;
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotSearchEmptyResultTests
{
    [Fact]
    public async Task CompletedEmptySearchesReturnSuccessfulEvidence()
    {
        var workspaceRoot = CreateWorkspace();
        try
        {
            var request = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Auto,
                UserText = "find missing.file and missing literal",
                SearchRootPaths = [workspaceRoot],
            };

            var fileResult = await new CopilotSearchFilesTool().ExecuteAsync(
                request,
                new CopilotAgentToolInput
                {
                    Query = "missing.file",
                    Path = workspaceRoot,
                },
                CancellationToken.None);
            var textResult = await new CopilotGrepTextTool().ExecuteAsync(
                request,
                new CopilotAgentToolInput
                {
                    Query = "missing literal",
                    Path = workspaceRoot,
                },
                CancellationToken.None);

            Assert.True(fileResult.Success, fileResult.ErrorMessage);
            Assert.Equal(CopilotToolFailureKind.None, fileResult.FailureKind);
            Assert.Empty(fileResult.ErrorMessage);
            Assert.Contains("[Matched Files] 0", fileResult.Content, StringComparison.Ordinal);
            Assert.Contains("[Scan Complete] true", fileResult.Content, StringComparison.Ordinal);
            Assert.Contains("[Result] No candidate files matched", fileResult.Content, StringComparison.Ordinal);

            Assert.True(textResult.Success, textResult.ErrorMessage);
            Assert.Equal(CopilotToolFailureKind.None, textResult.FailureKind);
            Assert.Empty(textResult.ErrorMessage);
            Assert.Contains("[Matches Shown] 0", textResult.Content, StringComparison.Ordinal);
            Assert.Contains("[Scan Complete] true", textResult.Content, StringComparison.Ordinal);
            Assert.Contains("[Result] No text lines matched", textResult.Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public void InvalidSearchQueriesRemainFailures()
    {
        var workspaceRoot = CreateWorkspace();
        try
        {
            var fileResult = CopilotSearchFilesCapability.SearchWithinScope(
                [workspaceRoot],
                [workspaceRoot],
                "bad\r\nquery",
                fallbackText: string.Empty,
                allowPlainSearchTerms: false,
                cursor: null,
                CancellationToken.None);
            var textResult = CopilotGrepTextCapability.SearchWithinScope(
                [workspaceRoot],
                [workspaceRoot],
                "bad\r\nquery",
                fallbackText: string.Empty,
                cursor: null,
                CancellationToken.None);

            Assert.False(fileResult.Success);
            Assert.NotEmpty(fileResult.ErrorMessage);
            Assert.False(textResult.Success);
            Assert.NotEmpty(textResult.ErrorMessage);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static string CreateWorkspace()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "ColorVisionCopilotEmptySearchTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllText(
            Path.Combine(workspaceRoot, "Existing.cs"),
            "namespace Sample; public sealed class Existing { }");
        return workspaceRoot;
    }
}
