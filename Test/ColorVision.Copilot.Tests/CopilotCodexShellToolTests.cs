using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexShellToolTests
{
    [Fact]
    public async Task DisabledSnapshotHidesShellStartsAndRejectsInjectedCallsBeforeApproval()
    {
        var shellTool = new CopilotShellCommandTool();
        var backgroundTool = new CopilotStartBackgroundShellCommandTool();
        var fixedDiagnostic = new CopilotInspectWindowsSystemTool();
        var request = new CopilotAgentRequest
        {
            Profile = CopilotProfileConfig.CreateDefault(),
            Mode = CopilotAgentMode.Code,
            UserText = "Run a PowerShell command to print the current directory.",
            TaskIntentText = "Run a PowerShell command to print the current directory.",
            CodexShellToolEnabled = false,
        };
        var registry = new CopilotToolRegistry([shellTool, backgroundTool, fixedDiagnostic]);

        var availableTools = registry.FindTools(request);
        var outcome = await new CopilotToolExecutor(Array.Empty<ICopilotToolExecutionHook>()).ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "stale-shell-call",
                Round = 1,
                RuntimeName = "codex-shell-tool-test",
                Tool = shellTool,
                AgentRequest = request,
                ToolInput = new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?>
                    {
                        ["command"] = "Get-Location",
                    },
                },
            },
            _ => { },
            CancellationToken.None);
        string prompt = new CopilotAgentContextBuilder().BuildPreparedUserMessageContent(
            request,
            Array.Empty<CopilotToolResult>());

        Assert.DoesNotContain(availableTools, tool => tool is CopilotShellCommandTool);
        Assert.False(CopilotToolRegistry.IsAllowedForCodexShellToolPolicy(shellTool, request));
        Assert.False(CopilotToolRegistry.IsAllowedForCodexShellToolPolicy(backgroundTool, request));
        Assert.True(CopilotToolRegistry.IsAllowedForCodexShellToolPolicy(fixedDiagnostic, request));
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.Equal(CopilotToolFailureKind.Authorization, outcome.Result.FailureKind);
        Assert.Equal("codex_shell_tool_disabled", outcome.Result.FailureCode);
        Assert.Contains("features.shell_tool=false applies", prompt, StringComparison.Ordinal);
        Assert.Contains("do not claim that a command or script was executed", prompt, StringComparison.Ordinal);
    }
}
