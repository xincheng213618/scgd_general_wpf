using ColorVision.Engine.Services.FlowDiagnostics;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecordedFlowTemplateGraphTests
{
    private static readonly Guid FirstId = Guid.Parse("41264415-70af-4af3-bf90-c8844bff1a01");
    private static readonly Guid SecondId = Guid.Parse("41264415-70af-4af3-bf90-c8844bff1a02");
    private const string MissingModel = "Definitely.Not.Installed.CopilotGraphFixture, Version=98.0.0.0";
    private const string MissingType = "unavailable-plugin-type-identity";

    [Fact]
    public void UnknownPluginsUseOnlySavedMetadataAndSharedEmptyPortIndices()
    {
        var first = Node(FirstId, Layout([Port(empty: true), Port()], [Port(), Port(empty: true)], "Saved first title"), properties =>
        {
            properties["Vendor.Secret"] = Encoding.UTF8.GetBytes("PRIVATE_PROPERTY_VALUE");
            properties["Title"] = Encoding.UTF8.GetBytes("PRIVATE_UNRELATED_TITLE_VALUE");
        });
        var second = Node(SecondId, Layout([Port(empty: true), Port()], [Port(empty: true), Port()], "Saved second title"));
        // Empty is one shared global option. Index order is node inputs, then node outputs.
        // Global indices: empty=0, first input[1]=1, first output[0]=2, second input[1]=3, second output[1]=4.
        var content = Canvas([first, second], Pack(2, 3), Pack(4, 1));
        var original = content.ToArray();

        var graph = RecordedFlowTemplateGraphReader.Read(content, CancellationToken.None);

        Assert.Equal(original, content);
        Assert.Equal(2, graph.Nodes.Count);
        Assert.Equal(MissingModel, graph.Nodes[0].ModelKey);
        Assert.Equal(MissingType, graph.Nodes[0].TypeKey);
        Assert.Equal("Saved first title", graph.Nodes[0].Title);
        Assert.Contains("Vendor.Secret", graph.Nodes[0].PropertyNames);
        Assert.Contains("Title", graph.Nodes[0].PropertyNames);
        Assert.Equal(new[]
        {
            new RecordedFlowTemplateEdge(FirstId, 0, SecondId, 1),
            new RecordedFlowTemplateEdge(SecondId, 1, FirstId, 1),
        }, graph.Edges);
        Assert.Equal(2, graph.TotalConnectionCount);
        Assert.True(graph.ConnectionsComplete);
        var publicMetadata = JsonSerializer.Serialize(graph);
        Assert.DoesNotContain("PRIVATE_PROPERTY_VALUE", publicMetadata, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_UNRELATED_TITLE_VALUE", publicMetadata, StringComparison.Ordinal);
        Assert.True(Assert.IsAssignableFrom<IList<RecordedFlowTemplateNode>>(graph.Nodes).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<RecordedFlowTemplateEdge>>(graph.Edges).IsReadOnly);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("short")]
    [InlineData("duplicate")]
    public void InvalidNodeGuidsAreRejectedWithoutResolvingNodeTypes(string scenario)
    {
        var first = Node(FirstId, edit: properties =>
        {
            switch (scenario)
            {
                case "missing": properties.Remove("Guid"); break;
                case "empty": properties["Guid"] = Guid.Empty.ToByteArray(); break;
                case "short": properties["Guid"] = new byte[15]; break;
            }
        });
        var nodes = scenario == "duplicate" ? new[] { first, Node(FirstId) } : new[] { first };

        Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateGraphReader.Read(Canvas(nodes), CancellationToken.None));
    }

    [Theory]
    [InlineData("missing-layout")]
    [InlineData("placeholder")]
    [InlineData("archive")]
    public void MissingOrLegacyPortSchemasNeverGuessConnections(string scenario)
    {
        var first = Node(FirstId, Layout([], [Port()]));
        var second = Node(SecondId, scenario == "missing-layout" ? null : Layout([Port()], []), properties =>
        {
            if (scenario == "placeholder")
                properties[STNodeSavedState.LegacyPlaceholderKey] = Array.Empty<byte>();
            if (scenario == "archive")
                properties["__STNode.LegacyConnections"] = Encoding.UTF8.GetBytes("PRIVATE_OPAQUE_LEGACY_ARCHIVE");
        });

        var withConnection = RecordedFlowTemplateGraphReader.Read(Canvas([first, second], Pack(0, 1)), CancellationToken.None);
        var withoutPackedConnections = RecordedFlowTemplateGraphReader.Read(Canvas([first, second]), CancellationToken.None);

        Assert.Empty(withConnection.Edges);
        Assert.Equal(1, withConnection.TotalConnectionCount);
        Assert.False(withConnection.ConnectionsComplete);
        Assert.Empty(withoutPackedConnections.Edges);
        Assert.Equal(0, withoutPackedConnections.TotalConnectionCount);
        Assert.Equal(scenario == "missing-layout", withoutPackedConnections.ConnectionsComplete);
        Assert.DoesNotContain("PRIVATE_OPAQUE_LEGACY_ARCHIVE", JsonSerializer.Serialize(withConnection), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("source-direction")]
    [InlineData("target-direction")]
    [InlineData("port-index")]
    [InlineData("same-node")]
    [InlineData("duplicate")]
    [InlineData("negative-index")]
    public void InvalidConnectionsAgainstCompleteSavedSchemasAreRejected(string scenario)
    {
        var nodes = new[] { Node(FirstId, Layout([Port()], [Port()])), Node(SecondId, Layout([Port()], [Port()])) };
        var packed = scenario switch
        {
            "source-direction" => Pack(0, 2),
            "target-direction" => Pack(1, 3),
            "port-index" => Pack(1, 4),
            "same-node" => Pack(1, 0),
            "negative-index" => Pack(-1, 2),
            _ => Pack(1, 2),
        };
        var connections = scenario == "duplicate" ? new[] { packed, packed } : new[] { packed };

        Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateGraphReader.Read(Canvas(nodes, connections), CancellationToken.None));
    }

    [Fact]
    public void TheSharedEmptyOptionCannotBecomeAConnectionEndpoint()
    {
        var nodes = new[] { Node(FirstId, Layout([], [Port(empty: true), Port()])), Node(SecondId, Layout([Port()], [])) };

        Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateGraphReader.Read(Canvas(nodes, Pack(0, 2)), CancellationToken.None));
    }

    [Theory]
    [InlineData("version")]
    [InlineData("crc")]
    [InlineData("trailer")]
    [InlineData("frame")]
    public void InvalidEnvelopesAndFramesAreRejected(string scenario)
    {
        var content = Canvas([Node(FirstId)]);
        switch (scenario)
        {
            case "version": content[4] = 2; break;
            case "crc": content[^8] ^= 1; break;
            case "trailer": content = content[..^4]; break;
            case "frame":
                using (var input = new MemoryStream(content, 5, content.Length - 5))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var body = new MemoryStream())
                {
                    gzip.CopyTo(body);
                    content = Compress(body.ToArray()[..^1]);
                }
                break;
        }

        Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateGraphReader.Read(content, CancellationToken.None));
    }

    [Fact]
    public void AnEmptyCanvasHasCompleteEmptyMetadataAndRawEmptyBytesAreInvalid()
    {
        var graph = RecordedFlowTemplateGraphReader.Read(Canvas([]), CancellationToken.None);

        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Edges);
        Assert.Equal(0, graph.TotalConnectionCount);
        Assert.True(graph.ConnectionsComplete);
        Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateGraphReader.Read([], CancellationToken.None));
    }

    [Fact]
    public void PropertyFramesAreCountedBeforeSavedStateDictionaryAllocation()
    {
        var node = Node(FirstId, edit: properties =>
        {
            for (var index = 0; index < 4096; index++)
                properties[$"P{index}"] = Array.Empty<byte>();
        });

        Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateGraphReader.Read(Canvas([node]), CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TotalSavedPortBudgetCountsRepeatedEmptyOptionsBeforeGlobalDeduplication(bool exceedsLimit)
    {
        var compactLayout = Encoding.UTF8.GetBytes("{\"Inputs\":[" + string.Join(',', Enumerable.Repeat("{\"IsEmpty\":true}", 100_000)) + "],\"Outputs\":[]}");
        var nodes = Enumerable.Range(0, 10).Select(_ => Node(Guid.NewGuid(), edit: properties =>
            properties[STNodeSavedState.PortLayoutKey] = compactLayout)).ToList();
        if (exceedsLimit)
            nodes.Add(Node(Guid.NewGuid(), Layout([Port(empty: true)], [])));
        var content = Canvas(nodes);

        if (exceedsLimit)
            Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateGraphReader.Read(content, CancellationToken.None));
        else
        {
            var graph = RecordedFlowTemplateGraphReader.Read(content, CancellationToken.None);
            Assert.Equal(10, graph.Nodes.Count);
            Assert.True(graph.ConnectionsComplete);
        }
    }

    [Theory]
    [InlineData("nodes")]
    [InlineData("connections")]
    public void ExistingCanvasCountLimitsStillApply(string scenario)
    {
        byte[] content;
        if (scenario == "nodes")
            content = Canvas(Enumerable.Range(0, 10_001).Select(_ => Node(Guid.NewGuid())).ToArray());
        else
        {
            var ports = Enumerable.Repeat(Port(), 1000).ToArray();
            var nodes = new[] { Node(FirstId, Layout([], ports)), Node(SecondId, Layout(ports, [])) };
            content = Canvas(nodes, Enumerable.Range(0, 100_001).Select(index => Pack(index / 1000, 1000 + index % 1000)).ToArray());
        }

        Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateGraphReader.Read(content, CancellationToken.None));
    }

    [Fact]
    public void InspectionUsesItsOwn32MiBDecompressionBudget()
    {
        var opaqueValue = new byte[12 * 1024 * 1024];
        var nodes = Enumerable.Range(0, 3).Select(_ => Node(Guid.NewGuid(), edit: properties => properties["Opaque.Binary"] = opaqueValue)).ToArray();

        Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateGraphReader.Read(Canvas(nodes), CancellationToken.None));
    }

    [Fact]
    public void CancellationIsPropagatedBeforeReadingTheEnvelope()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => RecordedFlowTemplateGraphReader.Read(Canvas([Node(FirstId)]), cancellation.Token));
    }

    private static STNodeSavedPort Port(bool empty = false) => new() { Name = "saved port", TypeName = "Missing.Plugin.PortType", IsEmpty = empty };

    private static STNodePortLayout Layout(STNodeSavedPort[] inputs, STNodeSavedPort[] outputs, string? title = null) =>
        new() { Inputs = inputs, Outputs = outputs, Title = title };

    private static byte[] Node(Guid nodeId, STNodePortLayout? layout = null, Action<Dictionary<string, byte[]>>? edit = null)
    {
        var properties = new Dictionary<string, byte[]> { ["Guid"] = nodeId.ToByteArray() };
        if (layout != null)
            properties[STNodeSavedState.PortLayoutKey] = JsonSerializer.SerializeToUtf8Bytes(layout);
        edit?.Invoke(properties);
        return STNodeSavedState.Write(MissingModel, MissingType, properties);
    }

    private static byte[] Canvas(IReadOnlyList<byte[]> nodes, params long[] connections)
    {
        using var stream = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(stream, nodes, connections, 0, 0, 1);
        return stream.ToArray();
    }

    private static long Pack(int output, int input) => (long)output << 32 | unchecked((uint)input);

    private static byte[] Compress(byte[] body)
    {
        using var stream = new MemoryStream();
        stream.Write("STND"u8);
        stream.WriteByte(1);
        using (var gzip = new GZipStream(stream, CompressionMode.Compress, leaveOpen: true))
            gzip.Write(body);
        return stream.ToArray();
    }
}
