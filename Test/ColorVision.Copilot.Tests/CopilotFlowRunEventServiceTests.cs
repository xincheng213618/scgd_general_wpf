using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DatabaseFixture = ColorVision.Copilot.Tests.CopilotFlowRunEventSourceTests.DatabaseFixture;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunEventServiceTests
{
    private const string SourceKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task RealJournalBudgetShrinkingReachesEarlyEventsWithoutLosingRowsOrReadingAnotherRun()
    {
        using var database = new DatabaseFixture();
        database.CreateDatabase();
        database.AddRun(11);
        database.AddRun(22);
        const string secret = "sk-1234567890abcdef1234567890abcdef";
        var expected = Enumerable.Range(1, 160).Select(sequence => database.AddEvent(11, sequence,
            secret + new string('中', 580) + "😀", nodeId: "  node  ", attemptId: 5_000_000_000L)).ToArray();
        database.AddEvent(22, 160, "OTHER_RUN_PRIVATE");
        var originalBytes = File.ReadAllBytes(database.DatabasePath);
        var service = new CopilotFlowRunEventInspectionService((run, maximum, before, token) => database.Reader.ReadPageAsync(run, maximum, before, token));
        var delivered = new List<long>();
        var cursors = new HashSet<string>();
        string? cursor = null;
        long? earliestSequence = null;
        do
        {
            var maximum = cursor == null ? 100 : 40;
            var result = await service.InspectAsync(11, maximum, cursor, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.InRange(result.Content.Length, 1, CopilotFlowRunEventInspectionService.MaximumContentCharacters);
            Assert.DoesNotContain(secret, result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE", result.Content, StringComparison.Ordinal);
            Assert.Equal(result.Content, string.Concat(result.Content.EnumerateRunes().Select(rune => rune.ToString())));
            using var json = JsonDocument.Parse(result.Content);
            var root = json.RootElement;
            var rows = root.GetProperty("events").EnumerateArray().ToArray();
            Assert.NotEmpty(rows);
            var coverage = root.GetProperty("coverage");
            Assert.Equal(rows.Length, coverage.GetProperty("events_returned").GetInt32());
            Assert.Equal(cursor == null, coverage.GetProperty("page_includes_latest_events").GetBoolean());
            Assert.True(coverage.GetProperty("text_redacted").GetBoolean());
            var sequences = rows.Select(row => row.GetProperty("sequence_no").GetInt64()).ToArray();
            Assert.Equal(sequences.Order(), sequences);
            if (earliestSequence.HasValue) Assert.All(sequences, sequence => Assert.True(sequence < earliestSequence.Value));
            earliestSequence = sequences[0];
            foreach (var row in rows)
            {
                Assert.Equal(11, row.GetProperty("run_record_id").GetInt32());
                Assert.Equal("  node  ", row.GetProperty("node_id").GetString());
                Assert.Equal(5_000_000_000L, row.GetProperty("attempt_id").GetInt64());
                Assert.False(row.TryGetProperty("data_json", out _));
                Assert.False(row.TryGetProperty("event_key", out _));
            }
            if (cursor == null) Assert.True(coverage.GetProperty("output_limited").GetBoolean());
            delivered.AddRange(rows.Select(row => row.GetProperty("event_id").GetInt64()));
            cursor = root.GetProperty("next_cursor").GetString();
            Assert.Equal(cursor != null, root.GetProperty("has_more").GetBoolean());
            Assert.Equal(cursor == null, coverage.GetProperty("reached_oldest_retained_event").GetBoolean());
            if (cursor != null) Assert.True(cursors.Add(cursor), "Older-event continuation must advance.");
        } while (cursor != null);
        Assert.Equal(expected.Select(item => item.Id).Order(), delivered.Order());
        Assert.Equal(delivered.Count, delivered.Distinct().Count());
        Assert.Equal(originalBytes, File.ReadAllBytes(database.DatabasePath));
    }

    [Fact]
    public async Task EmptyRetainedRunIsDifferentFromAnUnavailableRun()
    {
        var empty = await Service(Page([])).InspectAsync(11, 40, null, CancellationToken.None);
        Assert.True(empty.Success, empty.ErrorMessage);
        using var json = JsonDocument.Parse(empty.Content);
        Assert.Empty(json.RootElement.GetProperty("events").EnumerateArray());
        Assert.False(json.RootElement.GetProperty("has_more").GetBoolean());
        Assert.Contains("does not prove", json.RootElement.GetProperty("coverage").GetProperty("query_scope").GetString(), StringComparison.Ordinal);
        var missing = new CopilotFlowRunEventInspectionService((_, _, _, _) => Task.FromResult<FlowRunEventPage?>(null));
        var result = await missing.InspectAsync(11, 40, null, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.NotFound, result.FailureKind);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("sequence")]
    [InlineData("deleted-anchor")]
    [InlineData("deleted-run")]
    [InlineData("different-run")]
    public async Task ChangedCursorAssociationsRequireRestartRatherThanAnotherEventWindow(string mutation)
    {
        var calls = 0;
        var service = new CopilotFlowRunEventInspectionService((_, _, before, _) =>
        {
            calls++;
            if (before == null) return Task.FromResult<FlowRunEventPage?>(Page([Event(3), Event(4)], true));
            if (mutation == "deleted-anchor") throw new ArgumentException("The anchor no longer exists.");
            if (mutation == "deleted-run") return Task.FromResult<FlowRunEventPage?>(null);
            return Task.FromResult<FlowRunEventPage?>(Page([Event(1), Event(2)]) with
            {
                SourceKey = mutation == "source" ? new string('b', 64) : SourceKey,
                AnchorSequenceNo = mutation == "sequence" ? 8 : 3,
            });
        });
        var cursor = Next(await service.InspectAsync(11, 2, null, CancellationToken.None));
        var result = await service.InspectAsync(mutation == "different-run" ? 22 : 11, 100, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.Contains("Restart", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(mutation == "different-run" ? 1 : 2, calls);
    }

    [Theory]
    [InlineData("empty-more")]
    [InlineData("wrong-run")]
    [InlineData("foreign-event")]
    [InlineData("duplicate")]
    [InlineData("order")]
    public async Task InconsistentSourcePagesCannotPublishEvidence(string mutation)
    {
        var page = mutation switch
        {
            "empty-more" => Page([], true), "wrong-run" => Page([Event(1)]) with { RunRecordId = 22 },
            "foreign-event" => Page([new FlowExecutionEvent { Id = 1, RunRecordId = 22 }]),
            "duplicate" => Page([Event(1), Event(1)]), _ => Page([Event(2), Event(1)]),
        };
        var result = await Service(page).InspectAsync(11, 40, null, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("whitespace")]
    [InlineData("duplicate")]
    [InlineData("version")]
    [InlineData("extra")]
    [InlineData("sequence-type")]
    [InlineData("missing")]
    [InlineData("oversize")]
    public async Task MalformedCursorDoesNotOpenTheJournal(string mutation)
    {
        var calls = 0;
        var service = new CopilotFlowRunEventInspectionService((_, _, _, _) => { calls++; return Task.FromResult<FlowRunEventPage?>(Page([Event(3)], true)); });
        var cursor = Next(await service.InspectAsync(11, 1, null, CancellationToken.None))!;
        var encoded = cursor[6..].Replace('-', '+').Replace('_', '/');
        var source = Encoding.UTF8.GetString(Convert.FromBase64String(encoded + new string('=', (4 - encoded.Length % 4) % 4)));
        var json = JsonNode.Parse(source)!.AsObject();
        switch (mutation)
        {
            case "version": json["version"] = 2; break;
            case "extra": json["extra"] = true; break;
            case "sequence-type": json["sequence_no"] = "3"; break;
            case "missing": json.Remove("source_key"); break;
        }
        var text = mutation == "duplicate" ? source.Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal) : json.ToJsonString();
        var changed = "cvre1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        cursor = mutation switch { "empty" => "", "whitespace" => " " + cursor, "oversize" => cursor + new string('a', 4096), _ => changed };
        var result = await service.InspectAsync(11, 1, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Equal(1, calls);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("missing", CopilotToolFailureKind.NotFound)]
    [InlineData("schema", CopilotToolFailureKind.Validation)]
    [InlineData("access", CopilotToolFailureKind.Authorization)]
    [InlineData("io", CopilotToolFailureKind.Transient)]
    public async Task SourceFailuresRemainClassifiedRatherThanBecomingEmptyEvents(string error, CopilotToolFailureKind kind)
    {
        Exception failure = error switch
        {
            "missing" => new FileNotFoundException(), "schema" => new InvalidDataException(),
            "access" => new UnauthorizedAccessException(), _ => new IOException(),
        };
        var service = new CopilotFlowRunEventInspectionService((_, _, _, _) => Task.FromException<FlowRunEventPage?>(failure));
        var result = await service.InspectAsync(11, 40, null, CancellationToken.None);
        Assert.Equal(kind, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Fact]
    public async Task ACancelledReadNeverBecomesSuccessfulAbsence()
    {
        using var cancellation = new CancellationTokenSource();
        var service = new CopilotFlowRunEventInspectionService((_, _, _, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return Task.FromCanceled<FlowRunEventPage?>(token);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InspectAsync(11, 40, null, cancellation.Token));
    }

    private static FlowExecutionEvent Event(long sequence) => new()
    {
        Id = sequence, RunRecordId = 11, SequenceNo = sequence, EventType = "NodeStarted",
        OccurredTimeUtc = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc),
    };
    private static FlowRunEventPage Page(IReadOnlyList<FlowExecutionEvent> events, bool hasMore = false) => new(11, events, hasMore, false, SourceKey);
    private static CopilotFlowRunEventInspectionService Service(FlowRunEventPage page) => new((_, _, _, _) => Task.FromResult<FlowRunEventPage?>(page));
    private static string? Next(CopilotCapabilityResult result)
    {
        Assert.True(result.Success, result.ErrorMessage);
        using var json = JsonDocument.Parse(result.Content);
        return json.RootElement.GetProperty("next_cursor").GetString();
    }
}
