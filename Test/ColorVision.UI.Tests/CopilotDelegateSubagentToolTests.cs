using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotDelegateSubagentToolTests
{
    [Fact]
    public async Task CompletedAnswerIsReportedAsSuccessful()
    {
        var tool = new CopilotDelegateExploreTool(new StubRunner(new CopilotSubagentResult
        {
            Answer = "Verified finding.",
            StopReason = CopilotAgentStopReason.Completed,
        }));

        var result = await tool.ExecuteAsync(Request(), Input(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CopilotToolFailureKind.None, result.FailureKind);
        Assert.Contains("Verified finding.", result.Content, StringComparison.Ordinal);
        Assert.Equal(CopilotAgentStopReason.Completed, result.DelegatedRunUsage?.StopReason);
    }

    [Fact]
    public async Task BudgetExhaustedAnswerIsPreservedButNotReportedAsSuccessful()
    {
        var tool = new CopilotDelegateExploreTool(new StubRunner(new CopilotSubagentResult
        {
            Answer = "Partial observation.",
            StopReason = CopilotAgentStopReason.BudgetExhausted,
        }));

        var result = await tool.ExecuteAsync(Request(), Input(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Contains("部分结果", result.Summary, StringComparison.Ordinal);
        Assert.Contains("Partial observation.", result.Content, StringComparison.Ordinal);
        Assert.Contains("evidence only", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(CopilotAgentStopReason.BudgetExhausted, result.DelegatedRunUsage?.StopReason);
    }

    [Fact]
    public async Task CancelledAnswerUsesCancelledFailureKind()
    {
        var tool = new CopilotDelegateExploreTool(new StubRunner(new CopilotSubagentResult
        {
            Answer = "Interrupted observation.",
            StopReason = CopilotAgentStopReason.Cancelled,
        }));

        var result = await tool.ExecuteAsync(Request(), Input(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Cancelled, result.FailureKind);
    }

    private static CopilotAgentRequest Request()
    {
        return new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = @"只读审计 C:\workspace，列出 1 条可验证的问题；不要修改文件。",
            SearchRootPaths = [@"C:\workspace"],
        };
    }

    private static CopilotAgentToolInput Input()
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["task"] = "Inspect the workspace and return one verified finding.",
            },
        };
    }

    private sealed class StubRunner(CopilotSubagentResult result) : ICopilotSubagentRunner
    {
        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotSubagentResult
            {
                RoleId = role.Id,
                RunId = runRequest.RunId,
                RequestTokenBudget = runRequest.RequestTokenBudget,
                QueueDurationMs = runRequest.QueueDurationMs,
                Answer = result.Answer,
                StopReason = result.StopReason,
                Usage = result.Usage,
                Budget = result.Budget,
                ToolNames = result.ToolNames,
                WasTruncated = result.WasTruncated,
            });
        }
    }
}
