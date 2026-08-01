using ColorVision.Engine.Templates.Flow.Versioning;

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
    public void DiffReportsNodesPropertiesEdgesSubflowsRoutesAndRetries()
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
        after.Subflows[0].Revision = 8;
        after.ErrorRoutes[0].TargetNodeId = "node-c";
        after.RetryPolicies[0].MaxAttempts = 5;

        FlowSemanticDiffResult diff =
            FlowSemanticDiff.Compare(before, after);

        Assert.True(diff.HasSemanticChanges);
        Assert.False(diff.IsLayoutOnly);
        Assert.Equal("node-c", Assert.Single(diff.AddedNodes).NodeId);
        Assert.Equal("node-b", Assert.Single(diff.RemovedNodes).NodeId);
        Assert.Equal("Mode", Assert.Single(diff.PropertyChanges).PropertyName);
        Assert.Single(diff.AddedEdges);
        Assert.Single(diff.RemovedEdges);
        Assert.Single(diff.AddedSubflows);
        Assert.Single(diff.RemovedSubflows);
        Assert.Single(diff.AddedErrorRoutes);
        Assert.Single(diff.RemovedErrorRoutes);
        Assert.Single(diff.AddedRetryPolicies);
        Assert.Single(diff.RemovedRetryPolicies);
    }

    [Fact]
    public void HashesAreStableWhenCollectionsAreReordered()
    {
        FlowSemanticDocument first = CreateFullDocument();
        FlowSemanticDocument reordered = CreateFullDocument();
        reordered.Nodes.Reverse();
        reordered.Edges.Reverse();
        reordered.Subflows.Reverse();
        reordered.ErrorRoutes.Reverse();
        reordered.RetryPolicies.Reverse();
        reordered.RetryPolicies[0].RetryableKinds.Reverse();
        reordered.Layout.Nodes.Reverse();

        Assert.Equal(
            FlowSemanticHash.ComputeSemanticHash(first),
            FlowSemanticHash.ComputeSemanticHash(reordered));
        Assert.Equal(
            FlowSemanticHash.ComputeLayoutHash(first),
            FlowSemanticHash.ComputeLayoutHash(reordered));
    }

    [Fact]
    public void InvalidDuplicateNodeIdentityIsRejectedBeforeHashing()
    {
        FlowSemanticDocument invalid = CreateFullDocument();
        invalid.Nodes.Add(invalid.Nodes[0].DeepClone());

        Assert.Throws<ArgumentException>(() =>
            FlowSemanticHash.ComputeSemanticHash(invalid));
    }

    [Fact]
    public void DuplicateFailureKindRouteForOneNodeIsRejectedBeforeHashing()
    {
        FlowSemanticDocument invalid = CreateFullDocument();
        FlowErrorRoute duplicate =
            invalid.ErrorRoutes[0].DeepClone();
        duplicate.TargetPort = "in:1";
        invalid.ErrorRoutes.Add(duplicate);

        Assert.Throws<ArgumentException>(() =>
            FlowSemanticHash.ComputeSemanticHash(invalid));
    }

    [Fact]
    public void ErrorAndRetryKindsMustUseCanonicalFailureKindNames()
    {
        FlowSemanticDocument invalidRoute = CreateFullDocument();
        invalidRoute.ErrorRoutes[0].ErrorCode = "CAMERA_TIMEOUT";
        Assert.Throws<ArgumentException>(() =>
            FlowSemanticHash.ComputeSemanticHash(invalidRoute));

        FlowSemanticDocument invalidRetry = CreateFullDocument();
        invalidRetry.RetryPolicies[0].RetryableKinds.Add(
            "Transient");
        Assert.Throws<ArgumentException>(() =>
            FlowSemanticHash.ComputeSemanticHash(invalidRetry));
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
            Subflows =
            [
                new FlowSubflowReference
                {
                    CallNodeId = "node-a",
                    FlowKey = "flow:child",
                    Revision = 7,
                    InputMappings = new Dictionary<string, string>
                    {
                        ["input"] = "parent.value",
                    },
                },
            ],
            ErrorRoutes =
            [
                new FlowErrorRoute
                {
                    SourceNodeId = "node-a",
                    ErrorCode = "Timeout",
                    TargetNodeId = "node-b",
                    TargetPort = "in:0",
                },
            ],
            RetryPolicies =
            [
                new FlowRetryPolicyReference
                {
                    NodeId = "node-a",
                    MaxAttempts = 3,
                    InitialDelayMs = 100,
                    Backoff = 2,
                    MaxDelayMs = 1_000,
                    RetryableKinds =
                    [
                        "Technical",
                        "Timeout",
                    ],
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
