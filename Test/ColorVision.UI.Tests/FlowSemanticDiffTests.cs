using ColorVision.Engine.Templates.Flow.Versioning;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class FlowSemanticDiffTests
{
    [Fact]
    public void LayoutOnlyChangeIsSeparatedFromSemanticChange()
    {
        FlowSemanticDocument before =
            FlowRevisionSidecarTests.CreateDocument("same", x: 10);
        FlowSemanticDocument after =
            FlowRevisionSidecarTests.CreateDocument("same", x: 200);

        FlowSemanticDiffResult diff =
            FlowSemanticDiff.Compare(before, after);

        Assert.True(diff.IsLayoutOnly);
        Assert.False(diff.HasSemanticChanges);
        Assert.Single(diff.LayoutChanges);
        Assert.Equal(
            FlowSemanticHash.ComputeSemanticHash(before),
            FlowSemanticHash.ComputeSemanticHash(after));
        Assert.NotEqual(
            FlowSemanticHash.ComputeLayoutHash(before),
            FlowSemanticHash.ComputeLayoutHash(after));
    }

    [Fact]
    public void DiffReportsNodesPropertiesAndEdges()
    {
        FlowSemanticDocument before = CreateFullDocument();
        FlowSemanticDocument after = CreateFullDocument();
        after.Nodes[0].Properties["Mode"] = "updated";
        after.Nodes.RemoveAt(1);
        after.Nodes.Add(new FlowSemanticNode
        {
            NodeId = "node-c",
            TypeKey = "Test.C",
        });
        after.Edges.Clear();
        after.Edges.Add(new FlowSemanticEdge
        {
            SourceNodeId = "node-a",
            SourcePort = "out",
            TargetNodeId = "node-c",
            TargetPort = "in",
        });
        FlowSemanticDiffResult diff =
            FlowSemanticDiff.Compare(before, after);

        Assert.True(diff.HasSemanticChanges);
        Assert.False(diff.IsLayoutOnly);
        Assert.Equal("node-c", Assert.Single(diff.AddedNodes).NodeId);
        Assert.Equal("node-b", Assert.Single(diff.RemovedNodes).NodeId);
        Assert.Equal("Mode", Assert.Single(diff.PropertyChanges).PropertyName);
        Assert.Single(diff.AddedEdges);
        Assert.Single(diff.RemovedEdges);
    }

    [Fact]
    public void PropertyDiffIncludesNullAdditionsAndRemovalsInOrdinalOrder()
    {
        FlowSemanticDocument before = CreateFullDocument();
        FlowSemanticDocument after = CreateFullDocument();
        before.Nodes[0].Properties["z-removed"] = null;
        before.Nodes[0].Properties["Mode"] = "before";
        after.Nodes[0].Properties["a-added"] = null;
        after.Nodes[0].Properties["Mode"] = "after";

        FlowSemanticDiffResult diff = FlowSemanticDiff.Compare(before, after);

        Assert.Equal(
            new FlowPropertyChange[]
            {
                new("node-a", "Mode", "before", "after"),
                new("node-a", "a-added", null, null),
                new("node-a", "z-removed", null, null),
            },
            diff.PropertyChanges);
    }

    [Fact]
    public void LayoutDiffDistinguishesSignedZeroInNodeCoordinates()
    {
        FlowSemanticDocument before = CreateFullDocument();
        FlowSemanticDocument after = CreateFullDocument();
        before.Layout.Nodes[0].X = -0.0;
        after.Layout.Nodes[0].X = 0.0;

        FlowSemanticDiffResult diff = FlowSemanticDiff.Compare(before, after);

        FlowLayoutChange change = Assert.Single(diff.LayoutChanges);
        Assert.Equal("node-a", change.NodeId);
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(change.Before!.X));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(change.After!.X));
    }

    [Fact]
    public void HashesAreStableWhenCollectionsAreReordered()
    {
        FlowSemanticDocument first = CreateFullDocument();
        FlowSemanticDocument reordered = CreateFullDocument();
        reordered.Nodes.Reverse();
        reordered.Edges.Reverse();
        reordered.Layout.Nodes.Reverse();

        Assert.Equal(
            FlowSemanticHash.ComputeSemanticHash(first),
            FlowSemanticHash.ComputeSemanticHash(reordered));
        Assert.Equal(
            FlowSemanticHash.ComputeLayoutHash(first),
            FlowSemanticHash.ComputeLayoutHash(reordered));
    }

    [Fact]
    public void CanonicalHashesMatchExistingRevisionsWithLongUnicodeValues()
    {
        const string nodeId = "node-🧪";
        var document = new FlowSemanticDocument
        {
            Nodes =
            [
                new FlowSemanticNode
                {
                    NodeId = nodeId,
                    TypeKey = "类型",
                    Properties = new Dictionary<string, string?>
                    {
                        ["Mode"] = "切换",
                        ["Null"] = null,
                        ["Notes"] = new string('x', 16_383) + "🧪中\uD800" + new string('λ', 32_770) + "\uDC00",
                    },
                },
            ],
            Layout = new FlowLayoutDocument
            {
                ViewportX = 1.25,
                ViewportY = -2.5,
                Scale = 0.75,
                Nodes =
                [
                    new FlowNodeLayout { NodeId = nodeId, X = -0.0, Y = 2.25, Width = 128.125, Height = 42 },
                ],
            },
        };

        Assert.Equal("9ae9e65f7a516b030ac3de062c46ab92214cb38ed81d10798bb568bb6128f513",
            FlowSemanticHash.ComputeSemanticHash(document));
        Assert.Equal("29da13f3f2c8c4b55908491d7830118b9884e9f56584a9cbc0560fddd1a38152",
            FlowSemanticHash.ComputeLayoutHash(document));
    }

    [Fact]
    public void LegacySubflowJsonIsIgnoredWithoutChangingStoredData()
    {
        const string legacyJson = """
            {
              "Nodes": [],
              "Edges": [],
              "Subflows": [{ "CallNodeId": "legacy-call" }],
              "Layout": { "Nodes": [] }
            }
            """;

        FlowSemanticDocument document = Assert.IsType<FlowSemanticDocument>(
            JsonSerializer.Deserialize<FlowSemanticDocument>(legacyJson));

        Assert.Empty(document.Nodes);
        Assert.Empty(document.Edges);
    }

    [Fact]
    public void InvalidDuplicateNodeIdentityIsRejectedBeforeHashing()
    {
        FlowSemanticDocument invalid = CreateFullDocument();
        invalid.Nodes.Add(invalid.Nodes[0].DeepClone());

        Assert.Throws<ArgumentException>(() =>
            FlowSemanticHash.ComputeSemanticHash(invalid));
    }

    private static FlowSemanticDocument CreateFullDocument()
    {
        return new FlowSemanticDocument
        {
            Nodes =
            [
                new FlowSemanticNode
                {
                    NodeId = "node-a",
                    TypeKey = "Test.A",
                    Properties = new Dictionary<string, string?>
                    {
                        ["Mode"] = "original",
                    },
                },
                new FlowSemanticNode
                {
                    NodeId = "node-b",
                    TypeKey = "Test.B",
                },
            ],
            Edges =
            [
                new FlowSemanticEdge
                {
                    SourceNodeId = "node-a",
                    SourcePort = "out",
                    TargetNodeId = "node-b",
                    TargetPort = "in",
                },
            ],
            Layout = new FlowLayoutDocument
            {
                Nodes =
                [
                    new FlowNodeLayout
                    {
                        NodeId = "node-a",
                        X = 10,
                        Y = 20,
                    },
                    new FlowNodeLayout
                    {
                        NodeId = "node-b",
                        X = 200,
                        Y = 20,
                    },
                ],
            },
        };
    }
}
