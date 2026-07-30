using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.PostProcess;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Runtime;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class FlowFailureRoutingTests
{
    [Fact]
    public void RuntimeErrorRouteDoesNotCreatePersistedPortConnection()
    {
        var source = new TempCommonSensorNode();
        var target = new CVEndNode();
        source.Create();
        target.Create();
        var action = new CVStartCFC("SN-ERROR-ROUTE");
        var router = new FlowFailureRouter(
            new[]
            {
                new FlowErrorRoute
                {
                    SourceNodeId = source.NodeID,
                    TargetNodeId = target.NodeID,
                    TargetInputIndex = 0,
                    FailureKinds = new[] { FlowFailureKind.Business }
                }
            },
            () => new STNode[] { source, target });

        FlowFailureRouteResult route = router.TryRoute(
            source,
            action,
            new FlowFailure(
                FlowFailureKind.Business,
                "SERVICE_FAILED",
                "simulated",
                source.NodeID,
                source.Title,
                DateTime.UtcNow));

        Assert.True(route.IsRouted);
        Assert.Equal(0, source.GetAllOutputOptions()[^1].ConnectionCount);
        Assert.Equal(0, target.m_in_start.ConnectionCount);
        Assert.Equal(
            ConnectionStatus.Connected,
            route.Dispatch());
        Assert.Equal(StatusTypeEnum.Completed, action.FlowStatus);
        FlowHandledFailure handled = Assert.Single(
            FlowFailureData.GetHandledFailures(action));
        Assert.Equal(target.NodeID, handled.TargetNodeId);
        Assert.Equal(0, source.GetAllOutputOptions()[^1].ConnectionCount);
        Assert.Equal(0, target.m_in_start.ConnectionCount);
    }

    [Fact]
    public void MissingRuntimeErrorRouteTargetFailsClosed()
    {
        var source = new TempCommonSensorNode();
        source.Create();
        var action = new CVStartCFC("SN-MISSING-TARGET");
        var router = new FlowFailureRouter(
            new[]
            {
                new FlowErrorRoute
                {
                    SourceNodeId = source.NodeID,
                    TargetNodeId = Guid.NewGuid().ToString(),
                    TargetInputIndex = 0,
                    FailureKinds = new[] { FlowFailureKind.Business }
                }
            },
            () => new STNode[] { source });

        FlowFailureRouteResult route = router.TryRoute(
            source,
            action,
            new FlowFailure(
                FlowFailureKind.Business,
                "SERVICE_FAILED",
                "simulated",
                source.NodeID,
                source.Title,
                DateTime.UtcNow));

        Assert.Equal(
            FlowFailureRouteStatus.InvalidTarget,
            route.Status);
        Assert.Empty(FlowFailureData.GetHandledFailures(action));
        Assert.Equal(StatusTypeEnum.Runing, action.FlowStatus);
    }

    [Fact]
    public void HandledFailureMakesCompletedRunAWarning()
    {
        var action = new CVStartCFC("SN-WARNING");
        var source = new TempCommonSensorNode();
        var target = new CVEndNode();
        source.Create();
        target.Create();
        var router = new FlowFailureRouter(
            new[]
            {
                new FlowErrorRoute
                {
                    SourceNodeId = source.NodeID,
                    TargetNodeId = target.NodeID,
                    FailureKinds = new[] { FlowFailureKind.Business }
                }
            },
            () => new STNode[] { source, target });
        FlowFailureRouteResult route = router.TryRoute(
            source,
            action,
            new FlowFailure(
                FlowFailureKind.Business,
                "SERVICE_FAILED",
                "simulated",
                source.NodeID,
                source.Title,
                DateTime.UtcNow));
        Assert.Equal(ConnectionStatus.Connected, route.Dispatch());

        var engineResult = new FlowControlData
        {
            Status = StatusTypeEnum.Completed,
            HandledFailures = FlowFailureData.GetHandledFailures(action)
        };

        Assert.Equal(
            FlowFinalOutcome.SucceededWithWarnings,
            FlowFinalOutcomeResolver.Resolve(
                engineResult,
                Array.Empty<PostProcessExecutionResult>()));
    }

    [Fact]
    public void RetryDecisionUsesBoundedExponentialBackoff()
    {
        var policy = new FlowNodeRetryPolicy
        {
            NodeId = Guid.NewGuid().ToString(),
            MaxAttempts = 4,
            InitialDelayMs = 100,
            Backoff = 3,
            MaxDelayMs = 500,
            RetryableKinds = new[]
            {
                FlowFailureKind.Technical,
                FlowFailureKind.Timeout
            }
        };

        FlowRetryDecision first = policy.GetDecision(
            completedAttempts: 1,
            FlowFailureKind.Technical);
        FlowRetryDecision second = policy.GetDecision(
            completedAttempts: 2,
            FlowFailureKind.Timeout);
        FlowRetryDecision last = policy.GetDecision(
            completedAttempts: 4,
            FlowFailureKind.Technical);

        Assert.True(first.ShouldRetry);
        Assert.Equal(2, first.NextAttempt);
        Assert.Equal(TimeSpan.FromMilliseconds(100), first.Delay);
        Assert.True(second.ShouldRetry);
        Assert.Equal(TimeSpan.FromMilliseconds(300), second.Delay);
        Assert.False(last.ShouldRetry);
    }

    [Fact]
    public void CancellationCannotBeConfiguredForAutomaticRetry()
    {
        var policy = new FlowNodeRetryPolicy
        {
            NodeId = Guid.NewGuid().ToString(),
            MaxAttempts = 2,
            InitialDelayMs = 0,
            MaxDelayMs = 0,
            RetryableKinds = new[]
            {
                FlowFailureKind.Canceled
            }
        };

        Assert.Throws<ArgumentException>(() =>
            policy.GetDecision(
                completedAttempts: 1,
                FlowFailureKind.Canceled));
    }
}
