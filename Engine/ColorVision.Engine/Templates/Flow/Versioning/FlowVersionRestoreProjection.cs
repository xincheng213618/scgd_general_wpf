using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.Templates.Flow.Routing;
using FlowEngineLib.Runtime;
using System;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    /// <summary>
    /// Rebuilds and validates the sidecar projection that belongs to an
    /// immutable flow revision. It does not mutate stores or editor state.
    /// </summary>
    internal static class FlowVersionRestoreProjection
    {
        public static FlowExecutionPolicySaveRequest CreatePolicySaveRequest(
            string flowKey,
            long expectedRevision,
            FlowSemanticDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            var retries = document.RetryPolicies
                .Select(policy => new FlowRetryPolicy(
                    policy.NodeId,
                    policy.MaxAttempts,
                    policy.InitialDelayMs,
                    policy.Backoff,
                    policy.MaxDelayMs,
                    policy.RetryableKinds
                        .Select(ParseFailureKind)
                        .ToArray()))
                .ToArray();

            var routeBindings = document.ErrorRoutes
                .Select(route =>
                {
                    if (!route.IsInterrupting)
                    {
                        throw new InvalidOperationException(
                            "当前运行时不支持恢复非中断型错误路由。");
                    }
                    return new
                    {
                        route.SourceNodeId,
                        route.TargetNodeId,
                        TargetInputIndex =
                            ParseTargetInputIndex(route.TargetPort),
                        FailureKind =
                            ParseFailureKind(route.ErrorCode),
                    };
                })
                .ToArray();
            FlowErrorRoutePolicy[] routes = routeBindings
                .GroupBy(item => new
                {
                    item.SourceNodeId,
                    item.TargetNodeId,
                    item.TargetInputIndex,
                })
                .Select(group => new FlowErrorRoutePolicy(
                    group.Key.SourceNodeId,
                    group.Key.TargetNodeId,
                    group.Key.TargetInputIndex,
                    group.Select(item => item.FailureKind)
                        .Distinct()
                        .ToArray()))
                .ToArray();
            return new FlowExecutionPolicySaveRequest(
                flowKey,
                expectedRevision,
                routes,
                retries);
        }

        public static void Validate(
            FlowRevision revision,
            FlowExecutionPolicySaveRequest policy)
        {
            StoredFlowSubflowDefinition? sidecar =
                FlowSubflowDefinitionStoreProvider.Shared.GetRevision(
                    revision.FlowKey,
                    revision.Revision);
            if (revision.SemanticDocument.Subflows.Count > 0
                && sidecar == null)
            {
                throw new InvalidOperationException(
                    $"版本 {revision.Revision} 引用了子流程，"
                    + "但对应的不可变子流程侧车不存在。");
            }

            NormalizedFlowExecutionPolicy normalized =
                FlowExecutionPolicyRules.Normalize(
                    revision.FlowKey,
                    policy.ErrorRoutes,
                    policy.RetryPolicies);
            var snapshot = new FlowExecutionPolicySnapshot(
                revision.FlowKey,
                revision: 0,
                normalized.ContentHash,
                DateTime.UnixEpoch,
                normalized.ErrorRoutes,
                normalized.RetryPolicies);
            FlowCanvasCatalogBuildResult projection =
                new FlowCanvasCatalogBuilder().Build(
                    revision.FullSnapshot,
                    sidecar?.Sidecar
                        ?? FlowSubflowSidecar.Empty,
                    snapshot);
            string semanticHash =
                FlowSemanticHash.ComputeSemanticHash(
                    projection.SemanticDocument);
            string layoutHash =
                FlowSemanticHash.ComputeLayoutHash(
                    projection.SemanticDocument);
            if (!string.Equals(
                    semanticHash,
                    revision.SemanticHash,
                    StringComparison.Ordinal)
                || !string.Equals(
                    layoutHash,
                    revision.LayoutHash,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"版本 {revision.Revision} 的 STN 与侧车内容"
                    + "无法重建出原始语义，已拒绝恢复。");
            }
        }

        private static FlowFailureKind ParseFailureKind(
            string value)
        {
            if (!Enum.TryParse(
                    value,
                    ignoreCase: false,
                    out FlowFailureKind kind)
                || !Enum.IsDefined(kind))
            {
                throw new InvalidOperationException(
                    $"版本包含无法识别的失败类型：{value}。");
            }
            return kind;
        }

        private static int ParseTargetInputIndex(
            string targetPort)
        {
            const string prefix = "in:";
            if (string.IsNullOrWhiteSpace(targetPort)
                || !targetPort.StartsWith(
                    prefix,
                    StringComparison.Ordinal)
                || !int.TryParse(
                    targetPort[prefix.Length..],
                    out int inputIndex)
                || inputIndex < 0)
            {
                throw new InvalidOperationException(
                    "版本包含无效的错误路由目标端口："
                    + $"{targetPort}。");
            }
            return inputIndex;
        }
    }
}
