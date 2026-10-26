using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Services.FlowDiagnostics;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecordedFlowTemplateServiceTests
{
    [Fact]
    public async Task HistoricalTemplateHasItsOwnProvenanceAndKeepsRunAndCapturedRevisionsDistinct()
    {
        var retained = CreateResult(3);
        var service = Service(retained);
        var result = await service.InspectAsync(17, null, 80, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        Assert.Equal(8, root.GetProperty("run").GetProperty("template_revision").GetInt32());
        Assert.Equal(7, root.GetProperty("snapshot").GetProperty("captured_template_revision").GetInt32());
        Assert.Equal(5_000_000_001, root.GetProperty("snapshot").GetProperty("snapshot_id").GetInt64());
        Assert.True(root.GetProperty("snapshot").GetProperty("stored_integrity_verified").GetBoolean());
        Assert.Contains("unsaved", root.GetProperty("provenance").GetString(), StringComparison.Ordinal);
        Assert.Contains("current graph Revision", root.GetProperty("provenance").GetString(), StringComparison.Ordinal);
        Assert.Equal(3, root.GetProperty("nodes").GetArrayLength());
        Assert.Equal(2, root.GetProperty("edges").GetArrayLength());
        Assert.True(root.GetProperty("coverage").GetProperty("returned_window_complete").GetBoolean());
        Assert.DoesNotContain("PRIVATE_", result.Content, StringComparison.Ordinal);
        Assert.Contains(root.GetProperty("nodes")[0].GetProperty("property_names").EnumerateArray(), value => value.GetString() == "PrivateValue");
    }

    [Fact]
    public async Task ExactNodeFocusIncludesOnlySavedDirectNeighborsAndKeepsTheFocusWhenBounded()
    {
        var retained = CreateResult(5);
        var result = await Service(retained).InspectAsync(17, NodeId(3).ToString("D"), 2, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        var nodes = root.GetProperty("nodes").EnumerateArray().ToArray();
        Assert.Equal(new[] { NodeId(3), NodeId(2) }, nodes.Select(node => node.GetProperty("node_id").GetGuid()));
        Assert.Single(root.GetProperty("edges").EnumerateArray());
        var coverage = root.GetProperty("coverage");
        Assert.Equal("focus_and_adjacent_nodes", coverage.GetProperty("scope").GetString());
        Assert.Equal(3, coverage.GetProperty("candidate_nodes_total").GetInt32());
        Assert.True(coverage.GetProperty("nodes_truncated").GetBoolean());
        Assert.False(coverage.GetProperty("returned_window_complete").GetBoolean());
        var absent = await Service(retained).InspectAsync(17, NodeId(99).ToString("D"), 80, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.NotFound, absent.FailureKind);
        Assert.Empty(absent.Content);
    }

    [Fact]
    public async Task OutputBudgetAndPropertyNameLimitsKeepWholeJsonAndDeclarePartialEvidence()
    {
        var retained = CreateResult(200, longLabels: true);
        var result = await Service(retained).InspectAsync(17, null, 200, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.InRange(result.Content.Length, 1, CopilotRecordedFlowTemplateInspectionService.MaximumContentCharacters);
        Assert.NotEmpty(result.PartialResultMessage);
        Assert.DoesNotContain("sk-1234567890abcdef1234567890abcdef", result.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_", result.Content, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(result.Content);
        var root = document.RootElement;
        var nodes = root.GetProperty("nodes").EnumerateArray().ToArray();
        Assert.NotEmpty(nodes);
        Assert.True(nodes.Length < 200);
        var ids = nodes.Select(node => node.GetProperty("node_id").GetGuid()).ToHashSet();
        foreach (var edge in root.GetProperty("edges").EnumerateArray())
        {
            Assert.Contains(edge.GetProperty("source_node_id").GetGuid(), ids);
            Assert.Contains(edge.GetProperty("target_node_id").GetGuid(), ids);
        }
        foreach (var node in nodes)
        {
            var title = node.GetProperty("title").GetString()!;
            Assert.Equal(title, string.Concat(title.EnumerateRunes().Select(rune => rune.ToString())));
            Assert.InRange(node.GetProperty("property_names").GetArrayLength(), 1, 32);
        }
        var coverage = root.GetProperty("coverage");
        Assert.True(coverage.GetProperty("nodes_truncated").GetBoolean());
        Assert.True(coverage.GetProperty("property_names_truncated").GetBoolean());
        Assert.True(coverage.GetProperty("text_truncated").GetBoolean());
        Assert.False(coverage.GetProperty("returned_window_complete").GetBoolean());
    }

    [Fact]
    public async Task MissingSavedPortLayoutNeverFallsBackToCurrentNodeTypesOrInventsConnections()
    {
        var result = await Service(CreateResult(3, includeLayout: false)).InspectAsync(17, null, 80, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        using var document = JsonDocument.Parse(result.Content);
        Assert.Empty(document.RootElement.GetProperty("edges").EnumerateArray());
        Assert.False(document.RootElement.GetProperty("coverage").GetProperty("connections_complete").GetBoolean());
        Assert.False(document.RootElement.GetProperty("coverage").GetProperty("returned_window_complete").GetBoolean());
    }

    [Theory]
    [InlineData("no-run", CopilotToolFailureKind.NotFound)]
    [InlineData("no-reference", CopilotToolFailureKind.NotFound)]
    [InlineData("missing-row", CopilotToolFailureKind.NotFound)]
    [InlineData("empty", CopilotToolFailureKind.Validation)]
    [InlineData("corrupt", CopilotToolFailureKind.Validation)]
    public async Task MissingAndUnreadableRecordedEvidenceNeverUsesTheCurrentCanvas(string kind, CopilotToolFailureKind expected)
    {
        var retained = CreateResult(1);
        FlowRecordedTemplateReadResult? source = retained;
        switch (kind)
        {
            case "no-run": source = null; break;
            case "no-reference": retained.Run.SnapshotId = null; source = retained with { Snapshot = null }; break;
            case "missing-row": source = retained with { Snapshot = null }; break;
            case "empty": retained.Snapshot!.Content = []; break;
            case "corrupt": retained.Snapshot!.Content = [83, 84, 78, 68, 1]; break;
        }
        var result = await Service(source).InspectAsync(17, null, 80, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(expected, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("schema", CopilotToolFailureKind.Validation)]
    [InlineData("missing", CopilotToolFailureKind.NotFound)]
    [InlineData("access", CopilotToolFailureKind.Authorization)]
    [InlineData("busy", CopilotToolFailureKind.Transient)]
    public async Task SourceFailuresAreClassifiedWithoutPublishingPrivatePaths(string kind, CopilotToolFailureKind expected)
    {
        var service = new CopilotRecordedFlowTemplateInspectionService((_, _) => Task.FromException<FlowRecordedTemplateReadResult?>(kind switch
        {
            "schema" => new InvalidDataException("PRIVATE_PATH"),
            "missing" => new FileNotFoundException("PRIVATE_PATH"),
            "access" => new UnauthorizedAccessException("PRIVATE_PATH"),
            _ => new IOException("PRIVATE_PATH"),
        }));
        var result = await service.InspectAsync(17, null, 80, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(expected, result.FailureKind);
        Assert.DoesNotContain("PRIVATE_PATH", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Empty(result.Content);
    }

    [Fact]
    public async Task InvalidNodeIdentityAndCancellationDoNotPublishEvidence()
    {
        var calls = 0;
        var service = new CopilotRecordedFlowTemplateInspectionService((_, _) => { calls++; return Task.FromResult<FlowRecordedTemplateReadResult?>(CreateResult(1)); });
        foreach (var nodeId in new[] { "camera", Guid.Empty.ToString("D"), " " + NodeId(1).ToString("D") })
        {
            var invalid = await service.InspectAsync(17, nodeId, 80, CancellationToken.None);
            Assert.Equal(CopilotToolFailureKind.Validation, invalid.FailureKind);
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.InspectAsync(17, null, 80, cancellation.Token));
        Assert.Equal(0, calls);
    }

    private static CopilotRecordedFlowTemplateInspectionService Service(FlowRecordedTemplateReadResult? source) => new((_, _) => Task.FromResult(source));
    private static Guid NodeId(int number) => new(number, 0, 0, new byte[8]);
    private static FlowRecordedTemplateReadResult CreateResult(int count, bool longLabels = false, bool includeLayout = true)
    {
        var payloads = Enumerable.Range(1, count).Select(number =>
        {
            var properties = new Dictionary<string, byte[]>
            {
                ["Guid"] = NodeId(number).ToByteArray(), ["PrivateValue"] = Encoding.UTF8.GetBytes("PRIVATE_PARAMETER_VALUE"),
            };
            if (longLabels)
                for (var index = 0; index < 40; index++) properties["Property" + index + new string('名', 110)] = [1];
            if (includeLayout)
                properties[STNodeSavedState.PortLayoutKey] = JsonSerializer.SerializeToUtf8Bytes(new STNodePortLayout
                {
                    Title = longLabels ? "sk-1234567890abcdef1234567890abcdef " + new string('文', 500) : "Saved node " + number,
                    Inputs = [new() { Name = "in" }], Outputs = [new() { Name = "out" }],
                });
            return STNodeSavedState.Write("Unloaded.Plugin", "Missing.Plugin.Node" + number, properties);
        }).ToArray();
        var connections = Enumerable.Range(0, Math.Max(0, count - 1)).Select(number => ((long)(number * 2 + 1) << 32) | (uint)(number * 2 + 2)).ToArray();
        using var stream = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(stream, payloads, connections, 0, 0, 1);
        var bytes = stream.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new(new FlowRunRecord
        {
            Id = 17, TemplateId = 43, FlowKey = "stable-flow", RunKey = "retained-run", FlowName = "Recorded flow",
            TemplateRevision = 8, ContentHash = hash, SnapshotId = 5_000_000_001, SerialNumber = "PRIVATE_SERIAL",
        }, new FlowTemplateSnapshot
        {
            Id = 5_000_000_001, TemplateId = 42, FlowKey = "stable-flow", TemplateRevision = 7, ContentHash = hash,
            Content = bytes, ContentLength = bytes.Length, CapturedTimeUtc = new(2026, 10, 27, 0, 0, 0),
        }, false);
    }
}
