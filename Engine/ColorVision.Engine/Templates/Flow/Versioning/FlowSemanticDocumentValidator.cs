using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    public static class FlowSemanticDocumentValidator
    {
        public static void Validate(FlowSemanticDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            if (document.Nodes == null || document.Edges == null)
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

    }
}
