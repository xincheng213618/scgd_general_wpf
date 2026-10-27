using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunAttemptServiceTests
{
    private static readonly string SourceKey = new('a', 64);

    [Fact]
    public async Task BudgetedOlderWindowsDeliverEveryInt64AttemptWithoutDeduplicatingRepeatedNumbers()
    {
        var rows = Enumerable.Range(0, 100).Select(index => Attempt(long.MaxValue - 99 + index)).ToArray();
        foreach (var row in rows)
        {
            row.NodeId = "  node/non-guid  ";
            row.ErrorMessage = "password=\"alpha, beta\"; " + new string('中', 2048) + "😀";
            row.InvocationId = "PRIVATE_INVOCATION";
            row.LegacyNodeRecordId = 12345;
        }
        var service = new CopilotFlowRunAttemptInspectionService((run, node, maximum, before, _) =>
        {
            Assert.Equal(11, run);
            Assert.Equal("  node/non-guid  ", node);
            var remaining = rows.Where(row => !before.HasValue || row.Id < before.Value).ToArray();
            return Task.FromResult<FlowRunAttemptPage?>(Page(remaining.TakeLast(maximum).ToArray(), remaining.Length > maximum));
        });
        var delivered = new List<long>();
        var cursors = new HashSet<string>();
        long? previousMinimum = null;
        string? cursor = null;
        do
        {
            var result = await service.InspectAsync(11, "  node/non-guid  ", cursor == null ? 100 : 7, cursor, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.InRange(result.Content.Length, 1, CopilotFlowRunAttemptInspectionService.MaximumContentCharacters);
            Assert.DoesNotContain("PRIVATE_INVOCATION", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("legacy_node_record_id", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("invocation_id", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("alpha", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("beta", result.Content, StringComparison.Ordinal);
            Assert.Equal(result.Content, string.Concat(result.Content.EnumerateRunes().Select(rune => rune.ToString())));
            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;
            var page = root.GetProperty("attempts").EnumerateArray().ToArray();
            var ids = page.Select(row => row.GetProperty("attempt_id").GetInt64()).ToArray();
            Assert.NotEmpty(ids);
            Assert.Equal(ids.Order(), ids);
            if (previousMinimum.HasValue) Assert.All(ids, id => Assert.True(id < previousMinimum.Value));
            previousMinimum = ids[0];
            Assert.All(page, row =>
            {
                Assert.Equal("  node/non-guid  ", row.GetProperty("node_id").GetString());
                Assert.Equal(1, row.GetProperty("attempt_no").GetInt32());
            });
            var coverage = root.GetProperty("coverage");
            Assert.Equal(page.Length, coverage.GetProperty("attempts_returned").GetInt32());
            Assert.True(coverage.GetProperty("text_redacted").GetBoolean());
            if (cursor == null) Assert.True(coverage.GetProperty("output_limited").GetBoolean());
            Assert.Equal(cursor == null, coverage.GetProperty("page_includes_latest_attempts").GetBoolean());
            delivered.AddRange(ids);
            cursor = root.GetProperty("next_cursor").GetString();
            Assert.Equal(cursor != null, root.GetProperty("has_more").GetBoolean());
            Assert.Equal(cursor == null, coverage.GetProperty("reached_oldest_retained_attempt").GetBoolean());
            Assert.NotEmpty(result.PartialResultMessage);
            if (cursor != null) Assert.True(cursors.Add(cursor));
        } while (cursor != null);
        Assert.Equal(rows.Select(row => row.Id).Order(), delivered.Order());
        Assert.Equal(rows.Length, delivered.Distinct().Count());
    }

    [Fact]
    public async Task CursorBindsRunExactUntrimmedNodeAndSourceWhileAllowingPageSizeChanges()
    {
        var calls = 0;
        var source = SourceKey;
        var stale = false;
        var service = new CopilotFlowRunAttemptInspectionService((run, node, maximum, before, _) =>
        {
            calls++;
            Assert.Equal(" node ", node);
            if (stale) throw new ArgumentException("PRIVATE_ANCHOR");
            var row = Attempt(before == null ? long.MaxValue : long.MaxValue - 1);
            row.NodeId = node!;
            return Task.FromResult<FlowRunAttemptPage?>(Page([row], before == null, source));
        });
        var first = await service.InspectAsync(11, " node ", 1, null, CancellationToken.None);
        var cursor = Next(first)!;
        var payload = Encoding.UTF8.GetString(Decode(cursor));
        Assert.DoesNotContain(" node ", payload, StringComparison.Ordinal);
        Assert.Equal(long.MaxValue, JsonNode.Parse(payload)!["attempt_id"]!.GetValue<long>());
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(22, " node ", 100, cursor, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(11, "node", 100, cursor, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(11, null, 100, cursor, CancellationToken.None)).FailureKind);
        Assert.Equal(1, calls);
        Assert.True((await service.InspectAsync(11, " node ", 100, cursor, CancellationToken.None)).Success);
        source = new string('b', 64);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(11, " node ", 100, cursor, CancellationToken.None)).FailureKind);
        source = SourceKey;
        stale = true;
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(11, " node ", 100, cursor, CancellationToken.None)).FailureKind);
    }

    [Fact]
    public async Task InvalidBoundsAndMalformedCursorsNeverReachTheSource()
    {
        var calls = 0;
        var service = new CopilotFlowRunAttemptInspectionService((_, _, _, _, _) =>
        {
            calls++;
            return Task.FromResult<FlowRunAttemptPage?>(Page([Attempt(10)], true));
        });
        var cursor = Next(await service.InspectAsync(11, null, 1, null, CancellationToken.None))!;
        calls = 0;
        foreach (var (run, node, maximum) in new[] { (0, (string?)null, 40), (11, "", 40), (11, " \t", 40), (11, new string('n', 129), 40), (11, null, 0), (11, null, 101) })
            Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(run, node, maximum, null, CancellationToken.None)).FailureKind);
        var payload = JsonNode.Parse(Decode(cursor))!.AsObject();
        payload["attempt_id"] = 0;
        var zero = Encode(payload.ToJsonString());
        payload["attempt_id"] = 10;
        payload["version"] = 2;
        var version = Encode(payload.ToJsonString());
        payload["version"] = 1;
        payload["extra"] = true;
        var extra = Encode(payload.ToJsonString());
        var duplicate = Encode(Encoding.UTF8.GetString(Decode(cursor)).Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal));
        foreach (var invalid in new[] { "", " ", cursor + "=", " " + cursor, new string('x', 4097), zero, version, extra, duplicate })
            Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(11, null, 40, invalid, CancellationToken.None)).FailureKind);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(null, "missing", false, false)]
    [InlineData("Succeeded", "valid", true, true)]
    [InlineData("Failed", "reverse", true, false)]
    [InlineData("Succeeded", "default-start", true, false)]
    [InlineData("TimedOut", "valid", true, true)]
    [InlineData("Canceled", "valid", true, true)]
    [InlineData("Interrupted", "valid", true, true)]
    [InlineData("succeeded", "valid", false, true)]
    [InlineData(" Succeeded ", "valid", false, true)]
    [InlineData("FutureOutcome", "valid", false, true)]
    public async Task OutcomesAndDurationsAreRecordedEvidenceWithoutInventedExecutionPolicy(string? outcome, string timing, bool known, bool validDuration)
    {
        var row = Attempt(1);
        row.Outcome = outcome;
        row.CompletedTimeUtc = timing == "missing" ? null : timing == "reverse" ? row.StartedTimeUtc.AddTicks(-1) : row.StartedTimeUtc;
        if (timing == "default-start") row.StartedTimeUtc = default;
        var result = await new CopilotFlowRunAttemptInspectionService((_, _, _, _, _) => Task.FromResult<FlowRunAttemptPage?>(Page([row])))
            .InspectAsync(11, null, 40, null, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var evidence = Assert.Single(document.RootElement.GetProperty("attempts").EnumerateArray());
        Assert.Equal(outcome, evidence.GetProperty("outcome").GetString());
        Assert.Equal(known, evidence.GetProperty("outcome_recognized").GetBoolean());
        Assert.Equal(validDuration, evidence.GetProperty("duration_valid").GetBoolean());
        if (validDuration) Assert.Equal(0, evidence.GetProperty("duration_ms").GetDouble());
        else Assert.Equal(JsonValueKind.Null, evidence.GetProperty("duration_ms").ValueKind);
        Assert.False(evidence.TryGetProperty("continue_on_fail", out _));
    }

    [Fact]
    public async Task OnlyTheTruncatedOutcomesLoseRecognitionAndAWorstCaseSingleRowStillFits()
    {
        var first = Attempt(1);
        var second = Attempt(2);
        var page = Page([first, second]) with { TextWasTruncated = true, OutcomeTruncatedAttemptIds = new long[] { 1 } };
        var result = await new CopilotFlowRunAttemptInspectionService((_, _, _, _, _) => Task.FromResult<FlowRunAttemptPage?>(page))
            .InspectAsync(11, null, 40, null, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var evidence = document.RootElement.GetProperty("attempts");
        Assert.Equal("Succeeded", evidence[0].GetProperty("outcome").GetString());
        Assert.True(evidence[0].GetProperty("outcome_truncated").GetBoolean());
        Assert.False(evidence[0].GetProperty("outcome_recognized").GetBoolean());
        Assert.False(evidence[1].GetProperty("outcome_truncated").GetBoolean());
        Assert.True(evidence[1].GetProperty("outcome_recognized").GetBoolean());

        var huge = Attempt(long.MaxValue);
        huge.NodeId = new string('中', 128);
        huge.Outcome = new string('中', 256);
        huge.ErrorCode = new string('中', 256);
        huge.ErrorMessage = new string('中', 2048);
        var bounded = await new CopilotFlowRunAttemptInspectionService((_, _, _, _, _) => Task.FromResult<FlowRunAttemptPage?>(Page([huge])))
            .InspectAsync(11, huge.NodeId, 1, null, CancellationToken.None);
        Assert.True(bounded.Success, bounded.ErrorMessage);
        Assert.InRange(bounded.Content.Length, 1, CopilotFlowRunAttemptInspectionService.MaximumContentCharacters);
        Assert.NotEmpty(bounded.PartialResultMessage);
    }

    [Theory]
    [InlineData("run")]
    [InlineData("association")]
    [InlineData("node")]
    [InlineData("order")]
    [InlineData("source")]
    [InlineData("empty-more")]
    [InlineData("truncation-foreign")]
    [InlineData("truncation-duplicate")]
    [InlineData("truncation-flag")]
    public async Task InconsistentSourcesCannotPublishUnrelatedOrAmbiguousAttemptEvidence(string kind)
    {
        var row = Attempt(1);
        if (kind == "association") row.RunRecordId = 22;
        if (kind == "node") row.NodeId = "different";
        var page = kind switch
        {
            "run" => Page([row]) with { RunRecordId = 22 },
            "order" => Page([Attempt(2), row]),
            "source" => Page([row], source: "invalid"),
            "empty-more" => Page([], true),
            "truncation-foreign" => Page([row]) with { TextWasTruncated = true, OutcomeTruncatedAttemptIds = new long[] { 2 } },
            "truncation-duplicate" => Page([row]) with { TextWasTruncated = true, OutcomeTruncatedAttemptIds = new long[] { 1, 1 } },
            "truncation-flag" => Page([row]) with { OutcomeTruncatedAttemptIds = new long[] { 1 } },
            _ => Page([row]),
        };
        var result = await new CopilotFlowRunAttemptInspectionService((_, _, _, _, _) => Task.FromResult<FlowRunAttemptPage?>(page))
            .InspectAsync(11, "node", 40, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("missing-file", CopilotToolFailureKind.NotFound)]
    [InlineData("schema", CopilotToolFailureKind.Validation)]
    [InlineData("anchor", CopilotToolFailureKind.Validation)]
    [InlineData("denied", CopilotToolFailureKind.Authorization)]
    [InlineData("io", CopilotToolFailureKind.Transient)]
    public async Task ClassifiedFailuresRemainDistinctFromAnEmptyKnownRun(string kind, CopilotToolFailureKind expected)
    {
        var service = new CopilotFlowRunAttemptInspectionService((_, _, _, _, _) => throw (kind switch
        {
            "missing-file" => (Exception)new FileNotFoundException("PRIVATE_PATH"),
            "schema" => new InvalidDataException("PRIVATE_SCHEMA"),
            "anchor" => new ArgumentException("PRIVATE_ANCHOR"),
            "denied" => new UnauthorizedAccessException("PRIVATE_PATH"),
            _ => new IOException("PRIVATE_PATH"),
        }));
        var result = await service.InspectAsync(11, null, 40, null, CancellationToken.None);
        Assert.Equal(expected, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.DoesNotContain("PRIVATE_", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AbsentRunAndEmptyAttemptWindowRemainDifferent()
    {
        var missing = await new CopilotFlowRunAttemptInspectionService((_, _, _, _, _) => Task.FromResult<FlowRunAttemptPage?>(null))
            .InspectAsync(11, null, 40, null, CancellationToken.None);
        Assert.False(missing.Success);
        Assert.Equal(CopilotToolFailureKind.NotFound, missing.FailureKind);
        var empty = await new CopilotFlowRunAttemptInspectionService((_, _, _, _, _) => Task.FromResult<FlowRunAttemptPage?>(Page([])))
            .InspectAsync(11, null, 40, null, CancellationToken.None);
        Assert.True(empty.Success, empty.ErrorMessage);
        Assert.Null(Next(empty));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeReadingOrAfterALateResultDoesNotPublishEvidence(bool late)
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var service = new CopilotFlowRunAttemptInspectionService((_, _, _, _, token) =>
        {
            calls++;
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return Task.FromResult<FlowRunAttemptPage?>(Page([Attempt(1)]));
        });
        if (!late) cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InspectAsync(11, null, 40, null, cancellation.Token));
        Assert.Equal(late ? 1 : 0, calls);
    }

    private static FlowNodeAttempt Attempt(long id) => new()
    {
        Id = id, RunRecordId = 11, NodeId = "node", AttemptNo = 1, Outcome = "Succeeded",
        StartedTimeUtc = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc),
        CompletedTimeUtc = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc),
    };
    private static FlowRunAttemptPage Page(IReadOnlyList<FlowNodeAttempt> rows, bool more = false, string? source = null) => new(11, rows, more, false, source ?? SourceKey);
    private static string? Next(CopilotCapabilityResult result)
    {
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        return document.RootElement.GetProperty("next_cursor").GetString();
    }
    private static byte[] Decode(string cursor)
    {
        var encoded = cursor["cvra1:".Length..].Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(encoded + new string('=', (4 - encoded.Length % 4) % 4));
    }
    private static string Encode(string payload) => "cvra1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
