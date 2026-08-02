using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FlowEngineLib.Runtime;

public enum FlowFailureKind
{
    Business,
    Technical,
    Timeout,
    Canceled,
    Contract
}

public sealed record FlowFailure(
    FlowFailureKind Kind,
    string Code,
    string Message,
    string NodeId,
    string NodeName,
    DateTime OccurredTimeUtc);

public sealed record FlowHandledFailure(
    FlowFailure Failure,
    string TargetNodeId,
    int TargetInputIndex);

public sealed class FlowNodeRetryPolicy
{
    public string NodeId { get; init; } = string.Empty;

    /// <summary>
    /// Total attempts including the initial attempt.
    /// </summary>
    public int MaxAttempts { get; init; } = 1;

    public int InitialDelayMs { get; init; }

    public double Backoff { get; init; } = 1;

    public int MaxDelayMs { get; init; }

    public FlowFailureKind[] RetryableKinds { get; init; } =
        Array.Empty<FlowFailureKind>();

    public FlowRetryDecision GetDecision(
        int completedAttempts,
        FlowFailureKind failureKind)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            completedAttempts,
            1);
        Validate();

        if (failureKind == FlowFailureKind.Canceled
            || completedAttempts >= MaxAttempts
            || !RetryableKinds.Contains(failureKind))
        {
            return FlowRetryDecision.DoNotRetry(
                completedAttempts,
                MaxAttempts);
        }

        double delay = InitialDelayMs
            * Math.Pow(Backoff, completedAttempts - 1);
        int delayMs = delay >= MaxDelayMs
            ? MaxDelayMs
            : Math.Max(0, (int)Math.Round(delay));
        return FlowRetryDecision.Retry(
            completedAttempts + 1,
            MaxAttempts,
            TimeSpan.FromMilliseconds(delayMs));
    }

    internal void Validate()
    {
        if (!Guid.TryParse(NodeId, out _))
        {
            throw new ArgumentException(
                "重试策略节点 ID 必须是有效 GUID。",
                nameof(NodeId));
        }
        if (MaxAttempts < 1 || MaxAttempts > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxAttempts));
        }
        if (InitialDelayMs < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(InitialDelayMs));
        }
        if (!double.IsFinite(Backoff) || Backoff < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(Backoff));
        }
        if (MaxDelayMs < InitialDelayMs)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxDelayMs));
        }
        if (RetryableKinds == null || RetryableKinds.Length == 0)
        {
            throw new ArgumentException(
                "重试策略必须至少包含一种可重试失败。",
                nameof(RetryableKinds));
        }
        if (RetryableKinds.Contains(FlowFailureKind.Canceled))
        {
            throw new ArgumentException(
                "取消永远不能自动重试。",
                nameof(RetryableKinds));
        }
    }
}

public sealed record FlowRetryDecision(
    bool ShouldRetry,
    int NextAttempt,
    int MaxAttempts,
    TimeSpan Delay)
{
    internal static FlowRetryDecision Retry(
        int nextAttempt,
        int maxAttempts,
        TimeSpan delay)
    {
        return new FlowRetryDecision(
            true,
            nextAttempt,
            maxAttempts,
            delay);
    }

    internal static FlowRetryDecision DoNotRetry(
        int completedAttempts,
        int maxAttempts)
    {
        return new FlowRetryDecision(
            false,
            completedAttempts,
            maxAttempts,
            TimeSpan.Zero);
    }
}

public sealed class FlowErrorRoute
{
    public string SourceNodeId { get; init; } = string.Empty;

    public string TargetNodeId { get; init; } = string.Empty;

    public int TargetInputIndex { get; init; }

    public FlowFailureKind[] FailureKinds { get; init; } =
    new[]
    {
        FlowFailureKind.Business,
        FlowFailureKind.Technical,
        FlowFailureKind.Contract
    };

    internal bool Matches(FlowFailureKind failureKind)
    {
        return FailureKinds != null && FailureKinds.Contains(failureKind);
    }
}

public enum FlowFailureRouteStatus
{
    NotConfigured,
    Routed,
    InvalidTarget,
    Rejected
}

public sealed class FlowFailureRouteResult
{
    private readonly Func<ConnectionStatus>? dispatcher;

    public FlowFailureRouteResult(
        FlowFailureRouteStatus status,
        string? targetNodeId = null,
        int? targetInputIndex = null,
        string? message = null)
        : this(
            status,
            targetNodeId,
            targetInputIndex,
            message,
            null)
    {
    }

    internal FlowFailureRouteResult(
        FlowFailureRouteStatus status,
        string? targetNodeId,
        int? targetInputIndex,
        string? message,
        Func<ConnectionStatus>? dispatcher)
    {
        Status = status;
        TargetNodeId = targetNodeId;
        TargetInputIndex = targetInputIndex;
        Message = message;
        this.dispatcher = dispatcher;
    }

    public FlowFailureRouteStatus Status { get; }

    public string? TargetNodeId { get; }

    public int? TargetInputIndex { get; }

    public string? Message { get; }

    public bool IsRouted => Status == FlowFailureRouteStatus.Routed;

    internal ConnectionStatus Dispatch()
    {
        return dispatcher?.Invoke() ?? ConnectionStatus.Reject;
    }
}

public interface IFlowFailureRouter
{
    FlowFailureRouteResult TryRoute(
        CVBaseServerNode sourceNode,
        CVStartCFC action,
        FlowFailure failure);
}

public static class FlowFailureData
{
    private const string HandledFailuresKey = "HandledFailures";

    public static IReadOnlyList<FlowHandledFailure> GetHandledFailures(
        CVStartCFC action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (action.Data)
        {
            return action.Data.TryGetValue(
                    HandledFailuresKey,
                    out object? value)
                && value is List<FlowHandledFailure> handledFailures
                    ? handledFailures.ToArray()
                    : Array.Empty<FlowHandledFailure>();
        }
    }
}

internal sealed class FlowFailureRouter : IFlowFailureRouter
{
    private readonly IReadOnlyDictionary<string, FlowErrorRoute[]> routesBySource;
    private readonly Func<IReadOnlyList<STNode>> nodesProvider;

    public FlowFailureRouter(
        IEnumerable<FlowErrorRoute> routes,
        Func<IReadOnlyList<STNode>> nodesProvider)
    {
        ArgumentNullException.ThrowIfNull(routes);
        this.nodesProvider =
            nodesProvider ?? throw new ArgumentNullException(nameof(nodesProvider));

        FlowErrorRoute[] normalizedRoutes = routes
            .Select(Normalize)
            .ToArray();
        routesBySource = normalizedRoutes
            .GroupBy(route => route.SourceNodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    public FlowFailureRouteResult TryRoute(
        CVBaseServerNode sourceNode,
        CVStartCFC action,
        FlowFailure failure)
    {
        ArgumentNullException.ThrowIfNull(sourceNode);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(failure);

        if (!routesBySource.TryGetValue(
                sourceNode.NodeID,
                out FlowErrorRoute[]? sourceRoutes))
        {
            return new FlowFailureRouteResult(
                FlowFailureRouteStatus.NotConfigured);
        }

        FlowErrorRoute? route = sourceRoutes
            .FirstOrDefault(candidate => candidate.Matches(failure.Kind));
        if (route == null)
        {
            return new FlowFailureRouteResult(
                FlowFailureRouteStatus.NotConfigured);
        }

        STNode? targetNode = nodesProvider()
            .FirstOrDefault(node => string.Equals(
                node.Guid.ToString(),
                route.TargetNodeId,
                StringComparison.OrdinalIgnoreCase));
        if (targetNode == null)
        {
            return InvalidTarget(route, "找不到错误分支目标节点。");
        }

        STNodeOption[] inputs = targetNode.GetAllInputOptions();
        if (route.TargetInputIndex < 0
            || route.TargetInputIndex >= inputs.Length)
        {
            return InvalidTarget(route, "错误分支目标输入端口不存在。");
        }

        STNodeOption targetInput = inputs[route.TargetInputIndex];
        ConnectionStatus status = sourceNode.CanTransferFailureTo(targetInput);
        if (status != ConnectionStatus.Connected)
        {
            return new FlowFailureRouteResult(
                FlowFailureRouteStatus.Rejected,
                route.TargetNodeId,
                route.TargetInputIndex,
                $"错误分支目标拒绝运行时数据：{status}。");
        }

        return new FlowFailureRouteResult(
            FlowFailureRouteStatus.Routed,
            route.TargetNodeId,
            route.TargetInputIndex,
            null,
            () =>
            {
                FlowHandledFailure handledFailure =
                    new FlowHandledFailure(
                        failure,
                        route.TargetNodeId,
                        route.TargetInputIndex);
                AddHandledFailure(action, handledFailure);
                try
                {
                    ConnectionStatus dispatchStatus =
                        sourceNode.TransferFailureTo(targetInput, action);
                    if (dispatchStatus != ConnectionStatus.Connected)
                    {
                        RemoveHandledFailure(action, handledFailure);
                    }
                    return dispatchStatus;
                }
                catch
                {
                    RemoveHandledFailure(action, handledFailure);
                    throw;
                }
            });
    }

    private static FlowErrorRoute Normalize(FlowErrorRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        string sourceNodeId = route.SourceNodeId?.Trim() ?? string.Empty;
        string targetNodeId = route.TargetNodeId?.Trim() ?? string.Empty;
        if (!Guid.TryParse(sourceNodeId, out _))
        {
            throw new ArgumentException(
                "错误分支源节点 ID 必须是有效 GUID。",
                nameof(route));
        }
        if (!Guid.TryParse(targetNodeId, out _))
        {
            throw new ArgumentException(
                "错误分支目标节点 ID 必须是有效 GUID。",
                nameof(route));
        }
        if (route.TargetInputIndex < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(route),
                "错误分支目标输入索引不能为负数。");
        }

        FlowFailureKind[] kinds = route.FailureKinds?
            .Distinct()
            .ToArray() ?? Array.Empty<FlowFailureKind>();
        if (kinds.Length == 0)
        {
            throw new ArgumentException(
                "错误分支必须至少匹配一种失败类型。",
                nameof(route));
        }

        return new FlowErrorRoute
        {
            SourceNodeId = sourceNodeId,
            TargetNodeId = targetNodeId,
            TargetInputIndex = route.TargetInputIndex,
            FailureKinds = kinds
        };
    }

    private static FlowFailureRouteResult InvalidTarget(
        FlowErrorRoute route,
        string message)
    {
        return new FlowFailureRouteResult(
            FlowFailureRouteStatus.InvalidTarget,
            route.TargetNodeId,
            route.TargetInputIndex,
            message);
    }

    private static void AddHandledFailure(
        CVStartCFC action,
        FlowHandledFailure handledFailure)
    {
        const string key = "HandledFailures";
        lock (action.Data)
        {
            if (action.Data.TryGetValue(key, out object? value)
                && value is List<FlowHandledFailure> handledFailures)
            {
                handledFailures.Add(handledFailure);
                return;
            }

            action.Data[key] = new List<FlowHandledFailure>
            {
                handledFailure
            };
        }
    }

    private static void RemoveHandledFailure(
        CVStartCFC action,
        FlowHandledFailure handledFailure)
    {
        const string key = "HandledFailures";
        lock (action.Data)
        {
            if (action.Data.TryGetValue(key, out object? value)
                && value is List<FlowHandledFailure> handledFailures)
            {
                handledFailures.Remove(handledFailure);
                if (handledFailures.Count == 0)
                {
                    action.Data.Remove(key);
                }
            }
        }
    }
}
