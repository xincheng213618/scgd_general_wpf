using ColorVision.Engine;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLoadedBatchResultsServiceTests
{
    internal static readonly string Registry = new('a', 32);

    [Fact]
    public async Task BudgetedResultPagesAdvanceOnlyAfterDeliveredObjectReferencesWithoutDeduplicatingDatabaseIds()
    {
        var rows = Enumerable.Range(1, 100).Select(index => Row(index) with
        {
            Kind = index % 2 == 0 ? "algorithm" : "image", TemplateName = "password=\"alpha, beta\"; " + new string('中', 256) + "😀",
        }).Reverse().ToArray();
        var service = Service(Snapshot([Source(1, rows)]));
        var delivered = new List<string>();
        var cursors = new HashSet<string>();
        string? cursor = null;
        do
        {
            var result = await service.InspectAsync(Ref(1), Registry, null, cursor == null ? 100 : 7, cursor, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.InRange(result.Content.Length, 1, CopilotLoadedBatchResultsInspectionService.MaximumContentCharacters);
            Assert.DoesNotContain("alpha", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("beta", result.Content, StringComparison.Ordinal);
            Assert.Equal(result.Content, string.Concat(result.Content.EnumerateRunes().Select(rune => rune.ToString())));
            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;
            var page = root.GetProperty("results").EnumerateArray().ToArray();
            Assert.NotEmpty(page);
            Assert.Equal(100, root.GetProperty("total_results").GetInt32());
            Assert.Equal(100, root.GetProperty("matched_results").GetInt32());
            var refs = page.Select(row => row.GetProperty("result_ref").GetString()!).ToArray();
            Assert.Equal(refs.Order(StringComparer.Ordinal), refs);
            Assert.All(page, row => Assert.Equal(1, row.GetProperty("internal_id").GetInt32()));
            Assert.Equal(page.Length, root.GetProperty("coverage").GetProperty("returned_results").GetInt32());
            if (cursor == null) Assert.True(root.GetProperty("coverage").GetProperty("output_limited").GetBoolean());
            Assert.True(root.GetProperty("coverage").GetProperty("text_redacted").GetBoolean());
            Assert.NotEmpty(result.PartialResultMessage);
            delivered.AddRange(refs);
            cursor = root.GetProperty("next_cursor").GetString();
            Assert.Equal(cursor != null, root.GetProperty("has_more").GetBoolean());
            if (cursor != null) Assert.True(cursors.Add(cursor));
        } while (cursor != null);
        Assert.Equal(rows.Select(row => row.ResultRef).Order(StringComparer.Ordinal), delivered);
        Assert.Equal(rows.Length, delivered.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task DirectoryAndResultCursorsBindTheirOwnMembershipWhileFocusNeverFallsBackToRecordedIds()
    {
        var snapshot = Snapshot([Source(1, [Row(1), Row(2)]), Source(2, [])]);
        var calls = 0;
        var service = new CopilotLoadedBatchResultsInspectionService(_ => { calls++; return Task.FromResult(snapshot); });
        var directoryCursor = Next(await service.InspectAsync(null, null, null, 1, null, CancellationToken.None))!;
        var rowCursor = Next(await service.InspectAsync(Ref(1), Registry, null, 1, null, CancellationToken.None))!;
        var captured = calls;
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(Ref(1), Registry, null, 100, directoryCursor, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(null, null, null, 100, rowCursor, CancellationToken.None)).FailureKind);
        Assert.Equal(captured, calls);
        snapshot = snapshot with { Sources = [Source(1, [Row(1), Row(2) with { ResultCode = 123, TemplateName = "Updated metadata" }]), Source(2, [])] };
        Assert.True((await service.InspectAsync(Ref(1), Registry, null, 100, rowCursor, CancellationToken.None)).Success);
        Assert.True((await service.InspectAsync(null, Registry, null, 100, directoryCursor, CancellationToken.None)).Success);
        snapshot = snapshot with { Sources = [snapshot.Sources[0], snapshot.Sources[1], Source(4, [])] };
        Assert.True((await service.InspectAsync(Ref(1), Registry, null, 100, rowCursor, CancellationToken.None)).Success);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(null, Registry, null, 100, directoryCursor, CancellationToken.None)).FailureKind);
        snapshot = snapshot with { Sources = [Source(1, [Row(1), Row(3)]), Source(2, [])] };
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(Ref(1), Registry, null, 100, rowCursor, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.NotFound, (await service.InspectAsync(Ref(1), Registry, Ref(2), 40, null, CancellationToken.None)).FailureKind);
        var focused = await service.InspectAsync(Ref(1), Registry, Ref(3), 100, null, CancellationToken.None);
        Assert.True(focused.Success);
        using (var json = JsonDocument.Parse(focused.Content))
        {
            Assert.Equal("focus", json.RootElement.GetProperty("mode").GetString());
            Assert.Equal(Ref(3), Assert.Single(json.RootElement.GetProperty("results").EnumerateArray()).GetProperty("result_ref").GetString());
            Assert.Null(json.RootElement.GetProperty("next_cursor").GetString());
        }
        snapshot = snapshot with { Sources = [Source(3, [Row(1), Row(3)]), Source(2, [])] };
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(null, Registry, null, 40, directoryCursor, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.NotFound, (await service.InspectAsync(Ref(1), Registry, null, 40, null, CancellationToken.None)).FailureKind);
        snapshot = snapshot with { RegistryInstanceId = new string('b', 32) };
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(Ref(3), Registry, Ref(3), 40, null, CancellationToken.None)).FailureKind);
    }

    [Fact]
    public async Task RawCodesDurationsAndDateKindsArePreservedWithoutCustomerOrFileJudgments()
    {
        var date = new DateTime(2026, 10, 27, 2, 3, 4, DateTimeKind.Unspecified);
        var first = Row(1) with { DurationMs = 3_700_001, CreatedAt = date, ResultCode = 0 };
        var second = Row(2) with { Kind = "algorithm", DurationMs = -17, CreatedAt = null, ResultCode = null };
        var result = await Service(Snapshot([Source(1, [first, second])])).InspectAsync(Ref(1), Registry, null, 40, null, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotEmpty(result.PartialResultMessage);
        using var document = JsonDocument.Parse(result.Content);
        var rows = document.RootElement.GetProperty("results");
        Assert.Equal(3_700_001, rows[0].GetProperty("recorded_duration_ms").GetInt64());
        Assert.Equal(3_700_001, rows[0].GetProperty("duration_ms").GetInt64());
        Assert.Equal("Unspecified", rows[0].GetProperty("created_at_kind").GetString());
        Assert.Equal("2026-10-27T02:03:04", rows[0].GetProperty("created_at").GetString());
        Assert.Equal(0, rows[0].GetProperty("result_code").GetInt32());
        Assert.Equal(-17, rows[1].GetProperty("recorded_duration_ms").GetInt64());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("duration_ms").ValueKind);
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("result_code").ValueKind);
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("created_at").ValueKind);
        Assert.Equal(1, document.RootElement.GetProperty("coverage").GetProperty("invalid_duration_rows").GetInt32());
        foreach (var row in rows.EnumerateArray())
        {
            Assert.False(row.TryGetProperty("customer_pass", out _));
            Assert.False(row.TryGetProperty("file_exists", out _));
            Assert.False(row.TryGetProperty("path", out _));
            Assert.False(row.TryGetProperty("payload", out _));
        }
    }

    [Fact]
    public async Task IllegalSelectorsAndMalformedCursorsNeverCaptureTheRegistry()
    {
        var calls = 0;
        var service = new CopilotLoadedBatchResultsInspectionService(_ => { calls++; return Task.FromResult(Snapshot([Source(1, []), Source(2, [])])); });
        var cursor = Next(await service.InspectAsync(null, null, null, 1, null, CancellationToken.None))!;
        calls = 0;
        var body = JsonNode.Parse(Decode(cursor))!.AsObject();
        body["version"] = 2;
        var unknown = Encode(body.ToJsonString());
        var duplicate = Encode(Encoding.UTF8.GetString(Decode(cursor)).Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal));
        foreach (var invalid in new[] { "", " ", cursor + "=", " " + cursor, new string('c', 4097), unknown, duplicate })
            Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(null, null, null, 40, invalid, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(Ref(1), null, null, 40, null, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(null, Registry, Ref(1), 40, null, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(Ref(1), Registry, Ref(1), 40, cursor, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync("alias", Registry, null, 40, null, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(null, null, null, 0, null, CancellationToken.None)).FailureKind);
        Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(null, null, null, 101, null, CancellationToken.None)).FailureKind);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("registry")]
    [InlineData("source-ref")]
    [InlineData("source-duplicate")]
    [InlineData("row-duplicate")]
    [InlineData("kind")]
    [InlineData("row-ref")]
    public async Task InconsistentSourceOrRowIdentityIsRejectedBeforePublication(string kind)
    {
        var source = kind switch
        {
            "source-ref" => Source(1, []) with { SourceRef = "invalid" },
            "row-duplicate" => Source(1, [Row(1), Row(1)]),
            "kind" => Source(1, [Row(1) with { Kind = "plugin-payload" }]),
            "row-ref" => Source(1, [Row(1) with { ResultRef = "invalid" }]),
            _ => Source(1, [Row(1)]),
        };
        var snapshot = kind == "source-duplicate" ? Snapshot([source, source]) : Snapshot([source]);
        if (kind == "registry") snapshot = snapshot with { RegistryInstanceId = "invalid" };
        var result = await Service(snapshot).InspectAsync(null, null, null, 40, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("changed", CopilotToolFailureKind.Validation)]
    [InlineData("invalid", CopilotToolFailureKind.Internal)]
    [InlineData("unavailable", CopilotToolFailureKind.NotFound)]
    [InlineData("timeout", CopilotToolFailureKind.Transient)]
    [InlineData("denied", CopilotToolFailureKind.Authorization)]
    public async Task FailuresKeepTheirClassificationAndDoNotPublishPrivateDetails(string kind, CopilotToolFailureKind expected)
    {
        var service = new CopilotLoadedBatchResultsInspectionService(_ => throw (kind switch
        {
            "changed" => (Exception)new CopilotLoadedBatchResultsSourceChangedException("PRIVATE_DETAIL"),
            "invalid" => new CopilotLoadedBatchResultsSourceInvalidException("PRIVATE_DETAIL"),
            "unavailable" => new CopilotLoadedBatchResultsSourceUnavailableException("PRIVATE_DETAIL"),
            "timeout" => new TimeoutException("PRIVATE_DETAIL"),
            _ => new UnauthorizedAccessException("PRIVATE_DETAIL"),
        }));
        var result = await service.InspectAsync(null, null, null, 40, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(expected, result.FailureKind);
        Assert.DoesNotContain("PRIVATE_DETAIL", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Content);
        if (kind == "unavailable")
            Assert.Equal(CopilotToolFailureKind.Validation, (await service.InspectAsync(Ref(1), Registry, null, 40, null, CancellationToken.None)).FailureKind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-17)]
    public async Task SignedBatchMetadataNeverReplacesTheExactSourceIdentity(int batchId)
    {
        var service = Service(Snapshot([Source(1, [Row(1), Row(2)]) with { BatchId = batchId }, Source(2, [Row(3)]) with { BatchId = batchId }]));
        var directory = await service.InspectAsync(null, null, null, 40, null, CancellationToken.None);
        Assert.True(directory.Success);
        using (var document = JsonDocument.Parse(directory.Content))
            Assert.All(document.RootElement.GetProperty("sources").EnumerateArray(), row => Assert.Equal(batchId, row.GetProperty("batch_id").GetInt32()));
        var first = await service.InspectAsync(Ref(1), Registry, null, 1, null, CancellationToken.None);
        var continuation = await service.InspectAsync(Ref(1), Registry, null, 100, Next(first), CancellationToken.None);
        Assert.True(continuation.Success, continuation.ErrorMessage);
        using var parsed = JsonDocument.Parse(continuation.Content);
        Assert.Equal(batchId, parsed.RootElement.GetProperty("batch_id").GetInt32());
        Assert.Equal(Ref(2), Assert.Single(parsed.RootElement.GetProperty("results").EnumerateArray()).GetProperty("result_ref").GetString());
        Assert.Equal(CopilotToolFailureKind.NotFound, (await service.InspectAsync(Ref(1), Registry, Ref(3), 40, null, CancellationToken.None)).FailureKind);
    }

    [Fact]
    public async Task AnAvailableEmptyDirectoryDoesNotMeanAnUnavailableRegistry()
    {
        var result = await Service(Snapshot([])).InspectAsync(null, null, null, 40, null, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Null(Next(result));
        using var document = JsonDocument.Parse(result.Content);
        Assert.Equal(0, document.RootElement.GetProperty("total_sources").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("sources").GetArrayLength());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeOrAfterCaptureCannotPublishALateWindow(bool late)
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var service = new CopilotLoadedBatchResultsInspectionService(token =>
        {
            calls++;
            Assert.Equal(cancellation.Token, token);
            cancellation.Cancel();
            return Task.FromResult(Snapshot([Source(1, [Row(1)])]));
        });
        if (!late) cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InspectAsync(null, null, null, 40, null, cancellation.Token));
        Assert.Equal(late ? 1 : 0, calls);
    }

    internal static string Ref(int id) => id.ToString("x32");
    internal static CopilotLoadedBatchResultRow Row(int reference) => new(Ref(reference), "image", 1, 0, "Loaded template", 0, 0,
        new DateTime(2026, 10, 27, 1, 2, 3, DateTimeKind.Unspecified));
    internal static CopilotLoadedBatchSourceSnapshot Source(int reference, IReadOnlyList<CopilotLoadedBatchResultRow> rows) => new(Ref(reference), 17, rows);
    internal static CopilotLoadedBatchResultsSnapshot Snapshot(IReadOnlyList<CopilotLoadedBatchSourceSnapshot> sources) => new(Registry,
        new DateTimeOffset(2026, 10, 27, 1, 0, 0, TimeSpan.Zero), sources);
    private static CopilotLoadedBatchResultsInspectionService Service(CopilotLoadedBatchResultsSnapshot snapshot) => new(_ => Task.FromResult(snapshot));
    private static string? Next(CopilotCapabilityResult result)
    {
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        return document.RootElement.GetProperty("next_cursor").GetString();
    }
    private static byte[] Decode(string value)
    {
        var encoded = value["cvlbr1:".Length..].Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(encoded + new string('=', (4 - encoded.Length % 4) % 4));
    }
    private static string Encode(string json) => "cvlbr1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
