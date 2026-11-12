using ColorVision.Engine.Services.FlowDiagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

public sealed partial class CopilotRecordedFlowTemplateComparisonServiceTests
{
    [Fact]
    public async Task ContinuationTraversesNodeAddedEdgeAndRemovedEdgeSegmentsExactlyOnceAndAllowsResizing()
    {
        var pair = Pair(Canvas([Node(1, "before"), Node(2, "removed"), Node(3, "same")], [Pack(1, 2), Pack(3, 4)]),
            Canvas([Node(1, "after"), Node(3, "same"), Node(4, "added")], [Pack(1, 2), Pack(3, 4)]));
        var expected = new[]
        {
            "node:1:modified", "node:2:removed", "node:4:added",
            "added:1:0:3:0", "added:3:0:4:0", "removed:1:0:2:0", "removed:2:0:3:0",
        };
        var service = Service(pair);
        var delivered = new List<string>();
        var cursors = new HashSet<string>();
        string? cursor = null;
        do
        {
            var maximum = cursor == null ? 1 : 2;
            var result = await service.CompareAsync(11, 22, maximum, CancellationToken.None, cursor);
            Assert.True(result.Success, result.ErrorMessage);
            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;
            var rows = RowKeys(root);
            Assert.NotEmpty(rows);
            Assert.InRange(rows.Count, 1, maximum);
            Assert.Equal(delivered.Count, root.GetProperty("coverage").GetProperty("page_offset").GetInt32());
            Assert.Equal(7, root.GetProperty("counts").GetProperty("detailed_change_rows").GetInt32());
            delivered.AddRange(rows);
            cursor = root.GetProperty("next_cursor").GetString();
            Assert.Equal(delivered.Count < expected.Length, root.GetProperty("has_more").GetBoolean());
            if (cursor != null) Assert.True(cursors.Add(cursor), "Continuation must advance.");
        } while (cursor != null);
        Assert.Equal(expected, delivered);
    }

    [Fact]
    public async Task BudgetShrunkPagesAdvanceByDeliveredRowsAndDoNotClaimCompleteFieldNamesAtTheEnd()
    {
        const int count = 12;
        var before = Enumerable.Range(1, count).Select(index => Node(index, "PRIVATE_BEFORE", longNames: true)).ToArray();
        var after = Enumerable.Range(1, count).Select(index => Node(index, "PRIVATE_AFTER", longNames: true)).ToArray();
        var service = Service(Pair(Canvas(before), Canvas(after)));
        var delivered = new List<int>();
        var cursors = new HashSet<string>();
        string? cursor = null;
        do
        {
            var result = await service.CompareAsync(11, 22, 200, CancellationToken.None, cursor);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.InRange(result.Content.Length, 1, CopilotRecordedFlowTemplateComparisonService.MaximumContentCharacters);
            Assert.DoesNotContain("PRIVATE_", result.Content, StringComparison.Ordinal);
            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;
            var coverage = root.GetProperty("coverage");
            Assert.Equal(delivered.Count, coverage.GetProperty("page_offset").GetInt32());
            var page = root.GetProperty("node_changes").EnumerateArray().Select(node => GuidNumber(node.GetProperty("node_id").GetGuid())).ToArray();
            Assert.NotEmpty(page);
            Assert.True(page.Length < count);
            Assert.True(coverage.GetProperty("field_names_truncated").GetBoolean());
            Assert.False(coverage.GetProperty("comparison_complete").GetBoolean());
            Assert.Equal(count, root.GetProperty("counts").GetProperty("detailed_change_rows").GetInt32());
            delivered.AddRange(page);
            cursor = root.GetProperty("next_cursor").GetString();
            Assert.Equal(delivered.Count < count, root.GetProperty("has_more").GetBoolean());
            if (cursor != null) Assert.True(cursors.Add(cursor));
        } while (cursor != null);
        Assert.Equal(Enumerable.Range(1, count), delivered);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("reference")]
    [InlineData("content")]
    [InlineData("identity")]
    [InlineData("missing-baseline")]
    [InlineData("missing-current")]
    [InlineData("missing-snapshot")]
    public async Task ChangedEvidenceBindingsRejectTheOldCursorWithoutPublishingAReplacementPage(string change)
    {
        var pair = Pair(Canvas([Node(1, "before"), Node(2, "before")]), Canvas([Node(1, "after"), Node(2, "after")]));
        var service = new CopilotRecordedFlowTemplateComparisonService((_, _, _) => Task.FromResult(pair));
        var first = await service.CompareAsync(11, 22, 1, CancellationToken.None);
        var cursor = NextCursor(first);
        switch (change)
        {
            case "source": pair = pair with { SourceKey = new string('e', 64) }; break;
            case "reference": pair.Current!.Run.SnapshotId = 6000; pair.Current.Snapshot!.Id = 6000; break;
            case "content":
                var bytes = Canvas([Node(1, "new after"), Node(2, "after")]);
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                pair.Current!.Run.ContentHash = hash;
                pair.Current.Snapshot!.Content = bytes;
                pair.Current.Snapshot.ContentLength = bytes.Length;
                pair.Current.Snapshot.ContentHash = hash;
                break;
            case "identity": pair = pair with { IdentityBasis = "legacy_template_id" }; break;
            case "missing-baseline": pair = pair with { Baseline = null }; break;
            case "missing-current": pair = pair with { Current = null }; break;
            case "missing-snapshot": pair = pair with { Current = pair.Current! with { Snapshot = null } }; break;
        }
        var stale = await service.CompareAsync(11, 22, 2, CancellationToken.None, cursor);
        Assert.False(stale.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, stale.FailureKind);
        Assert.Empty(stale.Content);
        Assert.Contains("Restart", stale.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SwappedRunIdsAreRejectedBeforeReadingStorage()
    {
        var calls = 0;
        var pair = Pair(Canvas([Node(1, "before"), Node(2, "before")]), Canvas([Node(1, "after"), Node(2, "after")]));
        var service = new CopilotRecordedFlowTemplateComparisonService((_, _, _) => { calls++; return Task.FromResult(pair); });
        var cursor = NextCursor(await service.CompareAsync(11, 22, 1, CancellationToken.None));
        var swapped = await service.CompareAsync(22, 11, 1, CancellationToken.None, cursor);
        Assert.Equal(CopilotToolFailureKind.Validation, swapped.FailureKind);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("whitespace")]
    [InlineData("duplicate")]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("version")]
    [InlineData("offset-type")]
    [InlineData("offset-negative")]
    [InlineData("oversize")]
    public async Task MalformedComparisonCursorsAreValidationBeforeReadingStorage(string mutation)
    {
        var calls = 0;
        var pair = Pair(Canvas([Node(1, "before"), Node(2, "before")]), Canvas([Node(1, "after"), Node(2, "after")]));
        var service = new CopilotRecordedFlowTemplateComparisonService((_, _, _) => { calls++; return Task.FromResult(pair); });
        var cursor = NextCursor(await service.CompareAsync(11, 22, 1, CancellationToken.None))!;
        var json = CursorJson(cursor);
        var node = JsonNode.Parse(json)!.AsObject();
        var invalid = mutation switch
        {
            "empty" => "", "whitespace" => " " + cursor,
            "duplicate" => CursorText(json.Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal)),
            "oversize" => cursor + new string('a', 4096), _ => Changed(),
        };
        string Changed()
        {
            switch (mutation)
            {
                case "missing": node.Remove("source_key"); break;
                case "extra": node["extra"] = 1; break;
                case "version": node["version"] = 2; break;
                case "offset-type": node["offset"] = "1"; break;
                case "offset-negative": node["offset"] = -1; break;
            }
            return CursorText(node.ToJsonString());
        }
        var result = await service.CompareAsync(11, 22, 1, CancellationToken.None, invalid);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Equal(1, calls);
        Assert.Empty(result.Content);
    }

    [Fact]
    public async Task AnOffsetAtTheEndCannotPretendToBeAnEmptyCompleteComparison()
    {
        var pair = Pair(Canvas([Node(1, "before"), Node(2, "before")]), Canvas([Node(1, "after"), Node(2, "after")]));
        var service = Service(pair);
        var cursor = NextCursor(await service.CompareAsync(11, 22, 1, CancellationToken.None))!;
        var node = JsonNode.Parse(CursorJson(cursor))!.AsObject();
        node["offset"] = 2;
        var result = await service.CompareAsync(11, 22, 1, CancellationToken.None, CursorText(node.ToJsonString()));
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Empty(result.Content);
    }

    private static List<string> RowKeys(JsonElement root)
    {
        var result = root.GetProperty("node_changes").EnumerateArray()
            .Select(node => $"node:{GuidNumber(node.GetProperty("node_id").GetGuid())}:{node.GetProperty("kind").GetString()}").ToList();
        foreach (var (property, prefix) in new[] { ("added_edges", "added"), ("removed_edges", "removed") })
            result.AddRange(root.GetProperty(property).EnumerateArray().Select(edge =>
                $"{prefix}:{GuidNumber(edge.GetProperty("source_node_id").GetGuid())}:{edge.GetProperty("source_port_index").GetInt32()}:{GuidNumber(edge.GetProperty("target_node_id").GetGuid())}:{edge.GetProperty("target_port_index").GetInt32()}"));
        return result;
    }
    private static int GuidNumber(Guid value) => BitConverter.ToInt32(value.ToByteArray(), 0);
    private static string? NextCursor(CopilotCapabilityResult result)
    {
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        return document.RootElement.GetProperty("next_cursor").GetString();
    }
    private static string CursorJson(string cursor)
    {
        var encoded = cursor[6..].Replace('-', '+').Replace('_', '/');
        return Encoding.UTF8.GetString(Convert.FromBase64String(encoded + new string('=', (4 - encoded.Length % 4) % 4)));
    }
    private static string CursorText(string json) => "cvtc1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
