using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Templates.Flow.Versioning
{
    public sealed record FlowNodeTypeChange(
        string NodeId,
        string BeforeTypeKey,
        string AfterTypeKey);

    public sealed record FlowPropertyChange(
        string NodeId,
        string PropertyName,
        string? BeforeValue,
        string? AfterValue);

    public sealed record FlowLayoutChange(
        string NodeId,
        FlowNodeLayout? Before,
        FlowNodeLayout? After);

    public sealed class FlowSemanticDiffResult
    {
        public List<FlowSemanticNode> AddedNodes { get; } = new();

        public List<FlowSemanticNode> RemovedNodes { get; } = new();

        public List<FlowNodeTypeChange> ChangedNodeTypes { get; } = new();

        public List<FlowPropertyChange> PropertyChanges { get; } = new();

        public List<FlowSemanticEdge> AddedEdges { get; } = new();

        public List<FlowSemanticEdge> RemovedEdges { get; } = new();

        public List<FlowLayoutChange> LayoutChanges { get; } = new();

        public bool ViewportChanged { get; internal set; }

        public bool HasSemanticChanges =>
            AddedNodes.Count > 0
            || RemovedNodes.Count > 0
            || ChangedNodeTypes.Count > 0
            || PropertyChanges.Count > 0
            || AddedEdges.Count > 0
            || RemovedEdges.Count > 0;

        public bool HasLayoutChanges =>
            ViewportChanged || LayoutChanges.Count > 0;

        public bool IsLayoutOnly =>
            !HasSemanticChanges && HasLayoutChanges;
    }

    public static class FlowSemanticDiff
    {
        public static FlowSemanticDiffResult Compare(
            FlowSemanticDocument before,
            FlowSemanticDocument after)
        {
            ArgumentNullException.ThrowIfNull(before);
            ArgumentNullException.ThrowIfNull(after);
            var result = new FlowSemanticDiffResult();

            Dictionary<string, FlowSemanticNode> beforeNodes =
                IndexNodes(before.Nodes);
            Dictionary<string, FlowSemanticNode> afterNodes =
                IndexNodes(after.Nodes);

            foreach (string nodeId in beforeNodes.Keys
                .Union(afterNodes.Keys, StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal))
            {
                bool hasBefore = beforeNodes.TryGetValue(
                    nodeId,
                    out FlowSemanticNode? beforeNode);
                bool hasAfter = afterNodes.TryGetValue(
                    nodeId,
                    out FlowSemanticNode? afterNode);
                if (!hasBefore)
                {
                    result.AddedNodes.Add(afterNode!.DeepClone());
                    continue;
                }
                if (!hasAfter)
                {
                    result.RemovedNodes.Add(beforeNode!.DeepClone());
                    continue;
                }

                if (!string.Equals(
                    beforeNode!.TypeKey,
                    afterNode!.TypeKey,
                    StringComparison.Ordinal))
                {
                    result.ChangedNodeTypes.Add(new FlowNodeTypeChange(
                        nodeId,
                        beforeNode.TypeKey,
                        afterNode.TypeKey));
                }
                CompareProperties(result, beforeNode, afterNode);
            }

            CompareSet(
                before.Edges,
                after.Edges,
                FlowSemanticHash.GetEdgeKey,
                result.RemovedEdges,
                result.AddedEdges,
                item => item.DeepClone());
            CompareLayout(result, before.Layout, after.Layout);
            return result;
        }

        private static Dictionary<string, FlowSemanticNode> IndexNodes(
            IEnumerable<FlowSemanticNode> nodes)
        {
            var index = new Dictionary<string, FlowSemanticNode>(
                StringComparer.Ordinal);
            foreach (FlowSemanticNode node in nodes)
            {
                if (string.IsNullOrWhiteSpace(node.NodeId))
                    throw new ArgumentException("语义节点 ID 不能为空。", nameof(nodes));
                if (!index.TryAdd(node.NodeId, node))
                {
                    throw new ArgumentException(
                        $"语义文档包含重复节点 ID：{node.NodeId}",
                        nameof(nodes));
                }
            }
            return index;
        }

        private static void CompareProperties(
            FlowSemanticDiffResult result,
            FlowSemanticNode before,
            FlowSemanticNode after)
        {
            foreach (string propertyName in before.Properties.Keys
                .Union(after.Properties.Keys, StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal))
            {
                before.Properties.TryGetValue(
                    propertyName,
                    out string? beforeValue);
                after.Properties.TryGetValue(
                    propertyName,
                    out string? afterValue);
                if (!string.Equals(
                    beforeValue,
                    afterValue,
                    StringComparison.Ordinal)
                    || before.Properties.ContainsKey(propertyName)
                        != after.Properties.ContainsKey(propertyName))
                {
                    result.PropertyChanges.Add(new FlowPropertyChange(
                        before.NodeId,
                        propertyName,
                        beforeValue,
                        afterValue));
                }
            }
        }

        private static void CompareSet<T>(
            IEnumerable<T> before,
            IEnumerable<T> after,
            Func<T, string> keySelector,
            ICollection<T> removed,
            ICollection<T> added,
            Func<T, T> clone)
        {
            Dictionary<string, T> beforeIndex = before.ToDictionary(
                keySelector,
                item => item,
                StringComparer.Ordinal);
            Dictionary<string, T> afterIndex = after.ToDictionary(
                keySelector,
                item => item,
                StringComparer.Ordinal);
            foreach (string key in beforeIndex.Keys
                .Except(afterIndex.Keys, StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal))
            {
                removed.Add(clone(beforeIndex[key]));
            }
            foreach (string key in afterIndex.Keys
                .Except(beforeIndex.Keys, StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal))
            {
                added.Add(clone(afterIndex[key]));
            }
        }

        private static void CompareLayout(
            FlowSemanticDiffResult result,
            FlowLayoutDocument before,
            FlowLayoutDocument after)
        {
            before ??= new FlowLayoutDocument();
            after ??= new FlowLayoutDocument();
            result.ViewportChanged =
                before.ViewportX != after.ViewportX
                || before.ViewportY != after.ViewportY
                || before.Scale != after.Scale;

            Dictionary<string, FlowNodeLayout> beforeNodes =
                before.Nodes.ToDictionary(
                    item => item.NodeId,
                    item => item,
                    StringComparer.Ordinal);
            Dictionary<string, FlowNodeLayout> afterNodes =
                after.Nodes.ToDictionary(
                    item => item.NodeId,
                    item => item,
                    StringComparer.Ordinal);
            foreach (string nodeId in beforeNodes.Keys
                .Union(afterNodes.Keys, StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal))
            {
                beforeNodes.TryGetValue(
                    nodeId,
                    out FlowNodeLayout? beforeNode);
                afterNodes.TryGetValue(
                    nodeId,
                    out FlowNodeLayout? afterNode);
                if (beforeNode == null
                    || afterNode == null
                    || !string.Equals(
                        FlowSemanticHash.GetLayoutKey(beforeNode),
                        FlowSemanticHash.GetLayoutKey(afterNode),
                        StringComparison.Ordinal))
                {
                    result.LayoutChanges.Add(new FlowLayoutChange(
                        nodeId,
                        beforeNode?.DeepClone(),
                        afterNode?.DeepClone()));
                }
            }
        }
    }
}
