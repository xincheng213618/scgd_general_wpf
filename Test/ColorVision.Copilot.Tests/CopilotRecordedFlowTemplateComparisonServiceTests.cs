using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed partial class CopilotRecordedFlowTemplateComparisonServiceTests
{
    [Fact]
    public async Task ComparesRecordedFieldsAndNodeIdentitiesWithoutPublishingValuesOrFieldFingerprints()
    {
        var baseline = Canvas([Node(1, "PRIVATE_BEFORE_VALUE"), Node(3, "PRIVATE_REMOVED_VALUE")]);
        var current = Canvas([Node(1, "PRIVATE_AFTER_VALUE", type: "Missing.Plugin.NewType"), Node(2, "PRIVATE_ADDED_VALUE")]);
        var result = await Service(Pair(baseline, current)).CompareAsync(11, 22, 50, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.False(root.GetProperty("same_recorded_bytes").GetBoolean());
        Assert.Equal(11, root.GetProperty("baseline").GetProperty("run_record_id").GetInt32());
        Assert.Equal(22, root.GetProperty("current").GetProperty("run_record_id").GetInt32());
        Assert.Equal(3, root.GetProperty("counts").GetProperty("node_changes").GetInt32());
        var changes = root.GetProperty("node_changes").EnumerateArray().ToArray();
        Assert.Equal(new[] { "modified", "added", "removed" }, changes.Select(change => change.GetProperty("kind").GetString()));
        Assert.True(changes[0].GetProperty("type_changed").GetBoolean());
        Assert.Contains(changes[0].GetProperty("changed_properties").EnumerateArray(), name => name.GetString() == "Exposure");
        Assert.True(root.GetProperty("coverage").GetProperty("comparison_complete").GetBoolean());
        Assert.DoesNotContain("PRIVATE_", result.Content, StringComparison.Ordinal);
        foreach (var value in new[] { "PRIVATE_BEFORE_VALUE", "PRIVATE_AFTER_VALUE", "PRIVATE_REMOVED_VALUE", "PRIVATE_ADDED_VALUE" })
            Assert.DoesNotContain(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))), result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not proof", root.GetProperty("provenance").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MaximumRowsAndOutputBudgetDoNotTurnOmittedDetailsIntoNoChange()
    {
        var before = Enumerable.Range(1, 200).Select(index => Node(index, "PRIVATE_BEFORE", longNames: true)).ToArray();
        var after = Enumerable.Range(1, 200).Select(index => Node(index, "PRIVATE_AFTER", longNames: true)).ToArray();
        var pair = Pair(Canvas(before), Canvas(after));
        foreach (var maximum in new[] { 1, 200 })
        {
            var result = await Service(pair).CompareAsync(11, 22, maximum, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.InRange(result.Content.Length, 1, CopilotRecordedFlowTemplateComparisonService.MaximumContentCharacters);
            Assert.NotEmpty(result.PartialResultMessage);
            Assert.DoesNotContain("PRIVATE_", result.Content, StringComparison.Ordinal);
            using var document = JsonDocument.Parse(result.Content);
            var root = document.RootElement;
            Assert.Equal(200, root.GetProperty("counts").GetProperty("node_changes").GetInt32());
            var coverage = root.GetProperty("coverage");
            var delivered = root.GetProperty("node_changes").GetArrayLength();
            Assert.InRange(delivered, 0, maximum);
            Assert.True(delivered < 200);
            Assert.Equal(delivered, coverage.GetProperty("detailed_change_rows_returned").GetInt32());
            Assert.True(coverage.GetProperty("details_truncated").GetBoolean());
            Assert.True(coverage.GetProperty("field_names_truncated").GetBoolean());
            Assert.False(coverage.GetProperty("comparison_complete").GetBoolean());
        }
    }

    [Fact]
    public async Task UnresolvedLegacyEdgesStayUnknownEvenWhenThereAreNoModeledNodeChanges()
    {
        var nodes = new[] { Node(1, "unchanged", includeLayout: false), Node(2, "unchanged", includeLayout: false) };
        var result = await Service(Pair(Canvas(nodes, [Pack(0, 1)]), Canvas(nodes, [Pack(1, 0)])))
            .CompareAsync(11, 22, 50, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.False(root.GetProperty("same_recorded_bytes").GetBoolean());
        Assert.Equal(0, root.GetProperty("counts").GetProperty("detailed_change_rows").GetInt32());
        Assert.Empty(root.GetProperty("added_edges").EnumerateArray());
        Assert.Empty(root.GetProperty("removed_edges").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("flags").GetProperty("connection_order_changed").ValueKind);
        Assert.False(root.GetProperty("coverage").GetProperty("connections_comparable").GetBoolean());
        Assert.False(root.GetProperty("coverage").GetProperty("comparison_complete").GetBoolean());
    }

    [Theory]
    [InlineData("flow_key", true)]
    [InlineData("legacy_template_id", false)]
    public async Task IdenticalBytesAreReportedSeparatelyFromRecordedIdentityConfidence(string basis, bool complete)
    {
        var canvas = Canvas([Node(1, "same")]);
        var result = await Service(Pair(canvas, canvas) with { IdentityBasis = basis }).CompareAsync(11, 22, 50, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.True(root.GetProperty("same_recorded_bytes").GetBoolean());
        Assert.Equal(0, root.GetProperty("counts").GetProperty("detailed_change_rows").GetInt32());
        Assert.Equal(complete, root.GetProperty("coverage").GetProperty("comparison_complete").GetBoolean());
        Assert.Equal(basis, root.GetProperty("identity_basis").GetString());
    }

    [Theory]
    [InlineData("different")]
    [InlineData("unavailable")]
    public async Task IncompatibleOrMissingFlowIdentityDoesNotPublishADiff(string basis)
    {
        var canvas = Canvas([Node(1, "same")]);
        var result = await Service(Pair(canvas, canvas) with { IdentityBasis = basis }).CompareAsync(11, 22, 50, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("baseline-run", CopilotToolFailureKind.NotFound)]
    [InlineData("current-run", CopilotToolFailureKind.NotFound)]
    [InlineData("snapshot", CopilotToolFailureKind.NotFound)]
    [InlineData("corrupt", CopilotToolFailureKind.Validation)]
    public async Task MissingOrUnreadableEvidenceIsAFailureRatherThanAnEmptyComparison(string kind, CopilotToolFailureKind expected)
    {
        var canvas = Canvas([Node(1, "same")]);
        var pair = Pair(canvas, canvas);
        switch (kind)
        {
            case "baseline-run": pair = pair with { Baseline = null }; break;
            case "current-run": pair = pair with { Current = null }; break;
            case "snapshot": pair = pair with { Current = pair.Current! with { Snapshot = null } }; break;
            case "corrupt": pair.Current!.Snapshot!.Content = [83, 84, 78, 68, 1]; break;
        }
        var result = await Service(pair).CompareAsync(11, 22, 50, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(expected, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("schema", CopilotToolFailureKind.Validation)]
    [InlineData("missing", CopilotToolFailureKind.NotFound)]
    [InlineData("access", CopilotToolFailureKind.Authorization)]
    [InlineData("busy", CopilotToolFailureKind.Transient)]
    public async Task SourceFailuresDoNotExposePrivatePaths(string kind, CopilotToolFailureKind expected)
    {
        var service = new CopilotRecordedFlowTemplateComparisonService((_, _, _) => Task.FromException<FlowRecordedTemplatePairReadResult>(kind switch
        {
            "schema" => new InvalidDataException("PRIVATE_PATH"), "missing" => new FileNotFoundException("PRIVATE_PATH"),
            "access" => new UnauthorizedAccessException("PRIVATE_PATH"), _ => new IOException("PRIVATE_PATH"),
        }));
        var result = await service.CompareAsync(11, 22, 50, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(expected, result.FailureKind);
        Assert.DoesNotContain("PRIVATE_PATH", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidIdsAndPreCancellationDoNotReadEitherTemplate()
    {
        var calls = 0;
        var service = new CopilotRecordedFlowTemplateComparisonService((_, _, _) => { calls++; throw new InvalidOperationException("Source should not run."); });
        var invalid = await service.CompareAsync(0, 22, 50, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, invalid.FailureKind);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CompareAsync(11, 22, 50, cancellation.Token));
        Assert.Equal(0, calls);
    }

    private static CopilotRecordedFlowTemplateComparisonService Service(FlowRecordedTemplatePairReadResult source) => new((_, _, _) => Task.FromResult(source));
    private static FlowRecordedTemplatePairReadResult Pair(byte[] baseline, byte[] current) => new(Entry(11, 5001, baseline), Entry(22, 5002, current), "flow_key")
    {
        SourceKey = new string('d', 64),
    };
    private static FlowRecordedTemplateReadResult Entry(int runId, long snapshotId, byte[] bytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new(new FlowRunRecord
        {
            Id = runId, TemplateId = 42, FlowKey = "same-flow", FlowName = "Retained flow", TemplateRevision = runId,
            SnapshotId = snapshotId, ContentHash = hash, SerialNumber = "PRIVATE_SERIAL",
        }, new FlowTemplateSnapshot
        {
            Id = snapshotId, TemplateId = 42, FlowKey = "same-flow", TemplateRevision = 1, ContentHash = hash, Content = bytes,
            ContentLength = bytes.Length, CapturedTimeUtc = new(2026, 10, 27, 0, 0, 0),
        }, false);
    }
    private static byte[] Node(int number, string value, string type = "Missing.Plugin.Node", bool longNames = false, bool includeLayout = true)
    {
        var properties = new Dictionary<string, byte[]>
        {
            ["Guid"] = new Guid(number, 0, 0, new byte[8]).ToByteArray(), ["Exposure"] = Encoding.UTF8.GetBytes(value),
        };
        if (longNames)
            for (var index = 0; index < 40; index++) properties["Field" + index + new string('名', 110)] = Encoding.UTF8.GetBytes(value);
        if (includeLayout)
            properties[STNodeSavedState.PortLayoutKey] = JsonSerializer.SerializeToUtf8Bytes(new STNodePortLayout
            {
                Title = longNames ? new string('文', 500) + "😀" : "Saved node " + number,
                Inputs = [new() { Name = "in" }], Outputs = [new() { Name = "out" }],
            });
        return STNodeSavedState.Write("Unloaded.Plugin", type, properties);
    }
    private static long Pack(int output, int input) => ((long)output << 32) | (uint)input;
    private static byte[] Canvas(IReadOnlyList<byte[]> nodes, IReadOnlyList<long>? edges = null)
    {
        using var stream = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(stream, nodes, edges ?? [], 0, 0, 1);
        return stream.ToArray();
    }
}
