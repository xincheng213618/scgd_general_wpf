using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowIncidentQueryServiceTests
{
    private const string SourceKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string DateKey = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private static readonly DateTime Detected = new(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task OutputBudgetContinuesAfterTheLastDeliveredIncidentWithoutLosingRows()
    {
        const string secret = "sk-1234567890abcdef1234567890abcdef";
        var rows = Enumerable.Range(1, 100).Reverse().Select(number => Incident(5_000_000_000L + number)).ToArray();
        foreach (var row in rows)
        {
            row.Summary = secret + new string('中', 580) + "😀";
            row.DetailsJson = "PRIVATE_PAYLOAD";
            row.OperatorName = "PRIVATE_OPERATOR";
            row.AcknowledgmentNote = "PRIVATE_NOTE";
        }
        var service = new CopilotFlowIncidentQueryService((state, maximum, before, _) =>
        {
            Assert.Equal("active", state);
            var start = before.HasValue ? Array.FindIndex(rows, row => row.Id == before.Value) + 1 : 0;
            var remaining = rows.Skip(start).ToArray();
            return Task.FromResult(Page(remaining.Take(maximum).ToArray(), remaining.Length > maximum) with
            {
                AnchorDetectedTimeUtc = before.HasValue ? Detected : null,
            });
        });
        string? cursor = null;
        var delivered = new List<long>();
        var cursors = new HashSet<string>();
        do
        {
            var result = await service.QueryAsync("active", 100, cursor, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.InRange(result.Content.Length, 1, CopilotFlowIncidentQueryService.MaximumContentCharacters);
            Assert.DoesNotContain(secret, result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE_", result.Content, StringComparison.Ordinal);
            Assert.Equal(result.Content, string.Concat(result.Content.EnumerateRunes().Select(rune => rune.ToString())));
            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;
            var page = root.GetProperty("incidents").EnumerateArray().ToArray();
            Assert.NotEmpty(page);
            Assert.Equal(page.Length, root.GetProperty("coverage").GetProperty("incidents_returned").GetInt32());
            if (cursor == null)
            {
                Assert.True(root.GetProperty("coverage").GetProperty("output_limited").GetBoolean());
                Assert.True(root.GetProperty("has_more").GetBoolean());
                Assert.NotEmpty(result.PartialResultMessage);
            }
            delivered.AddRange(page.Select(row => row.GetProperty("incident_id").GetInt64()));
            cursor = root.GetProperty("next_cursor").GetString();
            Assert.Equal(cursor != null, root.GetProperty("has_more").GetBoolean());
            if (cursor != null)
                Assert.True(cursors.Add(cursor), "Continuation must advance after delivered evidence.");
        } while (cursor != null);
        Assert.Equal(rows.Select(row => row.Id), delivered);
    }

    [Fact]
    public async Task CursorBindsStateSourceAndAnchorTimeButAllowsPageSizeChange()
    {
        var calls = 0;
        var source = SourceKey;
        var stamp = Detected;
        var dateKey = DateKey;
        var service = new CopilotFlowIncidentQueryService((state, maximum, before, _) =>
        {
            calls++;
            return Task.FromResult(before == null
                ? Page([Incident(9)], true, source)
                : Page([Incident(8)], false, source) with { AnchorDetectedTimeUtc = stamp, AnchorDetectedTimeFingerprint = dateKey });
        });
        var first = await service.QueryAsync(" ACTIVE ", 1, null, CancellationToken.None);
        var cursor = NextCursor(first);
        var mismatch = await service.QueryAsync("all", 1, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, mismatch.FailureKind);
        Assert.Equal(1, calls);

        source = new string('b', 64);
        var changedSource = await service.QueryAsync("active", 20, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, changedSource.FailureKind);
        Assert.Empty(changedSource.Content);
        source = SourceKey;
        stamp = Detected.AddTicks(1);
        var changedTime = await service.QueryAsync("active", 20, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, changedTime.FailureKind);
        Assert.Empty(changedTime.Content);
        stamp = Detected;
        dateKey = new string('d', 64);
        var changedRepresentation = await service.QueryAsync("active", 20, cursor, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, changedRepresentation.FailureKind);
        Assert.Empty(changedRepresentation.Content);
        dateKey = DateKey;
        var resized = await service.QueryAsync("active", 20, cursor, CancellationToken.None);
        Assert.True(resized.Success, resized.ErrorMessage);
        using var document = JsonDocument.Parse(resized.Content);
        Assert.Equal(8, document.RootElement.GetProperty("incidents")[0].GetProperty("incident_id").GetInt64());
        Assert.Null(document.RootElement.GetProperty("next_cursor").GetString());
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("whitespace")]
    [InlineData("duplicate")]
    [InlineData("wrong-type")]
    [InlineData("version")]
    [InlineData("extra")]
    [InlineData("missing")]
    [InlineData("oversize")]
    public async Task MalformedCursorsAreValidationBeforeSourceAccess(string mutation)
    {
        var calls = 0;
        var service = new CopilotFlowIncidentQueryService((_, _, _, _) =>
        {
            calls++;
            return Task.FromResult(Page([Incident(9)], true));
        });
        var first = await service.QueryAsync("active", 1, null, CancellationToken.None);
        var token = NextCursor(first)!;
        var bytes = token[5..].Replace('-', '+').Replace('_', '/');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(bytes + new string('=', (4 - bytes.Length % 4) % 4)));
        var node = JsonNode.Parse(json)!.AsObject();
        var modified = mutation switch
        {
            "empty" => "",
            "whitespace" => " " + token,
            "duplicate" => Encode(json.Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal)),
            "oversize" => token + new string('a', 4096),
            _ => Modify(),
        };
        string Modify()
        {
            switch (mutation)
            {
                case "wrong-type": node["incident_id"] = "9"; break;
                case "version": node["version"] = 2; break;
                case "extra": node["extra"] = 1; break;
                case "missing": node.Remove("detected_ticks"); break;
            }
            return Encode(node.ToJsonString());
        }
        var result = await service.QueryAsync("active", 1, modified, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Equal(1, calls);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("missing", CopilotToolFailureKind.NotFound)]
    [InlineData("schema", CopilotToolFailureKind.Validation)]
    [InlineData("access", CopilotToolFailureKind.Authorization)]
    [InlineData("busy", CopilotToolFailureKind.Transient)]
    [InlineData("deleted-anchor", CopilotToolFailureKind.Validation)]
    public async Task UnavailableOrStaleSourcesAreFailuresRatherThanSuccessfulEmptyLists(string failure, CopilotToolFailureKind expected)
    {
        var service = new CopilotFlowIncidentQueryService((_, _, _, _) => Task.FromException<FlowIncidentQueryPage>(failure switch
        {
            "missing" => new FileNotFoundException("PRIVATE_DATABASE_PATH"),
            "schema" => new InvalidDataException("PRIVATE_DATABASE_PATH"),
            "access" => new UnauthorizedAccessException("PRIVATE_DATABASE_PATH"),
            "busy" => new IOException("PRIVATE_DATABASE_PATH"),
            _ => new ArgumentException("PRIVATE_DATABASE_PATH"),
        }));
        var result = await service.QueryAsync("active", 20, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(expected, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.DoesNotContain("PRIVATE_DATABASE_PATH", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidEmptyQueryIsSuccessfulAndHasNoContinuation()
    {
        var service = new CopilotFlowIncidentQueryService((_, _, _, _) => Task.FromResult(Page([], false)));
        var result = await service.QueryAsync("active", 20, null, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        Assert.Empty(document.RootElement.GetProperty("incidents").EnumerateArray());
        Assert.False(document.RootElement.GetProperty("has_more").GetBoolean());
        Assert.Null(document.RootElement.GetProperty("next_cursor").GetString());
    }

    [Fact]
    public async Task CancellationIsPreservedBeforeAndAfterReading()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        var service = new CopilotFlowIncidentQueryService((_, _, _, _) =>
        {
            calls++;
            cancellation.Cancel();
            return Task.FromResult(Page([], false));
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryAsync("active", 20, null, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryAsync("active", 20, null, cancellation.Token));
        Assert.Equal(1, calls);
    }

    private static FlowIncident Incident(long id) => new()
    {
        Id = id, RunRecordId = 17, NodeId = "camera", State = "Open", Kind = "NodeFailure", Severity = "Error",
        Summary = "Camera timeout", DetectedTimeUtc = Detected,
    };
    private static FlowIncidentQueryPage Page(IReadOnlyList<FlowIncident> rows, bool hasMore, string sourceKey = SourceKey) =>
        new(rows, hasMore, false, sourceKey)
        {
            DetectedTimeFingerprints = rows.ToDictionary(row => row.Id, _ => DateKey),
            AnchorDetectedTimeUtc = Detected, AnchorDetectedTimeFingerprint = DateKey,
        };
    private static string? NextCursor(CopilotCapabilityResult result)
    {
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        return document.RootElement.GetProperty("next_cursor").GetString();
    }
    private static string Encode(string json) => "fic1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
