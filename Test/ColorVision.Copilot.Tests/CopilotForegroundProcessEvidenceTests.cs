using ColorVision.Copilot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotForegroundProcessEvidenceTests : IDisposable
{
    private readonly string _root = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        "ColorVisionCopilotForegroundProcessEvidenceTests",
        Guid.NewGuid().ToString("N")));

    [Fact]
    public async Task ForegroundServicesPublishOnlyFixedStructuredProcessOutcomes()
    {
        Directory.CreateDirectory(_root);
        var projectPath = Path.Combine(_root, "Evidence.csproj");
        var dotnetPath = Path.Combine(_root, "dotnet.exe");
        var shellPath = Path.Combine(_root, "powershell.exe");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(dotnetPath, string.Empty);
        File.WriteAllText(shellPath, string.Empty);
        var request = CreateRequest();

        var validationRunner = new SequenceValidationRunner(
            new CopilotWorkspaceValidationProcessResult(
                0,
                false,
                "private validation stdout",
                "private validation stderr",
                TimeSpan.FromMilliseconds(1)),
            new CopilotWorkspaceValidationProcessResult(
                -1,
                true,
                "private timed-out validation stdout",
                "private timed-out validation stderr",
                TimeSpan.FromSeconds(10)));
        var validationService = new CopilotWorkspaceValidationService(
            validationRunner,
            () => dotnetPath);

        var buildResult = await validationService.ExecuteAsync(
            request,
            CreateValidationInput(projectPath, "build"),
            CancellationToken.None);
        var timedOutTestResult = await validationService.ExecuteAsync(
            request,
            CreateValidationInput(projectPath, "test"),
            CancellationToken.None);

        Assert.True(buildResult.Success);
        Assert.Equal("build", buildResult.ProcessOperation);
        Assert.Equal(0, buildResult.ProcessExitCode);
        Assert.False(buildResult.ProcessTimedOut);
        Assert.False(timedOutTestResult.Success);
        Assert.Equal("test", timedOutTestResult.ProcessOperation);
        Assert.Null(timedOutTestResult.ProcessExitCode);
        Assert.True(timedOutTestResult.ProcessTimedOut);

        using var archiveRegistry = new CopilotShellCommandOutputArchiveRegistry();
        var shellRunner = new SequenceShellRunner(
            new CopilotShellProcessResult(
                23,
                false,
                "private shell stdout",
                "private shell stderr",
                TimeSpan.FromMilliseconds(1)),
            new CopilotShellProcessResult(
                -1,
                true,
                "private timed-out shell stdout",
                "private timed-out shell stderr",
                TimeSpan.FromSeconds(10)));
        var shellService = new CopilotShellCommandService(
            shellRunner,
            _ => shellPath,
            archiveRegistry);

        var failedShellResult = await shellService.ExecuteAsync(
            request,
            CreateShellInput("private foreground command"),
            CancellationToken.None);
        var timedOutShellResult = await shellService.ExecuteAsync(
            request,
            CreateShellInput("private timed-out foreground command"),
            CancellationToken.None);

        Assert.False(failedShellResult.Success);
        Assert.Equal("shell", failedShellResult.ProcessOperation);
        Assert.Equal(23, failedShellResult.ProcessExitCode);
        Assert.False(failedShellResult.ProcessTimedOut);
        Assert.False(timedOutShellResult.Success);
        Assert.Equal("shell", timedOutShellResult.ProcessOperation);
        Assert.Null(timedOutShellResult.ProcessExitCode);
        Assert.True(timedOutShellResult.ProcessTimedOut);

        var observation = CopilotToolObservation.FromResult(failedShellResult);
        Assert.Equal("shell", observation.ProcessOperation);
        Assert.Equal(23, observation.ProcessExitCode);
        Assert.False(observation.ProcessTimedOut);
        var contradictoryObservation = CopilotToolObservation.FromResult(new CopilotToolResult
        {
            ToolName = "RunShellCommand",
            Success = false,
            ProcessOperation = "shell",
            ProcessExitCode = 0,
        });
        Assert.Empty(contradictoryObservation.ProcessOperation);
        Assert.Null(contradictoryObservation.ProcessExitCode);
        Assert.False(contradictoryObservation.ProcessTimedOut);

        var trace = CopilotAgentTraceEntry.FromResult(
            new CopilotToolExecutionInfo
            {
                ToolName = "RunShellCommand",
                Access = CopilotToolAccess.Write,
                State = CopilotToolExecutionState.Failed,
                ArgumentSummary = "private foreground command",
            },
            failedShellResult);
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Shell validation finished.")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        assistantMessage.AgentTraceEntries.Add(trace);
        var evidence = CopilotGoalTurnEvidence.Capture(assistantMessage);
        var prompt = CopilotGoalCompletionEvaluator.BuildEvidencePrompt(
            CopilotConversationGoal.Create(
                "Verify the foreground shell outcome",
                new DateTimeOffset(2026, 8, 11, 8, 0, 0, TimeSpan.Zero)),
            Array.Empty<CopilotRequestMessage>(),
            evidence);

        Assert.Contains(
            "process_operation=shell | process_state=exited | exit_code=23",
            prompt,
            StringComparison.Ordinal);
        Assert.DoesNotContain("private foreground command", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("private shell stdout", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("private shell stderr", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceRoundTripKeepsValidOutcomeAndDropsLegacyOrContradictoryMetadata()
    {
        var execution = new CopilotToolExecutionInfo
        {
            ToolName = "RunShellCommand",
            State = CopilotToolExecutionState.Failed,
        };
        var trace = CopilotAgentTraceEntry.FromResult(
            execution,
            new CopilotToolResult
            {
                ToolName = "RunShellCommand",
                Success = false,
                Summary = "private shell summary",
                FailureKind = CopilotToolFailureKind.Unspecified,
                FailureCode = CopilotShellCommandService.NonzeroExitFailureCode,
                ProcessOperation = "shell",
                ProcessExitCode = 23,
            });

        Assert.Equal(CopilotAgentTraceEntry.CurrentSchemaVersion, trace.SchemaVersion);
        Assert.Equal("shell", trace.ProcessOperation);
        Assert.Equal(23, trace.ProcessExitCode);
        Assert.False(trace.ProcessTimedOut);

        var serialized = JsonConvert.SerializeObject(trace);
        var restored = JsonConvert.DeserializeObject<CopilotAgentTraceEntry>(serialized);
        Assert.NotNull(restored);
        Assert.False(restored.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Equal("shell", restored.ProcessOperation);
        Assert.Equal(23, restored.ProcessExitCode);

        var legacyDocument = JObject.Parse(serialized);
        legacyDocument[nameof(CopilotAgentTraceEntry.SchemaVersion)] =
            CopilotAgentTraceEntry.CurrentSchemaVersion - 1;
        legacyDocument.Remove(nameof(CopilotAgentTraceEntry.ProcessOperation));
        legacyDocument.Remove(nameof(CopilotAgentTraceEntry.ProcessExitCode));
        legacyDocument.Remove(nameof(CopilotAgentTraceEntry.ProcessTimedOut));
        var legacy = legacyDocument.ToObject<CopilotAgentTraceEntry>();
        Assert.NotNull(legacy);
        Assert.True(legacy.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Equal(CopilotAgentTraceEntry.CurrentSchemaVersion, legacy.SchemaVersion);
        Assert.Empty(legacy.ProcessOperation);
        Assert.Null(legacy.ProcessExitCode);
        Assert.False(legacy.ProcessTimedOut);

        var contradictory = new CopilotAgentTraceEntry
        {
            ToolName = "RunShellCommand",
            State = CopilotToolExecutionState.Completed,
            ProcessOperation = "shell",
            ProcessExitCode = 23,
        };
        Assert.True(contradictory.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Empty(contradictory.ProcessOperation);
        Assert.Null(contradictory.ProcessExitCode);
        Assert.False(contradictory.ProcessTimedOut);

        var foreignTool = new CopilotAgentTraceEntry
        {
            ToolName = "ReadLocalFile",
            State = CopilotToolExecutionState.Completed,
            ProcessOperation = "shell",
            ProcessExitCode = 0,
        };
        Assert.True(foreignTool.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Empty(foreignTool.ProcessOperation);
        Assert.Null(foreignTool.ProcessExitCode);
        Assert.False(foreignTool.ProcessTimedOut);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private CopilotAgentRequest CreateRequest() => new()
    {
        ConversationId = "foreground-process-evidence",
        WritableLocalRootPaths = [_root],
        SearchRootPaths = [_root],
        PreferredShell = CopilotShellKind.PowerShell,
    };

    private static CopilotAgentToolInput CreateValidationInput(string projectPath, string task) => new()
    {
        Path = projectPath,
        Arguments = new Dictionary<string, object?>
        {
            ["task"] = task,
            ["timeoutSeconds"] = 10,
        },
    };

    private CopilotAgentToolInput CreateShellInput(string command) => new()
    {
        Arguments = new Dictionary<string, object?>
        {
            ["command"] = command,
            ["workingDirectory"] = _root,
            ["shell"] = "powershell",
            ["timeoutSeconds"] = 10,
        },
    };

    private sealed class SequenceValidationRunner : ICopilotWorkspaceValidationRunner
    {
        private readonly Queue<CopilotWorkspaceValidationProcessResult> _results;

        public SequenceValidationRunner(params CopilotWorkspaceValidationProcessResult[] results)
        {
            _results = new Queue<CopilotWorkspaceValidationProcessResult>(results);
        }

        public Task<CopilotWorkspaceValidationProcessResult> RunAsync(
            CopilotWorkspaceValidationCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class SequenceShellRunner : ICopilotShellProcessRunner
    {
        private readonly Queue<CopilotShellProcessResult> _results;

        public SequenceShellRunner(params CopilotShellProcessResult[] results)
        {
            _results = new Queue<CopilotShellProcessResult>(results);
        }

        public Task<CopilotShellProcessResult> RunAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_results.Dequeue());
        }
    }
}
