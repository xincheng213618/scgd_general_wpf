using ColorVision.Copilot;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexInterruptMessageTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ActualCancellationKeepsLocalAuditButHonorsModelVisibility(bool interruptMessageEnabled)
    {
        var runner = new BlockingSubagentRunner();
        var tool = new CopilotDelegateExploreTool(runner);
        var request = new CopilotAgentRequest
        {
            ConversationId = "interrupt-message-" + Guid.NewGuid().ToString("N"),
            UserText = "Delegate a bounded workspace investigation.",
            TaskIntentText = "Delegate a bounded workspace investigation.",
            Profile = CopilotProfileConfig.CreateDefault(),
            CodexInterruptMessageEnabled = interruptMessageEnabled,
        };
        var progress = new CopilotToolProgressContext();
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Task<CopilotToolResult> executionTask = tool.ExecuteWithProgressAsync(
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["task"] = "Inspect the bounded workspace evidence.",
                },
            },
            progress,
            testCancellation.Token);
        await runner.Started.Task.WaitAsync(testCancellation.Token);
        string runId = Assert.IsType<CopilotDelegatedRunProgress>(
            progress.LatestSnapshot?.DelegatedRun).RunId;

        Assert.Equal(
            CopilotSubagentCancelResult.Requested,
            CopilotSubagentCoordination.RequestCancelActiveRun(request.ConversationId, runId));
        CopilotToolResult result = await executionTask.WaitAsync(testCancellation.Token);
        var outcome = new CopilotToolExecutionOutcome
        {
            Invocation = new CopilotToolInvocation
            {
                CallId = "interrupt-message-call",
                Round = 1,
                Tool = tool,
                AgentRequest = request,
                ToolCall = new CopilotToolCall { ToolName = tool.Name },
            },
            Result = result,
            Execution = new CopilotToolExecutionInfo
            {
                CallId = "interrupt-message-call",
                Round = 1,
                ToolName = tool.Name,
                State = CopilotToolExecutionState.Cancelled,
                FailureKind = CopilotToolFailureKind.Cancelled,
            },
        };
        string modelOutput = CopilotFrameworkToolResultFormatter.Format(outcome);
        string recoveryObservations = new CopilotAgentContextBuilder().BuildObservationSummary(
            [outcome.StepRecord],
            maxSteps: 4,
            maxContentChars: 2_000,
            includeContent: true);
        var trace = CopilotAgentTraceEntry.FromResult(outcome.Execution, result);
        var persistedTrace = JsonConvert.DeserializeObject<CopilotAgentTraceEntry>(
            JsonConvert.SerializeObject(trace));

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Cancelled, result.FailureKind);
        Assert.Equal(CopilotAgentStopReason.Cancelled, result.DelegatedRunUsage?.StopReason);
        Assert.Equal(
            result.DelegatedRunUsage?.RequestTokenBudget,
            result.DelegatedRunUsage?.ConsumedTokens);
        Assert.True(result.DelegatedRunUsage?.UsedEstimatedUsage);
        Assert.True(trace.DelegatedUsageIncludesEstimates);
        Assert.Contains("includes estimates", trace.DiagnosticDetails, StringComparison.Ordinal);
        Assert.True(Assert.IsType<CopilotAgentTraceEntry>(persistedTrace).DelegatedUsageIncludesEstimates);
        Assert.NotEmpty(result.Summary);
        Assert.NotEmpty(result.ErrorMessage);
        Assert.Equal(!interruptMessageEnabled, result.SuppressModelOutput);
        if (interruptMessageEnabled)
        {
            Assert.Contains("stopped by the user", modelOutput, StringComparison.Ordinal);
            Assert.Contains("\"includes_estimates\":true", modelOutput, StringComparison.Ordinal);
            Assert.Contains(tool.Name, recoveryObservations, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(string.Empty, modelOutput);
            Assert.Equal("- None", recoveryObservations);
        }
    }

    [Fact]
    public async Task CooperativeCancellationPreservesReturnedRunDiagnostics()
    {
        var returnedResult = new CopilotSubagentResult
        {
            StopReason = CopilotAgentStopReason.Completed,
            Usage = new CopilotTokenUsage(120, 30, 150, 40),
            Budget = new CopilotAgentBudgetSnapshot
            {
                ConsumedTokens = 140,
                ProviderCalls = 3,
                ToolCalls = 2,
                PeakEstimatedInputTokens = 800,
                ProviderRetryCount = 1,
                ProviderRateLimitRetryCount = 1,
                ProviderRetryDelayMs = 125,
                ProviderStreamInactivityTimeoutCount = 1,
                ProviderResponseCount = 2,
                ProviderFirstResponseLatencyTotalMs = 80,
                ProviderFirstResponseLatencyMaxMs = 50,
                ProviderCallDurationTotalMs = 125,
                ProviderStreamChunkCount = 5,
                ProviderStreamInterChunkLatencyCount = 3,
                ProviderStreamInterChunkLatencyTotalMs = 45,
                ProviderStreamInterChunkLatencyMaxMs = 20,
                ContextRecoveryCount = 1,
                ContextRecoveryEstimatedInputTokensBefore = 900,
                ContextRecoveryEstimatedInputTokensAfter = 600,
                RegisteredToolCount = 6,
                AvailableToolCount = 4,
                AvailableToolDefinitionCharacters = 320,
                HarnessInstructionCharacters = 100,
            },
            DeliveredSteeringCount = 2,
            UndeliveredSteeringCount = 1,
        };
        var runner = new CooperativeCancellationRunner(returnedResult);
        var tool = new CopilotDelegateExploreTool(runner);
        var request = new CopilotAgentRequest
        {
            ConversationId = "cooperative-cancellation-" + Guid.NewGuid().ToString("N"),
            UserText = "Delegate a bounded workspace investigation.",
            TaskIntentText = "Delegate a bounded workspace investigation.",
            Profile = CopilotProfileConfig.CreateDefault(),
        };
        var progress = new CopilotToolProgressContext();
        using var testCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Task<CopilotToolResult> executionTask = tool.ExecuteWithProgressAsync(
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["task"] = "Inspect the bounded workspace evidence.",
                },
            },
            progress,
            testCancellation.Token);
        await runner.Started.Task.WaitAsync(testCancellation.Token);
        string runId = Assert.IsType<CopilotDelegatedRunProgress>(
            progress.LatestSnapshot?.DelegatedRun).RunId;

        Assert.Equal(
            CopilotSubagentCancelResult.Requested,
            CopilotSubagentCoordination.RequestCancelActiveRun(request.ConversationId, runId));
        CopilotToolResult result = await executionTask.WaitAsync(testCancellation.Token);

        var delegated = Assert.IsType<CopilotDelegatedRunUsage>(result.DelegatedRunUsage);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Cancelled, result.FailureKind);
        Assert.Equal(CopilotAgentStopReason.Cancelled, delegated.StopReason);
        Assert.Equal(returnedResult.Usage, delegated.Usage);
        Assert.Equal(150, delegated.ConsumedTokens);
        Assert.Equal(3, delegated.ProviderCalls);
        Assert.Equal(2, delegated.ToolCalls);
        Assert.Equal(800, delegated.PeakEstimatedInputTokens);
        Assert.Equal(1, delegated.ProviderRetryCount);
        Assert.Equal(1, delegated.ProviderRateLimitRetryCount);
        Assert.Equal(125, delegated.ProviderRetryDelayMs);
        Assert.Equal(1, delegated.ProviderStreamInactivityTimeoutCount);
        Assert.Equal(2, delegated.ProviderResponseCount);
        Assert.Equal(80, delegated.ProviderFirstResponseLatencyTotalMs);
        Assert.Equal(50, delegated.ProviderFirstResponseLatencyMaxMs);
        Assert.Equal(125, delegated.ProviderCallDurationTotalMs);
        Assert.Equal(5, delegated.ProviderStreamChunkCount);
        Assert.Equal(3, delegated.ProviderStreamInterChunkLatencyCount);
        Assert.Equal(45, delegated.ProviderStreamInterChunkLatencyTotalMs);
        Assert.Equal(20, delegated.ProviderStreamInterChunkLatencyMaxMs);
        Assert.Equal(1, delegated.ContextRecoveryCount);
        Assert.Equal(900, delegated.ContextRecoveryEstimatedInputTokensBefore);
        Assert.Equal(600, delegated.ContextRecoveryEstimatedInputTokensAfter);
        Assert.Equal(2, delegated.DeliveredSteeringCount);
        Assert.Equal(1, delegated.UndeliveredSteeringCount);
        Assert.Equal(6, delegated.RegisteredToolCount);
        Assert.Equal(4, delegated.AvailableToolCount);
        Assert.Equal(320, delegated.AvailableToolDefinitionCharacters);
        Assert.Equal(100, delegated.HarnessInstructionCharacters);
        Assert.False(delegated.UsedEstimatedUsage);

        var trace = CopilotAgentTraceEntry.FromResult(
            new CopilotToolExecutionInfo
            {
                ToolName = tool.Name,
                State = CopilotToolExecutionState.Cancelled,
                FailureKind = CopilotToolFailureKind.Cancelled,
            },
            result);
        var persistedTrace = Assert.IsType<CopilotAgentTraceEntry>(
            JsonConvert.DeserializeObject<CopilotAgentTraceEntry>(
                JsonConvert.SerializeObject(trace)));

        Assert.Equal(120, trace.DelegatedReportedInputTokens);
        Assert.Equal(30, trace.DelegatedReportedOutputTokens);
        Assert.Equal(150, trace.DelegatedReportedTotalTokens);
        Assert.Equal(40, trace.DelegatedReportedCachedInputTokens);
        Assert.Contains(
            "Child reported usage: 120 input · 30 output · 150 total · 40 cached input",
            trace.DiagnosticDetails,
            StringComparison.Ordinal);
        Assert.Equal(trace.DelegatedReportedInputTokens, persistedTrace.DelegatedReportedInputTokens);
        Assert.Equal(trace.DelegatedReportedOutputTokens, persistedTrace.DelegatedReportedOutputTokens);
        Assert.Equal(trace.DelegatedReportedTotalTokens, persistedTrace.DelegatedReportedTotalTokens);
        Assert.Equal(trace.DelegatedReportedCachedInputTokens, persistedTrace.DelegatedReportedCachedInputTokens);
    }

    [Fact]
    public void PersistedDelegatedUsageNormalizationPreservesTokenInvariants()
    {
        var trace = new CopilotAgentTraceEntry
        {
            SchemaVersion = CopilotAgentTraceEntry.CurrentSchemaVersion - 1,
            DelegatedRunId = "child-run",
            DelegatedConsumedTokens = 40,
            DelegatedReportedInputTokens = 120,
            DelegatedReportedOutputTokens = 30,
            DelegatedReportedTotalTokens = 10,
            DelegatedReportedCachedInputTokens = 999,
        };

        Assert.True(trace.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Equal(CopilotAgentTraceEntry.CurrentSchemaVersion, trace.SchemaVersion);
        Assert.Equal(150, trace.DelegatedReportedTotalTokens);
        Assert.Equal(120, trace.DelegatedReportedCachedInputTokens);
        Assert.Equal(150, trace.DelegatedConsumedTokens);
    }

    private sealed class BlockingSubagentRunner : ICopilotSubagentRunner
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new CopilotSubagentResult();
        }
    }

    private sealed class CooperativeCancellationRunner(CopilotSubagentResult result) : ICopilotSubagentRunner
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            return result;
        }
    }
}
