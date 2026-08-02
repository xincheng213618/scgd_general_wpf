using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Flow.Routing;
using ColorVision.Engine.Templates.Flow.Versioning;
using FlowEngineLib.Runtime;

namespace ColorVision.UI.Tests;

public sealed class FlowVersionRestoreServiceTests
{
    private const string FlowKey =
        "flow:11111111111111111111111111111111";
    private const string SourceNode =
        "98c02e1b-e0a7-4868-a7cf-c7be72d376f1";
    private const string TargetNode =
        "3b1ed2a1-6b93-4aab-ae6a-1478b5bb8677";

    [Fact]
    public void PolicyProjectionGroupsErrorKindsAndPreservesRetry()
    {
        var document = new FlowSemanticDocument
        {
            ErrorRoutes =
            {
                new ColorVision.Engine.Templates.Flow.Versioning.FlowErrorRoute
                {
                    SourceNodeId = SourceNode,
                    ErrorCode = nameof(FlowFailureKind.Timeout),
                    TargetNodeId = TargetNode,
                    TargetPort = "in:2",
                },
                new ColorVision.Engine.Templates.Flow.Versioning.FlowErrorRoute
                {
                    SourceNodeId = SourceNode,
                    ErrorCode = nameof(FlowFailureKind.Technical),
                    TargetNodeId = TargetNode,
                    TargetPort = "in:2",
                },
            },
            RetryPolicies =
            {
                new FlowRetryPolicyReference
                {
                    NodeId = SourceNode,
                    MaxAttempts = 3,
                    InitialDelayMs = 25,
                    Backoff = 2,
                    MaxDelayMs = 200,
                    RetryableKinds =
                    {
                        nameof(FlowFailureKind.Timeout),
                    },
                },
            },
        };

        FlowExecutionPolicySaveRequest request =
            FlowVersionRestoreProjection.CreatePolicySaveRequest(
                FlowKey,
                expectedRevision: 7,
                document);

        Assert.Equal(7, request.ExpectedRevision);
        FlowErrorRoutePolicy route =
            Assert.Single(request.ErrorRoutes);
        Assert.Equal(2, route.TargetInputIndex);
        Assert.Equal(
            new[]
            {
                FlowFailureKind.Timeout,
                FlowFailureKind.Technical,
            },
            route.FailureKinds);
        FlowRetryPolicy retry =
            Assert.Single(request.RetryPolicies);
        Assert.Equal(3, retry.MaxAttempts);
        Assert.Equal(
            FlowFailureKind.Timeout,
            Assert.Single(retry.RetryableKinds));
    }

    [Fact]
    public void RestoreReturnsCommittedStateForUiRefresh()
    {
        var flowParam = new FlowParam
        {
            DataBase64 = "before",
            FlowKey = FlowKey,
            TemplateRevision = 3,
        };
        var revision = new FlowRevision
        {
            FlowKey = FlowKey,
            Revision = 8,
            FullSnapshot = [1, 2, 3],
            SemanticDocument = new FlowSemanticDocument(),
        };
        var store = new RecordingPolicyStore(
            CreatePolicySnapshot(revision: 4));
        FlowTemplateSaveCondition? receivedCondition = null;
        var service = new FlowVersionRestoreService(
            store,
            (param, condition) =>
            {
                receivedCondition = condition;
                param.LoadedContentHash = "committed-hash";
                param.TemplateRevision = 9;
            },
            (_, _) => { });

        FlowVersionRestoreResult result = service.Restore(
            new FlowVersionRestoreRequest(
                flowParam,
                revision,
                ExpectedContentHash: "loaded-hash"));

        Assert.True(result.Succeeded);
        Assert.Equal("AQID", flowParam.DataBase64);
        Assert.Equal("loaded-hash", receivedCondition?.ExpectedContentHash);
        Assert.Equal("committed-hash", result.LoadedContentHash);
        Assert.True(result.VersionCatalogUpdated);
        Assert.Empty(store.SaveRequests);
    }

    [Fact]
    public void RestoreRollsBackPolicyWhenTemplateSaveFails()
    {
        var flowParam = new FlowParam
        {
            DataBase64 = "before",
            FlowKey = FlowKey,
            TemplateRevision = 3,
        };
        var document = new FlowSemanticDocument
        {
            RetryPolicies =
            {
                new FlowRetryPolicyReference
                {
                    NodeId = SourceNode,
                    MaxAttempts = 2,
                    RetryableKinds =
                    {
                        nameof(FlowFailureKind.Timeout),
                    },
                },
            },
        };
        var revision = new FlowRevision
        {
            FlowKey = FlowKey,
            Revision = 8,
            FullSnapshot = [1, 2, 3],
            SemanticDocument = document,
        };
        var store = new RecordingPolicyStore(
            CreatePolicySnapshot(revision: 4));
        var service = new FlowVersionRestoreService(
            store,
            (_, _) => throw new InvalidOperationException(
                "template-save-failed"),
            (_, _) => { });

        FlowVersionRestoreResult result = service.Restore(
            new FlowVersionRestoreRequest(
                flowParam,
                revision,
                ExpectedContentHash: "loaded-hash"));

        Assert.False(result.Succeeded);
        Assert.Equal("template-save-failed", result.FailureMessage);
        Assert.Null(result.RollbackFailure);
        Assert.Equal("before", flowParam.DataBase64);
        Assert.Equal(3, flowParam.TemplateRevision);
        Assert.Equal(2, store.SaveRequests.Count);
        Assert.NotEmpty(store.SaveRequests[0].RetryPolicies);
        Assert.Empty(store.SaveRequests[1].RetryPolicies);
        Assert.Equal(
            store.SaveRequests[0].ExpectedRevision + 1,
            store.SaveRequests[1].ExpectedRevision);
    }

    private static FlowExecutionPolicySnapshot CreatePolicySnapshot(
        long revision)
    {
        NormalizedFlowExecutionPolicy normalized =
            FlowExecutionPolicyRules.Normalize(
                FlowKey,
                Array.Empty<FlowErrorRoutePolicy>(),
                Array.Empty<FlowRetryPolicy>());
        return new FlowExecutionPolicySnapshot(
            FlowKey,
            revision,
            normalized.ContentHash,
            DateTime.UnixEpoch,
            normalized.ErrorRoutes,
            normalized.RetryPolicies);
    }

    private sealed class RecordingPolicyStore :
        IFlowExecutionPolicyStore
    {
        private FlowExecutionPolicySnapshot current;

        public RecordingPolicyStore(
            FlowExecutionPolicySnapshot current)
        {
            this.current = current;
        }

        public List<FlowExecutionPolicySaveRequest> SaveRequests { get; } =
            new();

        public FlowExecutionPolicySnapshot Load(string flowKey)
        {
            return current;
        }

        public bool TryLoad(
            string flowKey,
            out FlowExecutionPolicySnapshot snapshot,
            out string? failureReason)
        {
            snapshot = current;
            failureReason = null;
            return true;
        }

        public FlowExecutionPolicySnapshot Save(
            FlowExecutionPolicySaveRequest request)
        {
            SaveRequests.Add(request);
            NormalizedFlowExecutionPolicy normalized =
                FlowExecutionPolicyRules.Normalize(
                    request.FlowKey,
                    request.ErrorRoutes,
                    request.RetryPolicies);
            current = new FlowExecutionPolicySnapshot(
                request.FlowKey,
                current.Revision + 1,
                normalized.ContentHash,
                DateTime.UnixEpoch,
                normalized.ErrorRoutes,
                normalized.RetryPolicies);
            return current;
        }
    }
}
