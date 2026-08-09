using ColorVision.Engine.Templates.Flow.Search;
using ColorVision.Engine.Templates.Flow.Versioning;

namespace ColorVision.UI.Tests;

public sealed class FlowCatalogServiceTests
{
    [Fact]
    public void RecordEditorSaveIsIdempotentAndIndexesOnlyLatestRevision()
    {
        var revisions = new InMemoryFlowRevisionStore();
        var search = new InMemoryFlowNodeSearchIndex();
        var catalog = new FlowCatalogService(revisions, search);

        FlowRevision first = catalog.RecordEditorSave(
            "flow:catalog",
            [1, 2, 3],
            CreateDocument("node-1", "Camera"),
            [CreateSearchDocument("旧曝光")]);
        FlowRevision repeated = catalog.RecordEditorSave(
            "flow:catalog",
            [1, 2, 3],
            CreateDocument("node-1", "Camera"),
            [CreateSearchDocument("旧曝光")]);
        FlowRevision second = catalog.RecordEditorSave(
            "flow:catalog",
            [4, 5, 6],
            CreateDocument("node-1", "CameraV2"),
            [CreateSearchDocument("新曝光")]);

        Assert.Equal(1, first.Revision);
        Assert.Equal(first.Revision, repeated.Revision);
        Assert.Equal(2, second.Revision);
        FlowNodeSearchEntry hit = Assert.Single(
            catalog.SearchLatest("曝光"));
        Assert.Equal(2, hit.Revision);
        Assert.Equal("新曝光", hit.DisplayName);
    }

    [Fact]
    public void SavingHistoricalBytesCreatesAppendOnlyRollbackRevision()
    {
        var catalog = new FlowCatalogService(
            new InMemoryFlowRevisionStore(),
            new InMemoryFlowNodeSearchIndex());
        FlowSemanticDocument firstDocument =
            CreateDocument("node-1", "Camera");
        catalog.RecordEditorSave(
            "flow:rollback",
            [1],
            firstDocument,
            [CreateSearchDocument("第一版")]);
        catalog.RecordEditorSave(
            "flow:rollback",
            [2],
            CreateDocument("node-1", "CameraV2"),
            [CreateSearchDocument("第二版")]);

        FlowRevision rollback = catalog.RecordEditorSave(
            "flow:rollback",
            [1],
            firstDocument,
            [CreateSearchDocument("恢复第一版")]);

        Assert.Equal(3, rollback.Revision);
        Assert.Equal(FlowRevisionSource.Rollback, rollback.Source);
        Assert.Equal(1, rollback.RollbackOfRevision);
    }

    [Fact]
    public void SameBinaryWithChangedSemanticAndLayoutAppendsNewRevision()
    {
        var catalog = new FlowCatalogService(
            new InMemoryFlowRevisionStore(),
            new InMemoryFlowNodeSearchIndex());
        byte[] snapshot = [7, 8, 9];

        FlowRevision first = catalog.RecordEditorSave(
            "flow:projections",
            snapshot,
            CreateDocument(
                "node-1",
                "Camera",
                modeVersion: 2),
            [CreateSearchDocument("第一版")]);
        FlowRevision semanticChange = catalog.RecordEditorSave(
            "flow:projections",
            snapshot,
            CreateDocument(
                "node-1",
                "Camera",
                modeVersion: 3),
            [CreateSearchDocument("策略已更新")]);
        FlowRevision layoutChange = catalog.RecordEditorSave(
            "flow:projections",
            snapshot,
            CreateDocument(
                "node-1",
                "Camera",
                modeVersion: 3,
                layoutX: 120),
            [CreateSearchDocument("布局已更新")]);
        FlowRevision repeated = catalog.RecordEditorSave(
            "flow:projections",
            snapshot,
            CreateDocument(
                "node-1",
                "Camera",
                modeVersion: 3,
                layoutX: 120),
            [CreateSearchDocument("布局已更新")]);

        Assert.Equal(1, first.Revision);
        Assert.Equal(2, semanticChange.Revision);
        Assert.Equal(FlowRevisionSource.Editor, semanticChange.Source);
        Assert.Equal(first.BinaryHash, semanticChange.BinaryHash);
        Assert.NotEqual(first.SemanticHash, semanticChange.SemanticHash);
        Assert.Equal(3, layoutChange.Revision);
        Assert.Equal(semanticChange.SemanticHash, layoutChange.SemanticHash);
        Assert.NotEqual(semanticChange.LayoutHash, layoutChange.LayoutHash);
        Assert.Equal(layoutChange.Revision, repeated.Revision);
    }

    [Fact]
    public void HistoricalRollbackSelectsExactCompositeRevision()
    {
        var catalog = new FlowCatalogService(
            new InMemoryFlowRevisionStore(),
            new InMemoryFlowNodeSearchIndex());
        FlowSemanticDocument firstDocument = CreateDocument(
            "node-1",
            "Camera",
            modeVersion: 2);
        FlowSemanticDocument secondDocument = CreateDocument(
            "node-1",
            "Camera",
            modeVersion: 3);
        catalog.RecordEditorSave(
            "flow:exact-rollback",
            [1],
            firstDocument,
            [CreateSearchDocument("第一版策略")]);
        catalog.RecordEditorSave(
            "flow:exact-rollback",
            [1],
            secondDocument,
            [CreateSearchDocument("第二版策略")]);
        catalog.RecordEditorSave(
            "flow:exact-rollback",
            [2],
            CreateDocument("node-1", "CameraV2"),
            [CreateSearchDocument("第三版")]);

        FlowRevision rollback = catalog.RecordEditorSave(
            "flow:exact-rollback",
            [1],
            firstDocument,
            [CreateSearchDocument("恢复第一版策略")]);

        Assert.Equal(4, rollback.Revision);
        Assert.Equal(FlowRevisionSource.Rollback, rollback.Source);
        Assert.Equal(1, rollback.RollbackOfRevision);
        Assert.Equal(
            FlowSemanticHash.ComputeSemanticHash(firstDocument),
            rollback.SemanticHash);
    }

    [Fact]
    public void FindRevisionReturnsHeadWithoutFallingBackToBinaryLookup()
    {
        var store = new HeadOnlyBinaryLookupStore();
        var catalog = new FlowCatalogService(
            store,
            new InMemoryFlowNodeSearchIndex());
        FlowRevision head = catalog.RecordEditorSave(
            "flow:head-preferred",
            [1],
            CreateDocument("node-1", "Camera"),
            [CreateSearchDocument("当前版本")]);

        FlowRevision found = Assert.IsType<FlowRevision>(
            catalog.FindRevision("flow:head-preferred", [1]));

        Assert.Equal(head.Revision, found.Revision);
    }

    [Fact]
    public void SqliteStoreFindsExactCompositeRevision()
    {
        using var store = new SqliteFlowRevisionStore(
            "Data Source=:memory:");
        var catalog = new FlowCatalogService(
            store,
            new InMemoryFlowNodeSearchIndex());
        FlowSemanticDocument firstDocument = CreateDocument(
            "node-1",
            "Camera",
            modeVersion: 2);
        FlowRevision first = catalog.RecordEditorSave(
            "flow:sqlite-composite",
            [1],
            firstDocument,
            [CreateSearchDocument("第一版")]);
        catalog.RecordEditorSave(
            "flow:sqlite-composite",
            [1],
            CreateDocument(
                "node-1",
                "Camera",
                modeVersion: 3),
            [CreateSearchDocument("第二版")]);

        FlowRevision found = Assert.IsType<FlowRevision>(
            store.FindByContentHashes(
                first.FlowKey,
                first.BinaryHash,
                first.SemanticHash,
                first.LayoutHash));

        Assert.Equal(1, found.Revision);
    }

    private static FlowSemanticDocument CreateDocument(
        string nodeId,
        string typeKey,
        int? modeVersion = null,
        double layoutX = 0)
    {
        var document = new FlowSemanticDocument
        {
            Nodes =
            [
                new FlowSemanticNode
                {
                    NodeId = nodeId,
                    TypeKey = typeKey,
                },
            ],
            Layout = new FlowLayoutDocument
            {
                Nodes =
                [
                    new FlowNodeLayout
                    {
                        NodeId = nodeId,
                        X = layoutX,
                        Width = 160,
                        Height = 80,
                    },
                ],
            },
        };
        if (modeVersion != null)
        {
            document.Nodes[0].Properties["ModeVersion"] =
                modeVersion.Value.ToString();
        }
        return document;
    }

    private static FlowNodeSearchDocument CreateSearchDocument(
        string displayName)
    {
        return new FlowNodeSearchDocument
        {
            SourceNodeGuid = Guid.Parse(
                "29f6e845-ae0c-4c6c-b04d-32c599e77287"),
            NodePath =
                "root/nodes/29f6e845ae0c4c6cb04d32c599e77287",
            NodeTypeKey = "Camera",
            DisplayName = displayName,
        };
    }

    private sealed class HeadOnlyBinaryLookupStore :
        IFlowRevisionStore
    {
        private readonly InMemoryFlowRevisionStore inner = new();

        public FlowRevision? GetHead(string flowKey)
        {
            return inner.GetHead(flowKey);
        }

        public FlowRevision? GetRevision(string flowKey, int revision)
        {
            return inner.GetRevision(flowKey, revision);
        }

        public FlowRevision? FindByBinaryHash(
            string flowKey,
            string binaryHash)
        {
            throw new InvalidOperationException(
                "Head matches must not use historical binary lookup.");
        }

        public IReadOnlyList<FlowRevision> List(string flowKey)
        {
            return inner.List(flowKey);
        }

        public FlowRevision Append(FlowRevisionAppendRequest request)
        {
            return inner.Append(request);
        }
    }
}
