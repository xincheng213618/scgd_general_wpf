using System.Collections.Concurrent;
using System.IO;
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolExecutionProgressTests
{
    [Fact]
    public void ProgressContextBoundsUpdatesAndIgnoresReportsAfterCompletion()
    {
        var progress = new CopilotToolProgressContext();
        progress.Report(new CopilotToolProgressUpdate
        {
            Message = "phase\r\n" + new string('x', 300),
            Completed = 20,
            Total = 10,
            Unit = new string('u', 40),
        });

        var accepted = Assert.IsType<CopilotToolProgressUpdate>(progress.LatestSnapshot);
        Assert.DoesNotContain('\r', accepted.Message);
        Assert.DoesNotContain('\n', accepted.Message);
        Assert.True(accepted.Message.Length <= 243);
        Assert.Equal(10, accepted.Completed);
        Assert.Equal(10, accepted.Total);
        Assert.Equal(24, accepted.Unit.Length);

        progress.Complete();
        progress.Report("late update", completed: 1, total: 1);

        Assert.Same(accepted, progress.LatestSnapshot);
    }

    [Fact]
    public async Task ProgressContextCoalescesDuplicatesAndCompletionUnblocksWaiters()
    {
        var progress = new CopilotToolProgressContext();
        progress.Report("phase", completed: 1, total: 2, unit: "items");
        progress.Report("phase", completed: 1, total: 2, unit: "items");

        Assert.Equal(
            CopilotToolProgressWaitResult.Updated,
            await progress.WaitForUpdateAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None));
        Assert.Equal(
            CopilotToolProgressWaitResult.TimedOut,
            await progress.WaitForUpdateAsync(TimeSpan.FromMilliseconds(20), CancellationToken.None));

        var completionWait = progress.WaitForUpdateAsync(
            TimeSpan.FromSeconds(1),
            CancellationToken.None).AsTask();
        progress.Complete();

        Assert.Equal(
            CopilotToolProgressWaitResult.Completed,
            await completionWait.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task ReportedToolProgressPublishesWithoutWaitingForHeartbeat()
    {
        var tool = new ReportingTool();
        var executor = new CopilotToolExecutor(
            hooks: null,
            utcNow: null,
            hookPhaseTimeout: null,
            progressInterval: TimeSpan.FromSeconds(5));
        var reported = new TaskCompletionSource<CopilotAgentEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executionTask = executor.ExecuteAsync(
            CreateInvocation(tool, "reported-progress-call"),
            agentEvent =>
            {
                if (agentEvent.Type == CopilotAgentEventType.ToolProgress
                    && agentEvent.Progress != null)
                {
                    reported.TrySetResult(agentEvent);
                }
            },
            CancellationToken.None);

        try
        {
            var progressEvent = await reported.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(CopilotToolExecutionState.Running, progressEvent.ToolExecution?.State);
            Assert.Equal("Converting approved images", progressEvent.Progress!.Message);
            Assert.Equal(3, progressEvent.Progress.Completed);
            Assert.Equal(10, progressEvent.Progress.Total);
            Assert.Equal("files", progressEvent.Progress.Unit);
            Assert.Contains("3/10 files", progressEvent.Text, StringComparison.Ordinal);
        }
        finally
        {
            tool.Release.TrySetResult();
            await executionTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task RapidProgressUpdatesAreCoalescedBeforePublishing()
    {
        var tool = new BurstReportingTool();
        var executor = new CopilotToolExecutor(
            hooks: null,
            utcNow: null,
            hookPhaseTimeout: null,
            progressInterval: TimeSpan.FromSeconds(5));
        var progressEvents = new ConcurrentQueue<CopilotAgentEvent>();
        var firstProgress = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var finalProgress = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executionTask = executor.ExecuteAsync(
            CreateInvocation(tool, "burst-progress-call"),
            agentEvent =>
            {
                if (agentEvent.Type != CopilotAgentEventType.ToolProgress
                    || agentEvent.Progress == null)
                {
                    return;
                }

                progressEvents.Enqueue(agentEvent);
                firstProgress.TrySetResult();
                if (agentEvent.Progress.Completed == BurstReportingTool.UpdateCount)
                    finalProgress.TrySetResult();
            },
            CancellationToken.None);

        try
        {
            await firstProgress.Task.WaitAsync(TimeSpan.FromSeconds(1));
            tool.ContinueBurst.TrySetResult();
            await tool.BurstCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await finalProgress.Task.WaitAsync(TimeSpan.FromSeconds(1));
            await Task.Delay(TimeSpan.FromMilliseconds(350));

            var published = progressEvents.ToArray();
            Assert.InRange(published.Length, 2, 3);
            Assert.Equal(
                BurstReportingTool.UpdateCount,
                published[^1].Progress?.Completed);
        }
        finally
        {
            tool.Release.TrySetResult();
            await executionTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task ApprovedShellOutputFlowsThroughStructuredProgress()
    {
        var runner = new ReportingShellRunner();
        var executablePath = Environment.ProcessPath
            ?? typeof(CopilotToolExecutionProgressTests).Assembly.Location;
        var tool = new CopilotShellCommandTool(
            new CopilotShellCommandService(runner, _ => executablePath));
        var executor = new CopilotToolExecutor(
            hooks: null,
            utcNow: null,
            hookPhaseTimeout: null,
            progressInterval: TimeSpan.FromMilliseconds(20));
        var reported = new TaskCompletionSource<CopilotAgentEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executionTask = executor.ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "shell-progress-call",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = tool,
                ToolInput = new CopilotAgentToolInput
                {
                    Arguments = new Dictionary<string, object?>
                    {
                        ["command"] = "Write-Output ready",
                        ["shell"] = "powershell",
                    },
                },
                AgentRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Auto,
                    UserText = "run command: Write-Output ready",
                },
                FrameworkApprovalGranted = true,
            },
            agentEvent =>
            {
                if (agentEvent.Type == CopilotAgentEventType.ToolProgress
                    && agentEvent.Progress?.Message.Contains("compiling sample.cs", StringComparison.Ordinal) == true)
                {
                    reported.TrySetResult(agentEvent);
                }
            },
            CancellationToken.None);

        try
        {
            await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var progressEvent = await reported.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(CopilotToolExecutionState.Running, progressEvent.ToolExecution?.State);
            Assert.Equal(
                "PowerShell 输出: compiling sample.cs token=<redacted>",
                progressEvent.Progress!.Message);
            Assert.DoesNotContain('\0', progressEvent.Progress.Message);
        }
        finally
        {
            runner.Release.TrySetResult();
            var outcome = await executionTask.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(CopilotToolExecutionState.Completed, outcome.Execution.State);
        }
    }

    [Fact]
    public async Task BoundedProcessReaderPublishesCapturedChunks()
    {
        const string content = "first line\r\nsecond line";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var chunks = new List<string>();

        var captured = await CopilotProcessExecutionSupport.ReadBoundedAsync(
            reader,
            maxCharacters: 100,
            headCharacters: 50,
            truncationMarker: "<truncated>",
            cancellationToken: CancellationToken.None,
            onChunk: chunks.Add);

        Assert.Equal(content, captured);
        Assert.Equal(content, string.Concat(chunks));
    }

    [Fact]
    public async Task ApprovedWorkspaceValidationPublishesOutputAndRedactsFinalEvidence()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "ColorVisionCopilotValidationProgressTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspaceRoot);
        var projectPath = Path.Combine(workspaceRoot, "Sample.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var runner = new ReportingValidationRunner();
        var executablePath = Environment.ProcessPath
            ?? typeof(CopilotToolExecutionProgressTests).Assembly.Location;
        var tool = new CopilotWorkspaceValidationTool(
            new CopilotWorkspaceValidationService(runner, () => executablePath));
        var executor = new CopilotToolExecutor(
            hooks: null,
            utcNow: null,
            hookPhaseTimeout: null,
            progressInterval: TimeSpan.FromMilliseconds(20));
        var reported = new TaskCompletionSource<CopilotAgentEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executionTask = executor.ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "validation-progress-call",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = tool,
                ToolInput = new CopilotAgentToolInput
                {
                    Path = projectPath,
                    Arguments = new Dictionary<string, object?>
                    {
                        ["task"] = "build",
                        ["configuration"] = "Debug",
                    },
                },
                AgentRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Auto,
                    UserText = "build the project",
                    WritableLocalRootPaths = [workspaceRoot],
                },
                FrameworkApprovalGranted = true,
            },
            agentEvent =>
            {
                if (agentEvent.Type == CopilotAgentEventType.ToolProgress
                    && agentEvent.Progress?.Message.Contains("Build succeeded.", StringComparison.Ordinal) == true)
                {
                    reported.TrySetResult(agentEvent);
                }
            },
            CancellationToken.None);

        try
        {
            await runner.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
            var progressEvent = await reported.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Equal("dotnet build 输出: Build succeeded.", progressEvent.Progress!.Message);

            runner.Release.TrySetResult();
            var outcome = await executionTask.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(CopilotToolExecutionState.Completed, outcome.Execution.State);
            Assert.Contains("token=<redacted>", outcome.Result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("validation-secret", outcome.Result.Content, StringComparison.Ordinal);
        }
        finally
        {
            runner.Release.TrySetResult();
            try
            {
                await executionTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task WaitingForSharedResourcePublishesPendingProgressBeforeStart()
    {
        using var firstTool = new ResourceTool(block: true);
        using var secondTool = new ResourceTool(block: false);
        var executor = new CopilotToolExecutor(
            hooks: null,
            utcNow: null,
            hookPhaseTimeout: null,
            progressInterval: TimeSpan.FromMilliseconds(20));
        var firstTask = executor.ExecuteAsync(
            CreateInvocation(firstTool, "first-call"),
            _ => { },
            CancellationToken.None);
        Assert.True(firstTool.Started.Wait(TimeSpan.FromSeconds(1)));

        var events = new ConcurrentQueue<CopilotAgentEvent>();
        var pendingProgress = new TaskCompletionSource<CopilotAgentEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondTask = executor.ExecuteAsync(
            CreateInvocation(secondTool, "second-call"),
            agentEvent =>
            {
                events.Enqueue(agentEvent);
                if (agentEvent.Type == CopilotAgentEventType.ToolProgress
                    && agentEvent.ToolExecution?.State == CopilotToolExecutionState.Pending)
                {
                    pendingProgress.TrySetResult(agentEvent);
                }
            },
            CancellationToken.None);

        try
        {
            var queuedEvent = await pendingProgress.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Contains("waiting for an execution slot", queuedEvent.Text, StringComparison.Ordinal);
            Assert.True(queuedEvent.ToolExecution!.QueueDurationMs > 0);
            Assert.False(secondTool.Started.IsSet);

            firstTool.Release.Set();
            await firstTask.WaitAsync(TimeSpan.FromSeconds(2));
            var secondOutcome = await secondTask.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.True(secondTool.Started.IsSet);
            Assert.Equal(CopilotToolExecutionState.Completed, secondOutcome.Execution.State);
            Assert.True(secondOutcome.Execution.QueueDurationMs > 0);
            Assert.True(events
                .Select((agentEvent, index) => (agentEvent, index))
                .Where(item => item.agentEvent.Type == CopilotAgentEventType.ToolProgress
                    && item.agentEvent.ToolExecution?.State == CopilotToolExecutionState.Pending)
                .Select(item => item.index)
                .First()
                < events
                    .Select((agentEvent, index) => (agentEvent, index))
                    .Where(item => item.agentEvent.Type == CopilotAgentEventType.ToolStarted)
                    .Select(item => item.index)
                    .First());
        }
        finally
        {
            firstTool.Release.Set();
            try
            {
                await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
        }
    }

    private static CopilotToolInvocation CreateInvocation(ICopilotTool tool, string callId)
    {
        return new CopilotToolInvocation
        {
            CallId = callId,
            Round = 1,
            Attempt = 1,
            MaxAttempts = 1,
            RuntimeName = "test",
            Tool = tool,
            AgentRequest = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Auto,
                UserText = "exercise tool queue progress",
            },
        };
    }

    private sealed class ResourceTool(bool block) : ICopilotTool, IDisposable
    {
        public ManualResetEventSlim Started { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public string Name => "ResourceTool";

        public string Description => "Uses one shared resource for queue-progress testing.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(TimeSpan.FromSeconds(5));

        public bool CanHandle(CopilotAgentRequest request) => true;

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) =>
            "resource:shared-test";

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            Started.Set();
            if (block)
                Release.Wait(cancellationToken);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "completed",
            });
        }

        public void Dispose()
        {
            Release.Set();
            Started.Dispose();
            Release.Dispose();
        }
    }

    private sealed class ReportingTool : ICopilotProgressReportingTool
    {
        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "ReportingTool";

        public string Description => "Reports structured progress for executor testing.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(TimeSpan.FromSeconds(5));

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The progress-aware execution path was not used.");
        }

        public async Task<CopilotToolResult> ExecuteWithProgressAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken)
        {
            progress.Report("Converting approved images", completed: 3, total: 10, unit: "files");
            await Release.Task.WaitAsync(cancellationToken);
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "completed",
            };
        }
    }

    private sealed class BurstReportingTool : ICopilotProgressReportingTool
    {
        public const int UpdateCount = 1_000;

        public TaskCompletionSource ContinueBurst { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource BurstCompleted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "BurstReportingTool";

        public string Description => "Emits high-frequency progress updates for coalescing tests.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(TimeSpan.FromSeconds(5));

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The progress-aware execution path was not used.");
        }

        public async Task<CopilotToolResult> ExecuteWithProgressAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken)
        {
            progress.Report("step 1", completed: 1, total: UpdateCount, unit: "items");
            await ContinueBurst.Task.WaitAsync(cancellationToken);
            for (var index = 2; index <= UpdateCount; index++)
                progress.Report($"step {index}", completed: index, total: UpdateCount, unit: "items");
            BurstCompleted.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "completed",
            };
        }
    }

    private sealed class ReportingShellRunner : ICopilotShellProcessRunner
    {
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CopilotShellProcessResult> RunAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            command.StandardOutputReceived?.Invoke(
                "restore complete\r\ncompiling sample.cs token=super-secret\0");
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new CopilotShellProcessResult(
                ExitCode: 0,
                TimedOut: false,
                StandardOutput: "ready",
                StandardError: string.Empty,
                Duration: TimeSpan.FromMilliseconds(100));
        }
    }

    private sealed class ReportingValidationRunner : ICopilotWorkspaceValidationRunner
    {
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<CopilotWorkspaceValidationProcessResult> RunAsync(
            CopilotWorkspaceValidationCommand command,
            CancellationToken cancellationToken)
        {
            command.StandardOutputReceived?.Invoke("Determining projects to restore...\r\nBuild succeeded.");
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new CopilotWorkspaceValidationProcessResult(
                ExitCode: 0,
                TimedOut: false,
                StandardOutput: "Build succeeded.\r\ntoken=validation-secret",
                StandardError: string.Empty,
                Duration: TimeSpan.FromMilliseconds(100));
        }
    }
}
