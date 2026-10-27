using ColorVision.Engine.Services.FlowDiagnostics;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecordedFlowTemplateComparisonGraphTests
{
    private static readonly Guid First = Guid.Parse("2b101433-e3d6-4cec-936d-1a6575d30001");
    private static readonly Guid Second = Guid.Parse("2b101433-e3d6-4cec-936d-1a6575d30002");
    private static readonly Guid Third = Guid.Parse("2b101433-e3d6-4cec-936d-1a6575d30003");
    private static readonly Guid Fourth = Guid.Parse("2b101433-e3d6-4cec-936d-1a6575d30004");
    private const string UnknownModel = "Uninstalled.RecordedComparison.Plugin";
    private const string UnknownType = "unavailable-plugin-type";

    [Fact]
    public void GuidAndExactPropertyNamesDefineChangesWithoutPublishingValuesOrFingerprints()
    {
        const string beforeSecret = "PRIVATE_BEFORE_VALUE";
        const string afterSecret = "PRIVATE_AFTER_VALUE";
        var baseline = Canvas([Node(Third, edit: properties => properties["RemovedNodeField"] = Bytes("PRIVATE_REMOVED_VALUE")),
            Node(Second, edit: properties =>
            {
                properties["Keep"] = [1];
                properties["Changed"] = Bytes(beforeSecret);
                properties["SameLength"] = [10, 20];
                properties["Removed"] = Bytes("PRIVATE_REMOVED_FIELD_VALUE");
                properties["Case"] = [2];
            }), Node(First)]);
        var current = Canvas([Node(Second, model: "Another.Uninstalled.Model", type: "another-unavailable-type", edit: properties =>
            {
                properties["Keep"] = [1];
                properties["Changed"] = Bytes(afterSecret);
                properties["SameLength"] = [10, 21];
                properties["Added"] = Bytes("PRIVATE_ADDED_FIELD_VALUE");
                properties["case"] = [2];
            }), Node(Fourth, edit: properties => properties["NewNodeField"] = Bytes("PRIVATE_ADDED_VALUE")), Node(First)]);

        var result = RecordedFlowTemplateComparer.Compare(baseline, current, CancellationToken.None);

        Assert.Equal(new[] { Second, Third, Fourth }, result.NodeChanges.Select(change => change.NodeId));
        var modified = result.NodeChanges[0];
        Assert.Equal("modified", modified.Kind);
        Assert.True(modified.ModelChanged);
        Assert.True(modified.TypeChanged);
        Assert.Equal(UnknownModel, modified.Baseline!.ModelKey);
        Assert.Equal("Another.Uninstalled.Model", modified.Current!.ModelKey);
        Assert.Equal(new[] { "Added", "case" }, modified.AddedProperties);
        Assert.Equal(new[] { "Case", "Removed" }, modified.RemovedProperties);
        Assert.Equal(new[] { "Changed", "SameLength" }, modified.ChangedProperties);
        var removed = result.NodeChanges[1];
        Assert.Equal("removed", removed.Kind);
        Assert.Null(removed.Current);
        Assert.Contains("Guid", removed.RemovedProperties);
        Assert.Contains("RemovedNodeField", removed.RemovedProperties);
        var added = result.NodeChanges[2];
        Assert.Equal("added", added.Kind);
        Assert.Null(added.Baseline);
        Assert.Contains("Guid", added.AddedProperties);
        Assert.Contains("NewNodeField", added.AddedProperties);
        Assert.False(result.SameRecordedBytes);
        Assert.True(result.ConnectionsComparable);
        Assert.Empty(result.AddedEdges);
        Assert.Empty(result.RemovedEdges);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("PRIVATE_", json, StringComparison.Ordinal);
        foreach (var secret in new[] { beforeSecret, afterSecret })
        {
            var fingerprint = Convert.ToHexString(SHA256.HashData(Bytes(secret)));
            Assert.DoesNotContain(fingerprint, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PortLayoutRemainsASavedFieldRatherThanAnInferredBusinessParameter()
    {
        var before = Canvas([Node(First, layout: Layout("Before title"))]);
        var after = Canvas([Node(First, layout: Layout("After title"))]);

        var result = RecordedFlowTemplateComparer.Compare(before, after, CancellationToken.None);

        var change = Assert.Single(result.NodeChanges);
        Assert.Equal(new[] { STNodeSavedState.PortLayoutKey }, change.ChangedProperties);
        Assert.Equal("Before title", change.Baseline!.Title);
        Assert.Equal("After title", change.Current!.Title);
        Assert.False(change.ModelChanged);
        Assert.False(change.TypeChanged);
    }

    [Theory]
    [InlineData("gzip-header")]
    [InlineData("property-order")]
    public void DifferentRecordedRepresentationsCanHaveNoModeledChanges(string mutation)
    {
        var baseline = Canvas([Node(First, edit: properties => { properties["A"] = [1]; properties["B"] = [2]; })]);
        var current = baseline.ToArray();
        if (mutation == "gzip-header")
            current[9] ^= 1; // gzip MTIME metadata is outside the decompressed body and its CRC.
        else
            current = Canvas([Node(First, edit: properties => { properties["B"] = [2]; properties["A"] = [1]; })]);

        var result = RecordedFlowTemplateComparer.Compare(baseline, current, CancellationToken.None);

        Assert.False(result.SameRecordedBytes);
        Assert.Empty(result.NodeChanges);
        Assert.Empty(result.AddedEdges);
        Assert.Empty(result.RemovedEdges);
        Assert.False(result.CanvasViewChanged);
        Assert.False(result.NodeOrderChanged);
        Assert.Equal(false, result.ConnectionOrderChanged);
    }

    [Theory]
    [InlineData("view")]
    [InlineData("nodes")]
    [InlineData("connections")]
    public void ViewNodeOrderAndConnectionOrderAreReportedIndependently(string mutation)
    {
        var order = new[] { First, Second, Third };
        var links = new[] { (First, Second), (Second, Third) };
        var baseline = Canvas(order.Select(id => Node(id)).ToArray(), Connections(order, links));
        var currentOrder = mutation == "nodes" ? new[] { Third, First, Second } : order;
        var currentLinks = mutation == "connections" ? links.Reverse().ToArray() : links;
        var current = Canvas(currentOrder.Select(id => Node(id)).ToArray(), Connections(currentOrder, currentLinks),
            mutation == "view" ? 23 : 0, mutation == "view" ? -17 : 0, mutation == "view" ? 1.5f : 1);

        var result = RecordedFlowTemplateComparer.Compare(baseline, current, CancellationToken.None);

        Assert.Empty(result.NodeChanges);
        Assert.Empty(result.AddedEdges);
        Assert.Empty(result.RemovedEdges);
        Assert.True(result.ConnectionsComparable);
        Assert.Equal(mutation == "view", result.CanvasViewChanged);
        Assert.Equal(mutation == "nodes", result.NodeOrderChanged);
        Assert.Equal(mutation == "connections", result.ConnectionOrderChanged);
    }

    [Fact]
    public void ConnectionSetChangesAreIndependentOfNodeSavedFields()
    {
        var order = new[] { First, Second, Third };
        var nodes = order.Select(id => Node(id)).ToArray();
        var baseline = Canvas(nodes, Connections(order, [(First, Second), (Second, Third)]));
        var current = Canvas(nodes, Connections(order, [(First, Third), (Second, Third)]));

        var result = RecordedFlowTemplateComparer.Compare(baseline, current, CancellationToken.None);

        Assert.Empty(result.NodeChanges);
        Assert.Equal(new RecordedFlowTemplateEdge(First, 0, Third, 0), Assert.Single(result.AddedEdges));
        Assert.Equal(new RecordedFlowTemplateEdge(First, 0, Second, 0), Assert.Single(result.RemovedEdges));
        Assert.True(result.ConnectionsComparable);
        Assert.Equal(true, result.ConnectionOrderChanged);
        Assert.False(result.NodeOrderChanged);
        Assert.False(result.CanvasViewChanged);
    }

    [Theory]
    [InlineData("missing-layout")]
    [InlineData("placeholder")]
    [InlineData("legacy-archive")]
    public void IdenticalLegacyBytesDoNotClaimThatConnectionsHaveBeenCompared(string kind)
    {
        var first = Node(First, includeLayout: kind != "missing-layout", edit: properties =>
        {
            if (kind == "placeholder") properties[STNodeSavedState.LegacyPlaceholderKey] = [];
            if (kind == "legacy-archive") properties["__STNode.LegacyConnections"] = Bytes("PRIVATE_ARCHIVE");
        });
        var content = Canvas([first, Node(Second)], kind == "missing-layout" ? [Pack(0, 1)] : []);

        var result = RecordedFlowTemplateComparer.Compare(content, content.ToArray(), CancellationToken.None);

        Assert.True(result.SameRecordedBytes);
        Assert.Empty(result.NodeChanges);
        Assert.False(result.ConnectionsComparable);
        Assert.Empty(result.AddedEdges);
        Assert.Empty(result.RemovedEdges);
        Assert.Null(result.ConnectionOrderChanged);
        Assert.DoesNotContain("PRIVATE_ARCHIVE", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothEnvelopesMustPassTheExistingIntegrityBoundary(bool invalidCurrent)
    {
        var valid = Canvas([Node(First)]);
        var corrupt = valid.ToArray();
        corrupt[^8] ^= 1;

        Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateComparer.Compare(
            invalidCurrent ? valid : corrupt, invalidCurrent ? corrupt : valid, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComparisonDecodeKeepsTheInspectionPropertyBudgetOnBothSides(bool excessiveCurrent)
    {
        var valid = Canvas([Node(First)]);
        var excessive = Canvas([Node(First, edit: properties =>
        {
            for (var index = 0; index < 4096; index++) properties[$"P{index}"] = [];
        })]);

        Assert.Throws<InvalidDataException>(() => RecordedFlowTemplateComparer.Compare(
            excessiveCurrent ? valid : excessive, excessiveCurrent ? excessive : valid, CancellationToken.None));
    }

    [Fact]
    public void IdenticalCompleteGraphsRemainReadOnlyAndHaveNoChanges()
    {
        var content = Canvas([Node(First), Node(Second)], [Pack(1, 2)]);
        var original = content.ToArray();

        var result = RecordedFlowTemplateComparer.Compare(content, content.ToArray(), CancellationToken.None);

        Assert.Equal(original, content);
        Assert.True(result.SameRecordedBytes);
        Assert.True(result.ConnectionsComparable);
        Assert.Empty(result.NodeChanges);
        Assert.Empty(result.AddedEdges);
        Assert.Empty(result.RemovedEdges);
        Assert.False(result.CanvasViewChanged);
        Assert.False(result.NodeOrderChanged);
        Assert.Equal(false, result.ConnectionOrderChanged);
        Assert.True(Assert.IsAssignableFrom<IList<RecordedFlowTemplateNodeChange>>(result.NodeChanges).IsReadOnly);
        Assert.True(Assert.IsAssignableFrom<IList<RecordedFlowTemplateEdge>>(result.AddedEdges).IsReadOnly);
    }

    [Fact]
    public void CancellationCannotBeBypassedByTheIdenticalByteShortcut()
    {
        var content = Canvas([Node(First)]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => RecordedFlowTemplateComparer.Compare(content, content, cancellation.Token));
    }

    private static byte[] Node(Guid id, string model = UnknownModel, string type = UnknownType,
        STNodePortLayout? layout = null, bool includeLayout = true, Action<Dictionary<string, byte[]>>? edit = null)
    {
        var properties = new Dictionary<string, byte[]> { ["Guid"] = id.ToByteArray() };
        if (includeLayout) properties[STNodeSavedState.PortLayoutKey] = JsonSerializer.SerializeToUtf8Bytes(layout ?? Layout("Saved node"));
        edit?.Invoke(properties);
        return STNodeSavedState.Write(model, type, properties);
    }

    private static STNodePortLayout Layout(string title) => new()
    {
        Title = title,
        Inputs = [new STNodeSavedPort { Name = "input", TypeName = "Uninstalled.PortType" }],
        Outputs = [new STNodeSavedPort { Name = "output", TypeName = "Uninstalled.PortType" }],
    };

    private static byte[] Canvas(IReadOnlyList<byte[]> nodes, IReadOnlyList<long>? connections = null,
        float x = 0, float y = 0, float scale = 1)
    {
        using var stream = new MemoryStream();
        STNodeCanvasWriter.WriteRaw(stream, nodes, connections ?? [], x, y, scale);
        return stream.ToArray();
    }

    private static long[] Connections(Guid[] order, IReadOnlyList<(Guid Source, Guid Target)> connections) => connections
        .Select(edge => Pack(Array.IndexOf(order, edge.Source) * 2 + 1, Array.IndexOf(order, edge.Target) * 2)).ToArray();

    private static long Pack(int output, int input) => (long)output << 32 | unchecked((uint)input);

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
