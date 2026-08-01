using System;
using System.Collections.Generic;
using System.Globalization;
using FlowFailureKind = FlowEngineLib.Runtime.FlowFailureKind;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    public static class FlowSemanticDocumentValidator
    {
        public static void Validate(FlowSemanticDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            if (document.Nodes == null
                || document.Edges == null
                || document.ErrorRoutes == null
                || document.RetryPolicies == null)
            {
                throw new ArgumentException(
                    "流程语义集合不能为 null。",
                    nameof(document));
            }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FlowSemanticNode node in document.Nodes)
            {
                ArgumentNullException.ThrowIfNull(node);
                EnsureText(node.NodeId, "节点 ID");
                EnsureText(node.TypeKey, "节点类型");
                if (!nodeIds.Add(node.NodeId))
                {
                    throw new ArgumentException(
                        $"流程包含重复节点 ID：{node.NodeId}",
                        nameof(document));
                }
                if (node.Properties == null)
                {
                    throw new ArgumentException(
                        $"节点 {node.NodeId} 的属性集合不能为 null。",
                        nameof(document));
                }
                foreach (string propertyName in node.Properties.Keys)
                    EnsureText(propertyName, "节点属性名");
            }

            var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (FlowSemanticEdge edge in document.Edges)
            {
                ArgumentNullException.ThrowIfNull(edge);
                EnsureText(edge.SourceNodeId, "普通边来源节点");
                EnsureText(edge.SourcePort, "普通边来源端口");
                EnsureText(edge.TargetNodeId, "普通边目标节点");
                EnsureText(edge.TargetPort, "普通边目标端口");
                if (!edgeKeys.Add(FlowSemanticHash.GetEdgeKey(edge)))
                {
                    throw new ArgumentException(
                        "流程包含重复普通边。",
                        nameof(document));
                }
            }

            var errorRouteKeys = new HashSet<string>(
                StringComparer.Ordinal);
            var errorRouteBindings =
                new HashSet<(string SourceNodeId, FlowFailureKind Kind)>();
            foreach (FlowErrorRoute route in document.ErrorRoutes)
            {
                ArgumentNullException.ThrowIfNull(route);
                EnsureText(route.SourceNodeId, "错误路由来源节点");
                EnsureText(route.ErrorCode, "错误代码");
                EnsureText(route.TargetNodeId, "错误路由目标节点");
                EnsureText(route.TargetPort, "错误路由目标端口");
                if (!Enum.TryParse(
                        route.ErrorCode,
                        ignoreCase: false,
                        out FlowFailureKind failureKind)
                    || !Enum.IsDefined(failureKind)
                    || !string.Equals(
                        route.ErrorCode,
                        failureKind.ToString(),
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"错误代码必须是有效的失败类型：{route.ErrorCode}。",
                        nameof(document));
                }
                EnsureInputPort(route.TargetPort, "错误路由目标端口");
                if (!errorRouteBindings.Add(
                    (route.SourceNodeId, failureKind)))
                {
                    throw new ArgumentException(
                        $"节点 {route.SourceNodeId} 的 {failureKind} "
                        + "失败类型只能配置一条错误路由。",
                        nameof(document));
                }
                if (!errorRouteKeys.Add(
                    FlowSemanticHash.GetErrorRouteKey(route)))
                {
                    throw new ArgumentException(
                        "流程包含重复错误路由。",
                        nameof(document));
                }
            }

            var retryPolicyNodes = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (FlowRetryPolicyReference retryPolicy in
                document.RetryPolicies)
            {
                ArgumentNullException.ThrowIfNull(retryPolicy);
                EnsureText(retryPolicy.NodeId, "重试策略节点");
                if (!retryPolicyNodes.Add(retryPolicy.NodeId))
                {
                    throw new ArgumentException(
                        $"节点 {retryPolicy.NodeId} 包含多个重试策略。",
                        nameof(document));
                }
                if (retryPolicy.MaxAttempts is < 1 or > 100)
                {
                    throw new ArgumentException(
                        "重试策略最大尝试次数必须介于 1 和 100 之间。",
                        nameof(document));
                }
                if (retryPolicy.InitialDelayMs < 0)
                {
                    throw new ArgumentException(
                        "重试策略初始延迟不能为负数。",
                        nameof(document));
                }
                EnsureFinite(retryPolicy.Backoff, "重试策略退避倍数");
                if (retryPolicy.Backoff < 1)
                {
                    throw new ArgumentException(
                        "重试策略退避倍数不能小于 1。",
                        nameof(document));
                }
                if (retryPolicy.MaxDelayMs
                    < retryPolicy.InitialDelayMs)
                {
                    throw new ArgumentException(
                        "重试策略最大延迟不能小于初始延迟。",
                        nameof(document));
                }
                if (retryPolicy.RetryableKinds == null
                    || retryPolicy.RetryableKinds.Count == 0)
                {
                    throw new ArgumentException(
                        "重试策略必须包含可重试失败类型。",
                        nameof(document));
                }

                var retryableKinds = new HashSet<string>(
                    StringComparer.Ordinal);
                foreach (string kind in retryPolicy.RetryableKinds)
                {
                    EnsureText(kind, "可重试失败类型");
                    if (!retryableKinds.Add(kind))
                    {
                        throw new ArgumentException(
                            $"重试策略包含重复失败类型：{kind}。",
                            nameof(document));
                    }
                    if (!Enum.TryParse(
                            kind,
                            ignoreCase: false,
                            out FlowFailureKind failureKind)
                        || !Enum.IsDefined(failureKind)
                        || !string.Equals(
                            kind,
                            failureKind.ToString(),
                            StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            $"可重试失败类型无效：{kind}。",
                            nameof(document));
                    }
                    if (failureKind == FlowFailureKind.Canceled)
                    {
                        throw new ArgumentException(
                            "Canceled 不能配置为可重试失败类型。",
                            nameof(document));
                    }
                }
            }

            FlowLayoutDocument layout =
                document.Layout ?? new FlowLayoutDocument();
            EnsureFinite(layout.ViewportX, "画布 X");
            EnsureFinite(layout.ViewportY, "画布 Y");
            EnsureFinite(layout.Scale, "画布缩放");
            if (layout.Scale <= 0)
                throw new ArgumentException("画布缩放必须大于零。", nameof(document));
            if (layout.Nodes == null)
                throw new ArgumentException("布局节点集合不能为 null。", nameof(document));
            var layoutNodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (FlowNodeLayout node in layout.Nodes)
            {
                ArgumentNullException.ThrowIfNull(node);
                EnsureText(node.NodeId, "布局节点 ID");
                if (!layoutNodeIds.Add(node.NodeId))
                {
                    throw new ArgumentException(
                        $"布局包含重复节点 ID：{node.NodeId}",
                        nameof(document));
                }
                EnsureFinite(node.X, "节点 X");
                EnsureFinite(node.Y, "节点 Y");
                EnsureFinite(node.Width, "节点宽度");
                EnsureFinite(node.Height, "节点高度");
            }
        }

        private static void EnsureText(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"{fieldName}不能为空。");
        }

        private static void EnsureFinite(double value, string fieldName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentException($"{fieldName}必须是有限数值。");
        }

        private static void EnsureInputPort(
            string value,
            string fieldName)
        {
            if (!value.StartsWith("in:", StringComparison.Ordinal)
                || !int.TryParse(
                    value.AsSpan(3),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int index)
                || index < 0)
            {
                throw new ArgumentException(
                    $"{fieldName}必须使用 in:<本地索引> 格式。");
            }
        }
    }
}
