using ColorVision.Engine.Templates.Flow.Versioning;

namespace ColorVision.UI.Tests;

public sealed class FlowRevisionSidecarTests
{
    [Fact]
    public void RevisionIsImmutableAndRejectsStaleParentOrBaseHash()
    {
        var store = new InMemoryFlowRevisionStore();
        var service = new FlowRevisionService(store);
        byte[] firstBytes = [1, 2, 3, 4];
        FlowRevision first = service.CreateRevision(new FlowRevisionCreateRequest
        {
            FlowKey = "flow:revision-test",
            FullSnapshot = firstBytes,
            SemanticDocument = CreateDocument("A", x: 10),
            Source = FlowRevisionSource.Editor,
            Condition = FlowRevisionWriteCondition.Initial,
        });

        firstBytes[0] = 99;
        Assert.Equal([1, 2, 3, 4], first.FullSnapshot);
        Assert.Equal(1, first.Revision);
        Assert.Null(first.ParentRevision);
        Assert.Equal(
            FlowSemanticHash.ComputeBinaryHash([1, 2, 3, 4]),
            first.BinaryHash);
        Assert.Equal(
            FlowSemanticHash.ComputeSemanticHash(first.SemanticDocument),
            first.SemanticHash);
        Assert.Equal(
            FlowSemanticHash.ComputeLayoutHash(first.SemanticDocument),
            first.LayoutHash);

        FlowRevision second = service.CreateRevision(
            new FlowRevisionCreateRequest
            {
                FlowKey = first.FlowKey,
                FullSnapshot = [4, 5, 6],
                SemanticDocument = CreateDocument("B", x: 10),
                Source = FlowRevisionSource.Publish,
                IsPublished = true,
                Condition = FlowRevisionWriteCondition.FromHead(first),
            });
        Assert.Equal(2, second.Revision);
        Assert.Equal(1, second.ParentRevision);
        Assert.Equal(first.BinaryHash, second.BaseBinaryHash);
        Assert.True(second.IsPublished);

        Assert.Throws<FlowRevisionConflictException>(() =>
            service.CreateRevision(new FlowRevisionCreateRequest
            {
                FlowKey = first.FlowKey,
                FullSnapshot = [7, 8, 9],
                SemanticDocument = CreateDocument("C", x: 10),
                Condition = FlowRevisionWriteCondition.FromHead(first),
            }));
        Assert.Equal(2, store.List(first.FlowKey).Count);
    }

    [Fact]
    public void RollbackCreatesNewRevisionWithoutMutatingHistory()
    {
        var store = new InMemoryFlowRevisionStore();
        var service = new FlowRevisionService(store);
        FlowRevision first = CreateInitial(service, [1], "one");
        FlowRevision second = service.CreateRevision(
            CreateRequest(first, [2], "two"));

        FlowRevision rollback = service.Rollback(
            first.FlowKey,
            targetRevision: 1,
            FlowRevisionWriteCondition.FromHead(second),
            author: "operator");

        Assert.Equal(3, rollback.Revision);
        Assert.Equal(2, rollback.ParentRevision);
        Assert.Equal(1, rollback.RollbackOfRevision);
        Assert.Equal(FlowRevisionSource.Rollback, rollback.Source);
        Assert.Equal(first.BinaryHash, rollback.BinaryHash);
        Assert.Equal(first.FullSnapshot, rollback.FullSnapshot);
        Assert.Equal([1, 2, 3], store.List(first.FlowKey)
            .Select(item => item.Revision));
        Assert.Equal(
            second.BinaryHash,
            store.GetRevision(first.FlowKey, 2)!.BinaryHash);
    }

    [Fact]
    public void PublishingDraftAppendsPublishedRevisionWithSameSnapshot()
    {
        var store = new InMemoryFlowRevisionStore();
        var service = new FlowRevisionService(store);
        FlowRevision draft = CreateInitial(service, [4, 2], "draft");

        FlowRevision published = service.PublishCurrent(
            draft.FlowKey,
            FlowRevisionWriteCondition.FromHead(draft),
            author: "reviewer");

        Assert.Equal(2, published.Revision);
        Assert.Equal(1, published.ParentRevision);
        Assert.True(published.IsPublished);
        Assert.Equal(FlowRevisionSource.Publish, published.Source);
        Assert.Equal(draft.BinaryHash, published.BinaryHash);
        Assert.Equal(draft.SemanticHash, published.SemanticHash);
    }

    [Fact]
    public void ExternalReconcileDistinguishesNoOpHistoryAndConflict()
    {
        var store = new InMemoryFlowRevisionStore();
        var service = new FlowRevisionService(store);
        FlowExternalReconcileResult first =
            service.ReconcileExternalUpdate(new FlowExternalUpdateRequest
            {
                FlowKey = "flow:external",
                FullSnapshot = [1],
                SemanticDocument = CreateDocument("one", x: 0),
                ExternalVersion = "remote-1",
            });
        Assert.Equal(FlowExternalReconcileStatus.Created, first.Status);

        FlowExternalReconcileResult unchanged =
            service.ReconcileExternalUpdate(new FlowExternalUpdateRequest
            {
                FlowKey = "flow:external",
                FullSnapshot = [1],
                SemanticDocument = CreateDocument("one", x: 0),
                BaseBinaryHash = first.Revision!.BinaryHash,
            });
        Assert.Equal(FlowExternalReconcileStatus.Unchanged, unchanged.Status);

        FlowExternalReconcileResult advanced =
            service.ReconcileExternalUpdate(new FlowExternalUpdateRequest
            {
                FlowKey = "flow:external",
                FullSnapshot = [2],
                SemanticDocument = CreateDocument("two", x: 0),
                BaseBinaryHash = first.Revision.BinaryHash,
                ExternalVersion = "remote-2",
            });
        Assert.Equal(FlowExternalReconcileStatus.Created, advanced.Status);

        FlowExternalReconcileResult historical =
            service.ReconcileExternalUpdate(new FlowExternalUpdateRequest
            {
                FlowKey = "flow:external",
                FullSnapshot = [1],
                SemanticDocument = CreateDocument("one", x: 0),
                BaseBinaryHash = advanced.Revision!.BinaryHash,
            });
        Assert.Equal(
            FlowExternalReconcileStatus.HistoricalContent,
            historical.Status);
        Assert.Equal(1, historical.MatchingHistoricalRevision!.Revision);

        FlowExternalReconcileResult conflict =
            service.ReconcileExternalUpdate(new FlowExternalUpdateRequest
            {
                FlowKey = "flow:external",
                FullSnapshot = [3],
                SemanticDocument = CreateDocument("three", x: 0),
                BaseBinaryHash = first.Revision.BinaryHash,
            });
        Assert.Equal(FlowExternalReconcileStatus.Conflict, conflict.Status);
        Assert.Equal(2, conflict.CurrentHead!.Revision);
    }

    [Fact]
    public void ExternalReconcileTreatsSidecarsAsPartOfContentIdentity()
    {
        var store = new InMemoryFlowRevisionStore();
        var service = new FlowRevisionService(store);
        FlowExternalReconcileResult first =
            service.ReconcileExternalUpdate(new FlowExternalUpdateRequest
            {
                FlowKey = "flow:external-sidecar",
                FullSnapshot = [1],
                SemanticDocument = CreateDocument("policy-v1", x: 0),
            });

        FlowExternalReconcileResult sidecarChange =
            service.ReconcileExternalUpdate(new FlowExternalUpdateRequest
            {
                FlowKey = "flow:external-sidecar",
                FullSnapshot = [1],
                SemanticDocument = CreateDocument("policy-v2", x: 0),
                BaseBinaryHash = first.Revision!.BinaryHash,
            });
        Assert.Equal(
            FlowExternalReconcileStatus.Created,
            sidecarChange.Status);
        Assert.Equal(2, sidecarChange.Revision!.Revision);
        Assert.Equal(
            first.Revision.BinaryHash,
            sidecarChange.Revision.BinaryHash);
        Assert.NotEqual(
            first.Revision.SemanticHash,
            sidecarChange.Revision.SemanticHash);

        FlowExternalReconcileResult advanced =
            service.ReconcileExternalUpdate(new FlowExternalUpdateRequest
            {
                FlowKey = "flow:external-sidecar",
                FullSnapshot = [2],
                SemanticDocument = CreateDocument("advanced", x: 0),
                BaseBinaryHash = sidecarChange.Revision.BinaryHash,
            });
        FlowExternalReconcileResult historical =
            service.ReconcileExternalUpdate(new FlowExternalUpdateRequest
            {
                FlowKey = "flow:external-sidecar",
                FullSnapshot = [1],
                SemanticDocument = CreateDocument("policy-v1", x: 0),
                BaseBinaryHash = advanced.Revision!.BinaryHash,
            });

        Assert.Equal(
            FlowExternalReconcileStatus.HistoricalContent,
            historical.Status);
        Assert.Equal(
            first.Revision.Revision,
            historical.MatchingHistoricalRevision!.Revision);
    }

    [Fact]
    public void SqliteStoreRoundTripsFullRevisionAndEnforcesSameRules()
    {
        using var store = new SqliteFlowRevisionStore("Data Source=:memory:");
        var service = new FlowRevisionService(store);
        FlowRevision first = CreateInitial(service, [7, 8], "sqlite");
        FlowRevision second = service.CreateRevision(
            CreateRequest(first, [9, 10], "sqlite-2"));

        FlowRevision persisted = store.GetRevision(first.FlowKey, 2)!;
        Assert.Equal(second.BinaryHash, persisted.BinaryHash);
        Assert.Equal([9, 10], persisted.FullSnapshot);
        Assert.Equal("sqlite-2", persisted.SemanticDocument
            .Nodes.Single().Properties["Mode"]);
        Assert.Equal(2, store.List(first.FlowKey).Count);

        Assert.Throws<FlowRevisionConflictException>(() =>
            service.CreateRevision(CreateRequest(first, [11], "stale")));
    }

    [Fact]
    public void StoreRejectsSnapshotWhoseDeclaredHashesWereTampered()
    {
        var store = new InMemoryFlowRevisionStore();
        FlowSemanticDocument document = CreateDocument("safe", x: 0);

        Assert.Throws<ArgumentException>(() =>
            store.Append(new FlowRevisionAppendRequest
            {
                FlowKey = "flow:tampered",
                FullSnapshot = [1, 2, 3],
                SemanticDocument = document,
                Source = FlowRevisionSource.Import,
                Condition = FlowRevisionWriteCondition.Initial,
                SemanticHash =
                    FlowSemanticHash.ComputeSemanticHash(document),
                LayoutHash =
                    FlowSemanticHash.ComputeLayoutHash(document),
                BinaryHash = new string('0', 64),
                CreatedTimeUtc = DateTime.UtcNow,
            }));
    }

    private static FlowRevision CreateInitial(
        FlowRevisionService service,
        byte[] snapshot,
        string mode)
    {
        return service.CreateRevision(new FlowRevisionCreateRequest
        {
            FlowKey = "flow:test",
            FullSnapshot = snapshot,
            SemanticDocument = CreateDocument(mode, x: 10),
            Source = FlowRevisionSource.Editor,
            Condition = FlowRevisionWriteCondition.Initial,
        });
    }

    private static FlowRevisionCreateRequest CreateRequest(
        FlowRevision head,
        byte[] snapshot,
        string mode)
    {
        return new FlowRevisionCreateRequest
        {
            FlowKey = head.FlowKey,
            FullSnapshot = snapshot,
            SemanticDocument = CreateDocument(mode, x: 10),
            Source = FlowRevisionSource.Editor,
            Condition = FlowRevisionWriteCondition.FromHead(head),
        };
    }

    internal static FlowSemanticDocument CreateDocument(
        string mode,
        double x)
    {
        return new FlowSemanticDocument
        {
            Nodes =
            [
                new FlowSemanticNode
                {
                    NodeId = "node-a",
                    TypeKey = "Test.Node",
                    Properties = new Dictionary<string, string?>
                    {
                        ["Mode"] = mode,
                    },
                },
            ],
            Layout = new FlowLayoutDocument
            {
                Nodes =
                [
                    new FlowNodeLayout
                    {
                        NodeId = "node-a",
                        X = x,
                        Y = 20,
                        Width = 120,
                        Height = 60,
                    },
                ],
            },
        };
    }
}
