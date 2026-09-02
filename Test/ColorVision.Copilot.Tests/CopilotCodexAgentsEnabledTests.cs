using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexAgentsEnabledTests
{
    [Fact]
    public async Task ConfiguredConcurrencyLimitQueuesTheNextSubagentLease()
    {
        var coordinator = new CopilotSubagentCoordinator(new CopilotAgentRequest
        {
            CodexMaximumConcurrentSubagentRuns = 1,
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var first = await coordinator.TryAcquireAsync("explore", cancellation.Token);
        Assert.NotNull(first);

        var secondTask = coordinator.TryAcquireAsync("scout", cancellation.Token);
        Assert.False(secondTask.IsCompleted);

        first.Dispose();
        using var second = await secondTask;
        Assert.NotNull(second);
    }

    [Fact]
    public async Task DisabledSnapshotHidesDelegationToolsAndRejectsInjectedCalls()
    {
        var runner = new RecordingSubagentRunner();
        var delegateTool = new CopilotDelegateExploreTool(runner);
        var registry = new CopilotToolRegistry(
            CopilotToolRegistry.CreateCoreDefaultTools().Append(delegateTool));
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = "Use only DelegateExplore; do not use parent agent file tools.",
            TaskIntentText = "Use only DelegateExplore; do not use parent agent file tools.",
            CodexAgentsEnabled = false,
        };

        var availableTools = registry.FindTools(request);
        var outcome = await new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>()).ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "stale-delegate-call",
                Round = 1,
                RuntimeName = "codex-agents-test",
                Tool = delegateTool,
                AgentRequest = request,
                ToolInput = new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?>
                    {
                        ["task"] = "Inspect the workspace.",
                    },
                },
            },
            _ => { },
            CancellationToken.None);
        var directResult = await delegateTool.ExecuteAsync(
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["task"] = "Inspect the workspace.",
                },
            },
            CancellationToken.None);

        Assert.DoesNotContain(availableTools, tool => tool is CopilotDelegateSubagentTool);
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.Equal(CopilotToolFailureKind.Authorization, outcome.Result.FailureKind);
        Assert.Equal("codex_agents_disabled", outcome.Result.FailureCode);
        Assert.False(directResult.Success);
        Assert.Equal("codex_agents_disabled", directResult.FailureCode);
        Assert.Equal(0, runner.RunCount);
    }

    private sealed class RecordingSubagentRunner : ICopilotSubagentRunner
    {
        public int RunCount { get; private set; }

        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            RunCount++;
            return Task.FromResult(new CopilotSubagentResult());
        }
    }
}
