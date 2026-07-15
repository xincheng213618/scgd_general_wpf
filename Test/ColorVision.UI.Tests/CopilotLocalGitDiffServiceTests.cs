#pragma warning disable CA1707
using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotLocalGitDiffServiceTests
{
    [Fact]
    public async Task ExecuteAsync_CombinesBoundedStatusUntrackedFilesAndSelectedDiff()
    {
        CopilotAgentRequest? capturedRequest = null;
        CopilotAgentToolInput? capturedDiffInput = null;
        var service = new CopilotLocalGitDiffService(
            (request, _, _) =>
            {
                capturedRequest = request;
                return Task.FromResult(Success("InspectGitWorkingTree", """
                    {"repository_root":"C:\\repo","branch":"develop","changed_path_count":3,"staged_count":1,"unstaged_count":1,"untracked_count":1,"conflict_count":0,"entries_truncated":false,"entries":[{"path":"notes.txt","is_untracked":true}]}
                    """));
            },
            (_, input, _) =>
            {
                capturedDiffInput = input;
                return Task.FromResult(Success("InspectGitDiff", """
                    {"patch_truncated":false,"sections":[{"scope":"unstaged","patch":"diff --git a/a.cs b/a.cs\n+new"},{"scope":"staged","patch":"diff --git a/b.cs b/b.cs\n+staged"}]}
                    """));
            });

        var result = await service.ExecuteAsync(["C:\\repo"], "both", CancellationToken.None);

        Assert.True(result.Success, result.Report);
        Assert.Equal(CopilotAgentMode.Diagnose, capturedRequest?.Mode);
        Assert.Equal(["C:\\repo"], capturedRequest?.SearchRootPaths);
        Assert.Equal("both", capturedDiffInput?.Arguments["scope"]);
        Assert.Contains("分支：develop", result.Report, StringComparison.Ordinal);
        Assert.Contains("1 已暂存 · 1 未暂存 · 1 未跟踪", result.Report, StringComparison.Ordinal);
        Assert.Contains("?? notes.txt", result.Report, StringComparison.Ordinal);
        Assert.Contains("未暂存补丁", result.Report, StringComparison.Ordinal);
        Assert.Contains("已暂存补丁", result.Report, StringComparison.Ordinal);
        Assert.Contains("+new", result.Report, StringComparison.Ordinal);
        Assert.Contains("+staged", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultsToBothAndRejectsUnknownScopeBeforeGit()
    {
        var calls = 0;
        var service = new CopilotLocalGitDiffService(
            (_, _, _) =>
            {
                calls++;
                return Task.FromResult(Success("InspectGitWorkingTree", """
                    {"repository_root":"C:\\repo","branch":"develop","changed_path_count":0,"staged_count":0,"unstaged_count":0,"untracked_count":0,"conflict_count":0,"entries_truncated":false,"entries":[]}
                    """));
            },
            (_, input, _) =>
            {
                calls++;
                Assert.Equal("both", input.Arguments["scope"]);
                return Task.FromResult(Success("InspectGitDiff", """{"patch_truncated":false,"sections":[]}"""));
            });

        var defaultResult = await service.ExecuteAsync(["C:\\repo"], string.Empty, CancellationToken.None);
        var invalidResult = await service.ExecuteAsync(["C:\\repo"], "everything", CancellationToken.None);

        Assert.True(defaultResult.Success, defaultResult.Report);
        Assert.Contains("所选范围没有补丁", defaultResult.Report, StringComparison.Ordinal);
        Assert.False(invalidResult.Success);
        Assert.Contains("/diff [both|staged|unstaged]", invalidResult.Report, StringComparison.Ordinal);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ExecuteAsync_StopsAfterStatusFailureAndRedactsDetails()
    {
        var diffCalls = 0;
        var service = new CopilotLocalGitDiffService(
            (_, _, _) => Task.FromResult(new CopilotToolResult
            {
                ToolName = "InspectGitWorkingTree",
                Success = false,
                Summary = "No Git working tree was found.",
                ErrorMessage = "api_key=secret",
                FailureKind = CopilotToolFailureKind.NotFound,
            }),
            (_, _, _) =>
            {
                diffCalls++;
                return Task.FromResult(new CopilotToolResult());
            });

        var result = await service.ExecuteAsync(["C:\\repo"], "both", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("No Git working tree", result.Report, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", result.Report, StringComparison.Ordinal);
        Assert.Equal(0, diffCalls);
    }

    private static CopilotToolResult Success(string toolName, string json)
    {
        return new CopilotToolResult
        {
            ToolName = toolName,
            Success = true,
            Content = "result_json: " + json,
        };
    }
}
