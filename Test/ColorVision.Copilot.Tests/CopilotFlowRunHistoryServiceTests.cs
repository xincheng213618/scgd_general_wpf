using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Services.FlowDiagnostics;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunHistoryServiceTests
{
    private const string HashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string HashB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string NodeId = "recorded-node-identifier";
    private static readonly DateTime Started = new(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReferencesUseOnlyExplicitSuccessfulSameRecordedContentAndKeepWarningsVisible()
    {
        var legacyOutcome = Run(7, 200);
        legacyOutcome.FinalOutcome = null;
        var unknownOutcome = Run(3, 200);
        unknownOutcome.FinalOutcome = (FlowFinalOutcome)99;
        var incomplete = Run(2, 200);
        incomplete.CompletedTimeUtc = null;
        var recovered = Run(1, 200);
        recovered.RecoveredTimeUtc = Started;
        var source = Result(Run(10, 4000), [Run(9, 1000), Run(8, 3000, outcome: FlowFinalOutcome.SucceededWithWarnings),
            legacyOutcome, Run(6, 20, FlowStatus.Failed, FlowFinalOutcome.Failed), Run(5, 200, hash: HashB),
            Run(4, 200, FlowStatus.Completed, FlowFinalOutcome.Failed), unknownOutcome, incomplete, recovered]);
        var result = await Service(source).InspectAsync(10, null, 10, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.Equal(9, root.GetProperty("previous_runs").GetArrayLength());
        var reference = root.GetProperty("references").GetProperty("run_duration");
        Assert.Equal(new[] { 9, 8 }, reference.GetProperty("run_record_ids").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(2, reference.GetProperty("sample_count").GetInt32());
        Assert.Equal(1, reference.GetProperty("warning_sample_count").GetInt32());
        Assert.Equal(2000, reference.GetProperty("mean_ms").GetDouble());
        Assert.Equal(2000, reference.GetProperty("median_ms").GetDouble());
        Assert.Equal(1000, reference.GetProperty("minimum_ms").GetDouble());
        Assert.Equal(3000, reference.GetProperty("maximum_ms").GetDouble());
        Assert.Equal(2000, reference.GetProperty("target_minus_median_ms").GetDouble());
        Assert.Equal(2, reference.GetProperty("target_to_median_ratio").GetDouble());
        Assert.DoesNotContain("PRIVATE_", result.Content, StringComparison.Ordinal);
        Assert.Contains("customer PASS/FAIL", root.GetProperty("provenance").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("legacy_template_id")]
    [InlineData("unavailable")]
    public async Task LegacyAndMissingIdentityRemainEvidenceWithoutBecomingConfirmedReferences(string basis)
    {
        var source = Result(Run(10, 4000), basis == "unavailable" ? [] : [Run(9, 1000)]) with { IdentityBasis = basis };
        var result = await Service(source).InspectAsync(10, null, 10, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotEmpty(result.PartialResultMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        var reference = root.GetProperty("references").GetProperty("run_duration");
        Assert.Equal(0, reference.GetProperty("sample_count").GetInt32());
        Assert.Equal(JsonValueKind.Null, reference.GetProperty("mean_ms").ValueKind);
        Assert.Equal(JsonValueKind.Null, reference.GetProperty("target_to_median_ratio").ValueKind);
        Assert.False(root.GetProperty("coverage").GetProperty("returned_window_complete").GetBoolean());
        Assert.Equal(basis != "unavailable", root.GetProperty("coverage").GetProperty("history_available").GetBoolean());
    }

    [Fact]
    public async Task NodeDurationsSumCompleteSuccessfulRepeatedAttemptsPerRunWithoutDeduplicatingAttemptNumbers()
    {
        var source = Result(Run(10, 4000), [Run(9, 1000), Run(8, 2000), Run(7, 2000)], nodeAvailable: true);
        var invalid = Attempt(701, 7, 200, outcome: "FutureOutcome");
        source = source with
        {
            Anchor = new(source.Anchor.Run, [Attempt(1001, 10, 300), Attempt(1002, 10, 500)], false),
            PreviousRuns = [new(source.PreviousRuns[0].Run, [Attempt(901, 9, 100), Attempt(902, 9, 200)], false),
                new(source.PreviousRuns[1].Run, [Attempt(801, 8, 500)], true),
                new(source.PreviousRuns[2].Run, [invalid], false)],
        };
        var result = await Service(source).InspectAsync(10, NodeId, 10, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("previous_runs")[0].GetProperty("node_attempts").GetArrayLength());
        var reference = root.GetProperty("references").GetProperty("node_duration");
        Assert.Equal(new[] { 9 }, reference.GetProperty("run_record_ids").EnumerateArray().Select(item => item.GetInt32()));
        Assert.Equal(300, reference.GetProperty("mean_ms").GetDouble());
        Assert.Equal(800, reference.GetProperty("target_duration_ms").GetDouble());
        Assert.True(root.GetProperty("coverage").GetProperty("node_attempts_truncated").GetBoolean());
        Assert.False(root.GetProperty("coverage").GetProperty("returned_window_complete").GetBoolean());
    }

    [Fact]
    public async Task ZeroDurationSamplesAreRealSamplesButDoNotProduceAnInfiniteRatio()
    {
        var result = await Service(Result(Run(10, 1000), [Run(9, 0)])).InspectAsync(10, null, 10, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var reference = document.RootElement.GetProperty("references").GetProperty("run_duration");
        Assert.Equal(1, reference.GetProperty("sample_count").GetInt32());
        Assert.Equal(0, reference.GetProperty("median_ms").GetDouble());
        Assert.Equal(JsonValueKind.Null, reference.GetProperty("target_to_median_ratio").ValueKind);
    }

    [Theory]
    [InlineData("canceled")]
    [InlineData("running")]
    [InlineData("negative-elapsed")]
    [InlineData("backward-time")]
    [InlineData("unknown-status")]
    [InlineData("recovered")]
    public async Task IncompleteCancelledOrInvalidTargetTimingIsNotPresentedAsAPerformanceComparison(string kind)
    {
        var target = Run(10, 200);
        switch (kind)
        {
            case "canceled": target.Status = FlowStatus.Canceled; target.FinalOutcome = FlowFinalOutcome.Canceled; break;
            case "running": target.Status = FlowStatus.Runing; target.CompletedTimeUtc = null; break;
            case "negative-elapsed": target.ElapsedMs = -1; break;
            case "backward-time": target.CompletedTimeUtc = Started.AddTicks(-1); break;
            case "unknown-status": target.Status = (FlowStatus)99; break;
            case "recovered": target.RecoveredTimeUtc = Started; break;
        }
        var prior = Run(9, 1000);
        var result = await Service(Result(target, [prior])).InspectAsync(10, null, 10, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var reference = document.RootElement.GetProperty("references").GetProperty("run_duration");
        Assert.Equal(1, reference.GetProperty("sample_count").GetInt32());
        Assert.Equal(JsonValueKind.Null, reference.GetProperty("target_duration_ms").ValueKind);
        Assert.Equal(JsonValueKind.Null, reference.GetProperty("target_minus_median_ms").ValueKind);
    }

    [Fact]
    public async Task AvailableCanonicalHistoryWithNoEarlierMatchesHasNullStatisticsRatherThanZeroDurationSamples()
    {
        var result = await Service(Result(Run(10, 2000), [])).InspectAsync(10, null, 10, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.Empty(root.GetProperty("previous_runs").EnumerateArray());
        Assert.True(root.GetProperty("coverage").GetProperty("history_available").GetBoolean());
        var reference = root.GetProperty("references").GetProperty("run_duration");
        Assert.Equal(0, reference.GetProperty("sample_count").GetInt32());
        Assert.Equal(JsonValueKind.Null, reference.GetProperty("mean_ms").ValueKind);
        Assert.Equal(JsonValueKind.Null, reference.GetProperty("median_ms").ValueKind);
    }

    [Theory]
    [InlineData(" recorded-node ")]
    [InlineData("node-sk-1234567890abcdef1234567890abcdef")]
    public async Task ExactNodeIdentityAndUnknownOutcomeWhitespaceArePreservedInDeliveredEvidence(string exactNode)
    {
        var anchorAttempt = Attempt(1001, 10, 100);
        anchorAttempt.NodeId = exactNode;
        var previousAttempt = Attempt(901, 9, 100, "Succeeded ");
        previousAttempt.NodeId = exactNode;
        var source = new FlowRunHistoryReadResult(new(Run(10, 2000), [anchorAttempt], false),
            [new(Run(9, 1000), [previousAttempt], false)], "flow_key", false, true, false);
        var result = await Service(source).InspectAsync(10, exactNode, 10, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.Equal(exactNode, root.GetProperty("node_id").GetString());
        Assert.Equal(exactNode, root.GetProperty("anchor").GetProperty("node_attempts")[0].GetProperty("node_id").GetString());
        var attempt = root.GetProperty("previous_runs")[0].GetProperty("node_attempts")[0];
        Assert.Equal(exactNode, attempt.GetProperty("node_id").GetString());
        Assert.Equal("Succeeded ", attempt.GetProperty("outcome").GetString());
        Assert.False(attempt.GetProperty("outcome_recognized").GetBoolean());
        Assert.Equal(0, root.GetProperty("references").GetProperty("node_duration").GetProperty("sample_count").GetInt32());
    }

    [Fact]
    public async Task MissingAttemptSchemaIsPartialAndDoesNotFabricateZeroNodeTime()
    {
        var result = await Service(Result(Run(10, 2000), [Run(9, 1000)])).InspectAsync(10, NodeId, 10, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.False(root.GetProperty("coverage").GetProperty("node_attempts_available").GetBoolean());
        Assert.False(root.GetProperty("coverage").GetProperty("returned_window_complete").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("references").GetProperty("node_duration").ValueKind);
    }

    [Fact]
    public async Task BudgetedResponseRecomputesStatisticsFromOnlyDeliveredEntriesAndPreservesTheAnchor()
    {
        var entries = Enumerable.Range(1, 50).Reverse().Select(number =>
        {
            var run = Run(number, number * 100);
            run.FlowName = new string('文', 500) + "😀";
            run.RunKey = new string('键', 300);
            return new FlowRunHistoryEntry(run, Enumerable.Range(1, 10).Select(attempt =>
            {
                var value = Attempt(number * 100 + attempt, number, attempt * 10);
                value.ErrorCode = "sk-1234567890abcdef1234567890abcdef";
                return value;
            }).ToArray(), false);
        }).ToArray();
        var source = new FlowRunHistoryReadResult(new(Run(100, 6000), [Attempt(10001, 100, 100)], false), entries,
            "flow_key", false, true, false);
        var result = await Service(source).InspectAsync(100, NodeId, 50, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.InRange(result.Content.Length, 1, CopilotFlowRunHistoryInspectionService.MaximumContentCharacters);
        Assert.DoesNotContain("sk-1234567890abcdef1234567890abcdef", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_", result.Content, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.Equal(100, root.GetProperty("anchor").GetProperty("run_record_id").GetInt32());
        var delivered = root.GetProperty("previous_runs").EnumerateArray().ToArray();
        Assert.InRange(delivered.Length, 1, 49);
        var ids = delivered.Select(entry => entry.GetProperty("run_record_id").GetInt32()).ToArray();
        var reference = root.GetProperty("references").GetProperty("run_duration");
        Assert.Equal(ids, reference.GetProperty("run_record_ids").EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal(delivered.Average(entry => entry.GetProperty("elapsed_ms").GetDouble()), reference.GetProperty("mean_ms").GetDouble());
        foreach (var entry in delivered)
        {
            var name = entry.GetProperty("flow_name").GetString()!;
            Assert.Equal(name, string.Concat(name.EnumerateRunes().Select(rune => rune.ToString())));
        }
        Assert.True(root.GetProperty("coverage").GetProperty("history_truncated").GetBoolean());
        Assert.True(root.GetProperty("coverage").GetProperty("output_limited").GetBoolean());
    }

    [Theory]
    [InlineData("missing", CopilotToolFailureKind.NotFound)]
    [InlineData("schema", CopilotToolFailureKind.Validation)]
    [InlineData("access", CopilotToolFailureKind.Authorization)]
    [InlineData("busy", CopilotToolFailureKind.Transient)]
    public async Task SourceFailuresStayDistinctFromSuccessfulEmptyHistory(string kind, CopilotToolFailureKind expected)
    {
        var service = new CopilotFlowRunHistoryInspectionService((_, _, _, _) => Task.FromException<FlowRunHistoryReadResult?>(kind switch
        {
            "missing" => new FileNotFoundException("PRIVATE_PATH"), "schema" => new InvalidDataException("PRIVATE_PATH"),
            "access" => new UnauthorizedAccessException("PRIVATE_PATH"), _ => new IOException("PRIVATE_PATH"),
        }));
        var result = await service.InspectAsync(10, null, 10, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(expected, result.FailureKind);
        Assert.DoesNotContain("PRIVATE_PATH", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExactMissingRunCancellationAndInvalidNodeInputsDoNotBecomeEmptySuccessfulHistory()
    {
        var calls = 0;
        var service = new CopilotFlowRunHistoryInspectionService((_, _, _, _) => { calls++; return Task.FromResult<FlowRunHistoryReadResult?>(null); });
        var missing = await service.InspectAsync(10, null, 10, CancellationToken.None);
        Assert.False(missing.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, missing.FailureKind);
        var invalid = await service.InspectAsync(10, " ", 10, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, invalid.FailureKind);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InspectAsync(10, null, 10, cancellation.Token));
        Assert.Equal(1, calls);
    }

    private static CopilotFlowRunHistoryInspectionService Service(FlowRunHistoryReadResult source) => new((_, _, _, _) => Task.FromResult<FlowRunHistoryReadResult?>(source));
    private static FlowRunHistoryReadResult Result(FlowRunRecord anchor, IReadOnlyList<FlowRunRecord> previous, bool nodeAvailable = false) =>
        new(new(anchor, [], false), previous.Select(run => new FlowRunHistoryEntry(run, [], false)).ToArray(), "flow_key", false, nodeAvailable, false);
    private static FlowRunRecord Run(int id, long elapsed, FlowStatus status = FlowStatus.Completed,
        FlowFinalOutcome outcome = FlowFinalOutcome.Succeeded, string? hash = HashA) => new()
    {
        Id = id, TemplateId = 42, FlowKey = "same-flow", FlowName = "Recorded flow", RunKey = "run-" + id,
        Status = status, FinalOutcome = outcome, ElapsedMs = elapsed, StartedTimeUtc = Started,
        CompletedTimeUtc = Started.AddMilliseconds(Math.Clamp(elapsed, 0, 10000)), ContentHash = hash, SnapshotId = 5_000_000_001,
        SerialNumber = "PRIVATE_SERIAL", OwnerMachine = "PRIVATE_MACHINE",
    };
    private static FlowNodeAttempt Attempt(int id, int runId, int duration, string outcome = "Succeeded") => new()
    {
        Id = id, RunRecordId = runId, NodeId = NodeId, AttemptNo = 1, StartedTimeUtc = Started,
        CompletedTimeUtc = Started.AddMilliseconds(duration), Outcome = outcome, ErrorMessage = "PRIVATE_ERROR_MESSAGE", InvocationId = "PRIVATE_INVOCATION",
    };
}
