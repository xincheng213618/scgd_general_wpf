using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.FlowDiagnostics;

public sealed record RecordedFlowTemplateNodeChange(
    Guid NodeId,
    string Kind,
    RecordedFlowTemplateNode? Baseline,
    RecordedFlowTemplateNode? Current,
    bool ModelChanged,
    bool TypeChanged,
    IReadOnlyList<string> AddedProperties,
    IReadOnlyList<string> RemovedProperties,
    IReadOnlyList<string> ChangedProperties);

public sealed record RecordedFlowTemplateComparison(
    RecordedFlowTemplateGraph BaselineGraph,
    RecordedFlowTemplateGraph CurrentGraph,
    IReadOnlyList<RecordedFlowTemplateNodeChange> NodeChanges,
    IReadOnlyList<RecordedFlowTemplateEdge> AddedEdges,
    IReadOnlyList<RecordedFlowTemplateEdge> RemovedEdges,
    bool SameRecordedBytes,
    bool ConnectionsComparable,
    bool CanvasViewChanged,
    bool NodeOrderChanged,
    bool? ConnectionOrderChanged);

/// <summary>Compares recorded graph metadata and saved property bytes without resolving node implementations.</summary>
public static class RecordedFlowTemplateComparer
{
    public static RecordedFlowTemplateComparison Compare(byte[] baseline, byte[] current, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        cancellationToken.ThrowIfCancellationRequested();
        var sameBytes = baseline.AsSpan().SequenceEqual(current);
        var before = RecordedFlowTemplateGraphReader.ReadForComparison(baseline, cancellationToken);
        var after = sameBytes ? before : RecordedFlowTemplateGraphReader.ReadForComparison(current, cancellationToken);
        var beforeNodes = before.Graph.Nodes.ToDictionary(node => node.NodeId);
        var afterNodes = after.Graph.Nodes.ToDictionary(node => node.NodeId);
        var nodeChanges = new List<RecordedFlowTemplateNodeChange>();
        foreach (var id in beforeNodes.Keys.Union(afterNodes.Keys).Order())
        {
            cancellationToken.ThrowIfCancellationRequested();
            beforeNodes.TryGetValue(id, out var oldNode);
            afterNodes.TryGetValue(id, out var newNode);
            if (oldNode == null)
            {
                nodeChanges.Add(new(id, "added", null, newNode, false, false, newNode!.PropertyNames, [], []));
                continue;
            }
            if (newNode == null)
            {
                nodeChanges.Add(new(id, "removed", oldNode, null, false, false, [], oldNode.PropertyNames, []));
                continue;
            }

            var oldProperties = before.Properties[id];
            var newProperties = after.Properties[id];
            var added = newProperties.Keys.Except(oldProperties.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var removed = oldProperties.Keys.Except(newProperties.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var changed = new List<string>();
            foreach (var property in oldProperties.Keys.Intersect(newProperties.Keys, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (oldProperties[property] != newProperties[property])
                    changed.Add(property);
            }
            var modelChanged = !string.Equals(oldNode.ModelKey, newNode.ModelKey, StringComparison.Ordinal);
            var typeChanged = !string.Equals(oldNode.TypeKey, newNode.TypeKey, StringComparison.Ordinal);
            if (modelChanged || typeChanged || added.Length > 0 || removed.Length > 0 || changed.Count > 0)
                nodeChanges.Add(new(id, "modified", oldNode, newNode, modelChanged, typeChanged,
                    Array.AsReadOnly(added), Array.AsReadOnly(removed), Array.AsReadOnly(changed.Order(StringComparer.Ordinal).ToArray())));
        }

        var comparable = before.Graph.ConnectionsComplete && after.Graph.ConnectionsComplete;
        var addedEdges = Array.Empty<RecordedFlowTemplateEdge>();
        var removedEdges = Array.Empty<RecordedFlowTemplateEdge>();
        if (comparable)
        {
            var oldEdges = before.Graph.Edges.ToHashSet();
            var newEdges = after.Graph.Edges.ToHashSet();
            addedEdges = SortEdges(newEdges.Except(oldEdges));
            removedEdges = SortEdges(oldEdges.Except(newEdges));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new RecordedFlowTemplateComparison(before.Graph, after.Graph, nodeChanges.AsReadOnly(),
            Array.AsReadOnly(addedEdges), Array.AsReadOnly(removedEdges), sameBytes, comparable,
            before.View != after.View,
            !before.Graph.Nodes.Select(node => node.NodeId).SequenceEqual(after.Graph.Nodes.Select(node => node.NodeId)),
            comparable ? !before.Graph.Edges.SequenceEqual(after.Graph.Edges) : null);
    }

    private static RecordedFlowTemplateEdge[] SortEdges(IEnumerable<RecordedFlowTemplateEdge> edges) => edges
        .OrderBy(edge => edge.SourceNodeId).ThenBy(edge => edge.SourcePortIndex)
        .ThenBy(edge => edge.TargetNodeId).ThenBy(edge => edge.TargetPortIndex).ToArray();
}
