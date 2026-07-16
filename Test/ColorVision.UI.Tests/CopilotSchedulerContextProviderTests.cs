using ColorVision.Scheduler;
using ColorVision.UI;

namespace ColorVision.UI.Tests;

public sealed class CopilotSchedulerContextProviderTests
{
    [Fact]
    public async Task SchedulerProvider_CapturesFreshSnapshotForRelevantRequests()
    {
        var taskCount = 3;
        var captureCount = 0;
        var provider = new CopilotSchedulerContextProvider(_ =>
        {
            captureCount++;
            return Task.FromResult<CopilotSchedulerContextSnapshot?>(CreateSnapshot(taskCount));
        });

        var first = await provider.CaptureAsync(CreateRequest("检查计划任务状态"), CancellationToken.None);
        taskCount = 7;
        var second = await provider.CaptureAsync(CreateRequest("show scheduled job failures"), CancellationToken.None);

        Assert.Equal(2, captureCount);
        Assert.Contains("Registered tasks: 3", Assert.IsType<CopilotContextItem>(first).Content, StringComparison.Ordinal);
        Assert.Contains("Registered tasks: 7", Assert.IsType<CopilotContextItem>(second).Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SchedulerProvider_SkipsUnrelatedTurnsButSupportsCurrentSurfaceAndDiagnose()
    {
        var captureCount = 0;
        var isCurrentSurface = false;
        var provider = new CopilotSchedulerContextProvider(
            _ =>
            {
                captureCount++;
                return Task.FromResult<CopilotSchedulerContextSnapshot?>(CreateSnapshot(3));
            },
            isCurrentSurface: () => isCurrentSurface);

        Assert.Null(await provider.CaptureAsync(CreateRequest("解释这张图片"), CancellationToken.None));
        Assert.Equal(0, captureCount);

        isCurrentSurface = true;
        Assert.NotNull(await provider.CaptureAsync(CreateRequest("这个状态怎么样？"), CancellationToken.None));
        isCurrentSurface = false;
        Assert.NotNull(await provider.CaptureAsync(CreateRequest("继续诊断", CopilotContextScope.Diagnose), CancellationToken.None));
        Assert.Equal(2, captureCount);
    }

    [Fact]
    public async Task SchedulerProvider_DropsSnapshotWhenSourceBecomesInactiveDuringCapture()
    {
        var active = true;
        var provider = new CopilotSchedulerContextProvider(
            _ =>
            {
                active = false;
                return Task.FromResult<CopilotSchedulerContextSnapshot?>(CreateSnapshot(3));
            },
            () => active);

        var result = await provider.CaptureAsync(CreateRequest("查看调度器"), CancellationToken.None);

        Assert.Null(result);
        Assert.False(provider.CanProvide(CopilotContextScope.Agent));
    }

    [Fact]
    public void SchedulerExtension_UsesStableReadOnlyContextMetadata()
    {
        var registry = new CopilotAgentExtensionRegistry();
        var provider = new CopilotSchedulerContextProvider(_ => Task.FromResult<CopilotSchedulerContextSnapshot?>(null));

        using (CopilotSchedulerAgentExtension.Register(registry, provider, "4.2.0"))
        {
            var extension = Assert.Single(registry.GetSnapshot().Extensions);
            Assert.Equal(CopilotSchedulerAgentExtension.SourceId, extension.SourceId);
            Assert.Equal("Task Scheduler", extension.SourceName);
            Assert.Equal("4.2.0", extension.SourceVersion);
            Assert.Same(provider, Assert.Single(extension.ContextProviders));
            Assert.Empty(extension.Tools);
        }

        Assert.Empty(registry.GetSnapshot().Extensions);
    }

    [Fact]
    public void SchedulerBuilder_ReportsRuntimeAndSelectedTaskWithoutConfigurationOrMessages()
    {
        var item = CopilotBusinessContextBuilder.BuildSchedulerContextItem(CreateSnapshot(3));

        Assert.Contains("Scheduler state: Started", item.Content, StringComparison.Ordinal);
        Assert.Contains("Running tasks: 1", item.Content, StringComparison.Ordinal);
        Assert.Contains("Bounded task overview (running and failed tasks first)", item.Content, StringComparison.Ordinal);
        Assert.Contains("runs 8; successes 6; failures 2", item.Content, StringComparison.Ordinal);
        Assert.Contains("Task name: Nightly password=<redacted>", item.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Custom configuration: Present (values withheld)", item.Content, StringComparison.Ordinal);
        Assert.Contains("Cron schedule: Configured (expression withheld)", item.Content, StringComparison.Ordinal);
        Assert.Contains("Last result message: Present (content withheld)", item.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("task-secret", item.Content, StringComparison.Ordinal);
        Assert.Contains("Cron expressions, job data, raw result or exception messages, payloads, paths, and credentials are withheld", item.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SchedulerBuilder_ReportsHistoryShapeAndWithholdsRecordDetail()
    {
        var snapshot = new CopilotSchedulerContextSnapshot
        {
            Surface = "Scheduled task execution history",
            SchedulerState = "Started",
            TotalTaskCount = 3,
            TotalRunCount = 12,
            TotalSuccessCount = 9,
            TotalFailureCount = 3,
            HasLoadedHistory = true,
            HistoryScope = "Selected scheduled task",
            HistoryTaskName = "Nightly",
            HistoryGroupName = "Inspection",
            HistoryPageIndex = 2,
            HistoryFilter = "Failure only",
            LoadedHistoryCount = 4,
            LoadedHistorySuccessCount = 0,
            LoadedHistoryFailureCount = 4,
            LoadedHistoryAverageExecutionTimeMilliseconds = 950,
            HasSelectedHistoryRecord = true,
            SelectedHistoryRecordId = 42,
            SelectedHistoryTaskName = "AOI token=history-secret",
            SelectedHistoryGroupName = "Inspection",
            SelectedHistoryStartTime = "2026-07-16T10:00:00.0000000+08:00",
            SelectedHistoryEndTime = "2026-07-16T10:00:01.0000000+08:00",
            SelectedHistoryExecutionTimeMilliseconds = 1000,
            SelectedHistorySucceeded = false,
            SelectedHistoryResult = "Failed",
            SelectedHistoryHasMessage = true,
        };

        var item = CopilotBusinessContextBuilder.BuildSchedulerContextItem(snapshot);

        Assert.Contains("Rows in current page: 4", item.Content, StringComparison.Ordinal);
        Assert.Contains("Failed rows: 4", item.Content, StringComparison.Ordinal);
        Assert.Contains("Internal record id: 42", item.Content, StringComparison.Ordinal);
        Assert.Contains("Detail message: Present (content withheld)", item.Content, StringComparison.Ordinal);
        Assert.Contains("token=<redacted>", item.Content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("history-secret", item.Content, StringComparison.Ordinal);
    }

    private static CopilotContextRequest CreateRequest(string userText, CopilotContextScope scope = CopilotContextScope.Agent)
    {
        return new CopilotContextRequest { Scope = scope, UserText = userText };
    }

    private static CopilotSchedulerContextSnapshot CreateSnapshot(int taskCount)
    {
        return new CopilotSchedulerContextSnapshot
        {
            Surface = "Scheduled task viewer",
            SchedulerState = "Started",
            TotalTaskCount = taskCount,
            ReadyTaskCount = 1,
            RunningTaskCount = 1,
            PausedTaskCount = 1,
            TotalRunCount = 12,
            TotalSuccessCount = 9,
            TotalFailureCount = 3,
            Tasks =
            [
                new CopilotSchedulerTaskContextSnapshot
                {
                    TaskName = "Nightly password=task-secret",
                    GroupName = "Inspection",
                    Status = "Running",
                    JobType = "FlowJob",
                    ExecutionMode = "Cron",
                    Priority = 5,
                    RunCount = 8,
                    SuccessCount = 6,
                    FailureCount = 2,
                    LastExecutionTimeMilliseconds = 1200,
                    LastExecutionResult = "Failed",
                    HasLastExecutionMessage = true,
                    NextFireTime = "2026/07/17 01:00:00",
                },
            ],
            SelectedTaskCount = 1,
            HasSelectedTask = true,
            SelectedTaskName = "Nightly password=task-secret",
            SelectedGroupName = "Inspection",
            SelectedTaskStatus = "Running",
            SelectedJobType = "FlowJob",
            SelectedExecutionMode = "Cron",
            SelectedRepeatMode = "Forever",
            SelectedPriority = 5,
            SelectedTimeoutSeconds = 60,
            SelectedRunCount = 8,
            SelectedSuccessCount = 6,
            SelectedFailureCount = 2,
            SelectedLastExecutionTimeMilliseconds = 1200,
            SelectedAverageExecutionTimeMilliseconds = 900,
            SelectedLastExecutionResult = "Failed",
            SelectedHasLastExecutionMessage = true,
            SelectedNextFireTime = "2026/07/17 01:00:00",
            SelectedPreviousFireTime = "2026/07/16 01:00:00",
            SelectedCreatedAt = "2026-07-10T08:00:00.0000000+08:00",
            SelectedHasConfiguration = true,
            SelectedHasCronExpression = true,
        };
    }
}
