using ColorVision.Copilot;
using ColorVision.Engine.Templates.Flow;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotFlowExecutionStatisticsTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 14, 15, 30, 0, DateTimeKind.Local);

    [Fact]
    public async Task TodayQueryUsesLocalCalendarRangeAndReturnsStructuredAggregates()
    {
        var source = new FakeStatisticsSource
        {
            Rows =
            [
                new((int)FlowStatus.Completed, 8, 1000),
                new((int)FlowStatus.Failed, 2, 2000),
                new((int)FlowStatus.Ready, 1, 0),
                new(99, 1, 500),
            ],
        };
        var service = new CopilotFlowExecutionStatisticsService(source, () => FixedNow);

        var result = await service.ExecuteAsync(Input("today"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(new DateTime(2026, 7, 14), source.StartInclusive);
        Assert.Equal(new DateTime(2026, 7, 15), source.EndExclusive);
        Assert.Contains("total_attempts: 12", result.Content, StringComparison.Ordinal);
        Assert.Contains("completed: 8", result.Content, StringComparison.Ordinal);
        Assert.Contains("failed: 2", result.Content, StringComparison.Ordinal);
        Assert.Contains("ready: 1", result.Content, StringComparison.Ordinal);
        Assert.Contains("unknown: 1", result.Content, StringComparison.Ordinal);
        Assert.Contains("completed_rate_percent: 66.67", result.Content, StringComparison.Ordinal);
        Assert.Contains("average_duration_ms: 1041.67", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("yesterday", "2026-07-13", "2026-07-14")]
    [InlineData("last7days", "2026-07-08", "2026-07-15")]
    public async Task SupportedPeriodsUseLeftClosedRightOpenLocalRanges(
        string period,
        string expectedStart,
        string expectedEnd)
    {
        var source = new FakeStatisticsSource();
        var service = new CopilotFlowExecutionStatisticsService(source, () => FixedNow);

        var result = await service.ExecuteAsync(Input(period), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(DateTime.Parse(expectedStart), source.StartInclusive);
        Assert.Equal(DateTime.Parse(expectedEnd), source.EndExclusive);
    }

    [Fact]
    public async Task DisconnectedDatabaseReturnsTransientFailureWithoutQuerying()
    {
        var source = new FakeStatisticsSource { IsAvailable = false };
        var service = new CopilotFlowExecutionStatisticsService(source, () => FixedNow);

        var result = await service.ExecuteAsync(Input("today"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Transient, result.FailureKind);
        Assert.Equal(0, source.QueryCount);
        Assert.Contains("not connected", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryFailureDoesNotExposeDatabaseExceptionDetails()
    {
        var source = new FakeStatisticsSource { Error = new InvalidOperationException("password=secret") };
        var service = new CopilotFlowExecutionStatisticsService(source, () => FixedNow);

        var result = await service.ExecuteAsync(Input("today"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Transient, result.FailureKind);
        Assert.DoesNotContain("password", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolSchemaAndRequestIntentKeepStatisticsSurfaceNarrow()
    {
        var tool = new CopilotQueryFlowExecutionStatsTool(new CopilotFlowExecutionStatisticsService(
            new FakeStatisticsSource(), () => FixedNow));
        var registry = new CopilotToolRegistry([tool]);

        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?>
        {
            ["period"] = "today; DROP TABLE t_scgd_measure_batch",
        }, out _, out var periodError));
        Assert.Contains("must be one of", periodError, StringComparison.OrdinalIgnoreCase);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?>
        {
            ["period"] = "today",
            ["sql"] = "SELECT * FROM users",
        }, out _, out var sqlError));
        Assert.Contains("Unknown argument 'sql'", sqlError, StringComparison.Ordinal);

        Assert.Contains(registry.FindTools(Request("查询今天执行了多少次流程")), item => item.Name == tool.Name);
        Assert.Contains(registry.FindTools(Request("最近七天流程成功率")), item => item.Name == tool.Name);
        Assert.Contains(registry.FindTools(Request("如何统计流程执行次数")), item => item.Name == tool.Name);
        Assert.DoesNotContain(registry.FindTools(Request("数据库是什么")), item => item.Name == tool.Name);
        Assert.DoesNotContain(registry.FindTools(new CopilotAgentRequest { UserText = "数据库是什么", Mode = CopilotAgentMode.Chat }), item => item.Name == tool.Name);
    }

    private static CopilotAgentToolInput Input(string period)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["period"] = JsonSerializer.SerializeToElement(period),
            },
        };
    }

    private static CopilotAgentRequest Request(string text)
    {
        return new CopilotAgentRequest { UserText = text, Mode = CopilotAgentMode.Auto };
    }

    private sealed class FakeStatisticsSource : ICopilotFlowExecutionStatisticsSource
    {
        public bool IsAvailable { get; init; } = true;

        public IReadOnlyList<CopilotFlowExecutionStatusCount> Rows { get; init; } = [];

        public Exception? Error { get; init; }

        public int QueryCount { get; private set; }

        public DateTime StartInclusive { get; private set; }

        public DateTime EndExclusive { get; private set; }

        public Task<IReadOnlyList<CopilotFlowExecutionStatusCount>> QueryAsync(
            DateTime startInclusive,
            DateTime endExclusive,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QueryCount++;
            StartInclusive = startInclusive;
            EndExclusive = endExclusive;
            if (Error != null)
                throw Error;
            return Task.FromResult(Rows);
        }
    }
}
