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
            HasSuccessfulEvidence = true,
            UsedPreselectedEvidence = true,
            ToolNames = ["ReadLocalFile"],
            Budget = new CopilotAgentBudgetSnapshot
            {
                ToolCalls = 1,
                PeakEstimatedInputTokens = 12_000,
                RegisteredToolCount = 48,
                AvailableToolCount = 3,
                AvailableToolDefinitionCharacters = 2_048,
                HarnessInstructionCharacters = 6_144,
            },
        }));

        var result = await tool.ExecuteAsync(Request(), Input(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CopilotToolFailureKind.None, result.FailureKind);
        Assert.Contains("Verified finding.", result.Content, StringComparison.Ordinal);
        Assert.Contains("preselected_evidence: true", result.Content, StringComparison.Ordinal);
        Assert.Equal(CopilotAgentStopReason.Completed, result.DelegatedRunUsage?.StopReason);
        Assert.Equal(12_000, result.DelegatedRunUsage?.PeakEstimatedInputTokens);
        Assert.Equal(48, result.DelegatedRunUsage?.RegisteredToolCount);
        Assert.Equal(3, result.DelegatedRunUsage?.AvailableToolCount);
        Assert.Equal(2_048, result.DelegatedRunUsage?.AvailableToolDefinitionCharacters);
        Assert.Equal(6_144, result.DelegatedRunUsage?.HarnessInstructionCharacters);
        var delegatedAnswer = Assert.IsType<CopilotDelegatedAnswer>(result.DelegatedAnswer);
        Assert.Equal("Verified finding.", delegatedAnswer.Text);
        Assert.Equal(CopilotAgentStopReason.Completed, delegatedAnswer.StopReason);
        Assert.True(delegatedAnswer.HasSuccessfulEvidence);
        Assert.False(delegatedAnswer.WasTruncated);
    }

    [Fact]
    public async Task CompletedTextWithoutSuccessfulToolEvidenceIsRejected()
    {
        var tool = new CopilotDelegateExploreTool(new StubRunner(new CopilotSubagentResult
        {
            Answer = "```tool_call\n{\"tool\":\"ReadFile\"}\n```",
            StopReason = CopilotAgentStopReason.Completed,
        }));

        var result = await tool.ExecuteAsync(Request(), Input(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Contains("without successful request-scoped tool evidence", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("successful_tool_evidence: false", result.Content, StringComparison.Ordinal);
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
        Assert.Equal(CopilotAgentStopReason.BudgetExhausted, result.DelegatedAnswer?.StopReason);
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
                UsedBudgetFinalization = result.UsedBudgetFinalization,
                UsedPreselectedEvidence = result.UsedPreselectedEvidence,
                HasSuccessfulEvidence = result.HasSuccessfulEvidence,
            });
        }
    }
}
