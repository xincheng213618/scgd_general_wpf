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
