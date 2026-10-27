using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Services.FlowDiagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunQueryServiceTests
{
    private static readonly string SourceKey = new('a', 64);

    [Fact]
    public async Task OutputBudgetPagesEveryActualDeliveredRunExactlyOnceWithoutPrivateFields()
    {
        var rows = Enumerable.Range(1, 100).Reverse().Select(id => Run(id)).ToArray();
        foreach (var row in rows)
        {
            row.FlowKey = new string('流', 256);
            row.FlowName = "token=\"alpha, beta\"; " + new string('中', 160) + "😀";
            row.SerialNumber = "PRIVATE_SERIAL";
            row.OwnerMachine = "PRIVATE_MACHINE";
            row.RunKey = "PRIVATE_RUN_KEY";
        }
        var service = new CopilotFlowRunQueryService((filter, maximum, before, _) =>
        {
            Assert.Null(filter);
            var remaining = rows.Where(row => !before.HasValue || row.Id < before.Value).ToArray();
            return Task.FromResult(Page(remaining.Take(maximum).ToArray(), remaining.Length > maximum));
        });
        string? cursor = null;
        var delivered = new List<int>();
        var cursors = new HashSet<string>();
        do
        {
            var result = await service.ListAsync(null, cursor == null ? 100 : 3, cursor, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.InRange(result.Content.Length, 1, CopilotFlowRunQueryService.MaximumContentCharacters);
            Assert.DoesNotContain("PRIVATE_", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("alpha", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("beta", result.Content, StringComparison.Ordinal);
            Assert.Equal(result.Content, string.Concat(result.Content.EnumerateRunes().Select(rune => rune.ToString())));
            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;
            var page = root.GetProperty("runs").EnumerateArray().ToArray();
            Assert.NotEmpty(page);
            Assert.Equal(page.Length, root.GetProperty("coverage").GetProperty("runs_returned").GetInt32());
            if (cursor == null)
            {
                Assert.True(root.GetProperty("coverage").GetProperty("output_limited").GetBoolean());
                Assert.NotEmpty(result.PartialResultMessage);
            }
            delivered.AddRange(page.Select(row => row.GetProperty("run_record_id").GetInt32()));
            cursor = root.GetProperty("next_cursor").GetString();
            Assert.Equal(cursor != null, root.GetProperty("has_more").GetBoolean());
            if (cursor != null) Assert.True(cursors.Add(cursor));
        } while (cursor != null);
        Assert.Equal(rows.Select(row => row.Id), delivered);
        Assert.All(rows, row => Assert.Contains("alpha, beta", row.FlowName, StringComparison.Ordinal));
    }

    [Fact]
    public async Task CursorBindsCanonicalFilterSourceAndAnchorWhileAllowingResizing()
    {
        var calls = new List<(string? Filter, int Maximum, int? Before)>();
        var source = SourceKey;
        var stale = false;
        var service = new CopilotFlowRunQueryService((filter, maximum, before, _) =>
        {
            calls.Add((filter, maximum, before));
            if (stale) throw new ArgumentException("Anchor was deleted or no longer matches.");
            return Task.FromResult(Page([Run(before == null ? 9 : 8)], before == null, source));
        });
        var first = await service.ListAsync(" \u2003Flow/A\t ", 1, null, CancellationToken.None);
        var cursor = Next(first)!;
        Assert.Equal(("Flow/A", 1, (int?)null), Assert.Single(calls));
        var cursorJson = Encoding.UTF8.GetString(Decode(cursor));
        Assert.DoesNotContain("Flow/A", cursorJson, StringComparison.Ordinal);
        Assert.Equal(4, JsonNode.Parse(cursorJson)!.AsObject().Count);
        var different = await service.ListAsync("flow/a", 1, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, different.FailureKind);
        var allInstead = await service.ListAsync(null, 1, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, allInstead.FailureKind);
        Assert.Single(calls);
        var resized = await service.ListAsync("Flow/A", 20, cursor, CancellationToken.None);
        Assert.True(resized.Success, resized.ErrorMessage);
        Assert.Equal(("Flow/A", 20, (int?)9), calls[^1]);
        Assert.Null(Next(resized));
        source = new string('b', 64);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.ListAsync("Flow/A", 20, cursor, CancellationToken.None)).FailureKind);
        source = SourceKey;
        stale = true;
        var missingAnchor = await service.ListAsync("Flow/A", 20, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, missingAnchor.FailureKind);
        Assert.Empty(missingAnchor.Content);

        var prefix = Run(7);
        prefix.FlowKey = "Flow/";
        var bounded = await new CopilotFlowRunQueryService((_, _, _, _) => Task.FromResult(Page([prefix]) with { TextWasTruncated = true }))
            .ListAsync("Flow/A", 20, null, CancellationToken.None);
        Assert.True(bounded.Success, bounded.ErrorMessage);
        Assert.NotEmpty(bounded.PartialResultMessage);
    }

    [Fact]
    public async Task InvalidBoundsAndMalformedOrAmbiguousCursorsNeverReachTheReader()
    {
        var calls = 0;
        var service = new CopilotFlowRunQueryService((_, _, _, _) => { calls++; return Task.FromResult(Page([Run(9)], true)); });
        var cursor = Next(await service.ListAsync(null, 1, null, CancellationToken.None))!;
        calls = 0;
        foreach (var (filter, maximum) in new[] { ("", 20), (" \t", 20), (new string('x', 257), 20), ((string?)null, 0), (null, 101) })
            Assert.Equal(CopilotToolFailureKind.Validation, (await service.ListAsync(filter, maximum, null, CancellationToken.None)).FailureKind);
        var payload = JsonNode.Parse(Decode(cursor))!.AsObject();
        payload["run_record_id"] = 0;
        var zeroId = Encode(payload.ToJsonString());
        payload["run_record_id"] = int.MaxValue + 1L;
        var overId = Encode(payload.ToJsonString());
        payload["run_record_id"] = 9;
        payload["version"] = 2;
        var unknownVersion = Encode(payload.ToJsonString());
        payload["version"] = 1;
        payload["unexpected"] = true;
        var extra = Encode(payload.ToJsonString());
        var duplicate = Encode(Encoding.UTF8.GetString(Decode(cursor)).Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal));
        foreach (var invalid in new[] { "", " ", cursor + "=", " " + cursor, new string('c', 4097), zeroId, overId, unknownVersion, extra, duplicate })
            Assert.Equal(CopilotToolFailureKind.Validation, (await service.ListAsync(null, 20, invalid, CancellationToken.None)).FailureKind);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("duplicate")]
    [InlineData("ascending")]
    [InlineData("too-many")]
    [InlineData("empty-more")]
    [InlineData("source-key")]
    [InlineData("filter")]
    [InlineData("filter-truncated")]
    [InlineData("boundary")]
    public async Task InconsistentProviderEvidenceIsNeverPublished(string kind)
    {
        var calls = 0;
        var service = new CopilotFlowRunQueryService((_, _, before, _) =>
        {
            calls++;
            if (kind == "boundary" && before == null) return Task.FromResult(Page([Run(9)], true));
            var page = kind switch
            {
                "zero" => Page([Run(0)]),
                "duplicate" => Page([Run(9), Run(9)]),
                "ascending" => Page([Run(8), Run(9)]),
                "too-many" => Page([Run(9), Run(8), Run(7)]),
                "empty-more" => Page([], true),
                "source-key" => Page([Run(9)], source: "not-a-hash"),
                "filter" => Page([WithFlowKey("different")]),
                "filter-truncated" => Page([WithFlowKey("different")]) with { TextWasTruncated = true },
                "boundary" => Page([Run(9)]),
                _ => throw new InvalidOperationException(),
            };
            return Task.FromResult(page);
        });
        var cursor = kind == "boundary" ? Next(await service.ListAsync("Flow/A", 2, null, CancellationToken.None)) : null;
        var result = await service.ListAsync("Flow/A", 2, cursor, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.Equal(kind == "boundary" ? 2 : 1, calls);

        FlowRunRecord WithFlowKey(string value)
        {
            var row = Run(9);
            row.FlowKey = value;
            return row;
        }
    }

    [Fact]
    public async Task EmptyResultsAndUnknownLegacyEvidenceRemainDistinctAndDoNotInventSuccess()
    {
        var empty = await new CopilotFlowRunQueryService((_, _, _, _) => Task.FromResult(Page([]))).ListAsync(null, 20, null, CancellationToken.None);
        Assert.True(empty.Success);
        Assert.Null(Next(empty));
        var legacy = Run(int.MaxValue);
        legacy.FlowKey = null;
        legacy.Status = (FlowStatus)12345;
        legacy.FinalOutcome = null;
        legacy.ContentHash = "not-a-valid-hash";
        legacy.CompletedTimeUtc = null;
        var result = await new CopilotFlowRunQueryService((_, _, _, _) => Task.FromResult(Page([legacy]))).ListAsync(null, 20, null, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotEmpty(result.PartialResultMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        var row = Assert.Single(root.GetProperty("runs").EnumerateArray());
        Assert.Equal(int.MaxValue, row.GetProperty("run_record_id").GetInt32());
        Assert.Equal(12345, row.GetProperty("status_code").GetInt32());
        Assert.Equal("Unknown", row.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("flow_key").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("final_outcome").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("content_hash").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("completed_time_utc").ValueKind);
        Assert.Equal(1, root.GetProperty("coverage").GetProperty("runs_without_flow_key").GetInt32());
        Assert.Equal(1, root.GetProperty("coverage").GetProperty("unknown_status_runs").GetInt32());
        Assert.True(root.GetProperty("coverage").GetProperty("text_truncated").GetBoolean());

        var redacted = Run(9);
        redacted.FlowName = "password=secret-value";
        var hidden = await new CopilotFlowRunQueryService((_, _, _, _) => Task.FromResult(Page([redacted]))).ListAsync(null, 20, null, CancellationToken.None);
        Assert.True(hidden.Success, hidden.ErrorMessage);
        Assert.NotEmpty(hidden.PartialResultMessage);
        using var hiddenJson = JsonDocument.Parse(hidden.Content);
        Assert.False(hiddenJson.RootElement.GetProperty("has_more").GetBoolean());
        Assert.True(hiddenJson.RootElement.GetProperty("coverage").GetProperty("text_redacted").GetBoolean());
        Assert.False(hiddenJson.RootElement.GetProperty("coverage").GetProperty("text_truncated").GetBoolean());
    }

    [Theory]
    [InlineData("missing", CopilotToolFailureKind.NotFound)]
    [InlineData("schema", CopilotToolFailureKind.Validation)]
    [InlineData("anchor", CopilotToolFailureKind.Validation)]
    [InlineData("denied", CopilotToolFailureKind.Authorization)]
    [InlineData("io", CopilotToolFailureKind.Transient)]
    public async Task ReaderFailuresKeepTheirMeaning(string kind, CopilotToolFailureKind expected)
    {
        var service = new CopilotFlowRunQueryService((_, _, _, _) => throw (kind switch
        {
            "missing" => (Exception)new FileNotFoundException("PRIVATE_PATH"),
            "schema" => new InvalidDataException("PRIVATE_COLUMN"),
            "anchor" => new ArgumentException("PRIVATE_FILTER"),
            "denied" => new UnauthorizedAccessException("PRIVATE_PATH"),
            _ => new IOException("PRIVATE_PATH"),
        }));
        var result = await service.ListAsync(null, 20, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(expected, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.DoesNotContain("PRIVATE_", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreCancellationAndLateReaderResultsCannotPublishEvidence(bool late)
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var service = new CopilotFlowRunQueryService((_, _, _, token) =>
        {
            calls++;
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return Task.FromResult(Page([Run(9)]));
        });
        if (!late) cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ListAsync(null, 20, null, cancellation.Token));
        Assert.Equal(late ? 1 : 0, calls);
    }

    private static FlowRunRecord Run(int id) => new()
    {
        Id = id, TemplateId = 1, FlowKey = "Flow/A", FlowName = "Recorded flow", Status = FlowStatus.Completed,
        FinalOutcome = FlowFinalOutcome.Succeeded, ElapsedMs = 12, ContentHash = new string('c', 64), SnapshotId = 7,
        StartedTimeUtc = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc),
        CompletedTimeUtc = new DateTime(2026, 10, 27, 0, 0, 1, DateTimeKind.Utc),
    };
    private static FlowRunQueryPage Page(IReadOnlyList<FlowRunRecord> runs, bool more = false, string? source = null) => new(runs, more, false, source ?? SourceKey);
    private static string? Next(CopilotCapabilityResult result)
    {
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        return document.RootElement.GetProperty("next_cursor").GetString();
    }
    private static byte[] Decode(string cursor)
    {
        var encoded = cursor["frq1:".Length..].Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(encoded + new string('=', (4 - encoded.Length % 4) % 4));
    }
    private static string Encode(string payload) => "frq1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
