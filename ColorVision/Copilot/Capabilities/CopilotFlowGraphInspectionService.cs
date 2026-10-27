using ColorVision.Copilot.Mcp;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot;

/// <summary>Reads bounded graph and node-detail streams from the complete current editor snapshot.</summary>
public sealed partial class CopilotFlowGraphInspectionService
{
    internal const int MaximumContentCharacters = 18_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Func<CancellationToken, Task<CopilotFlowContextSnapshot?>> _read;

    public CopilotFlowGraphInspectionService(Func<CancellationToken, Task<CopilotFlowContextSnapshot?>> read)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
    }

    public async Task<CopilotCapabilityResult> InspectAsync(string? nodeId, bool includeProperties, int maxNodes, string section,
        int maxEdges, string? expectedRevision, string? cursor, CancellationToken cancellationToken, string? expectedSourceInstanceId = null, int maxItems = 80)
    {
        nodeId = string.IsNullOrWhiteSpace(nodeId) ? null : nodeId;
        if (maxNodes is < 1 or > 200 || maxEdges is < 1 or > 200 || maxItems is < 1 or > 200 || !IsSection(section)
            || section == "properties" && !includeProperties
            || nodeId is { Length: > 256 } || expectedRevision != null && !IsHex(expectedRevision, 64)
            || expectedSourceInstanceId != null && !IsHex(expectedSourceInstanceId, 32))
            return Failure(CopilotToolFailureKind.Validation, "Use section nodes, edges, ports or properties, limits between 1 and 200, a node_id up to 256 characters, and exact revision/source identifiers from the graph response. The properties section requires include_properties=true.");
        Cursor? continuation = null;
        if (cursor != null && (!TryDecodeCursor(cursor, out continuation)
            || continuation!.Section != section || continuation.IncludeProperties != includeProperties
            || !string.Equals(continuation.NodeId, nodeId ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            || expectedRevision != null && !string.Equals(continuation.Revision, expectedRevision, StringComparison.OrdinalIgnoreCase)
            || expectedSourceInstanceId != null && !string.Equals(continuation.SourceInstanceId, expectedSourceInstanceId, StringComparison.OrdinalIgnoreCase)))
            return InvalidCursor();
        cancellationToken.ThrowIfCancellationRequested();
        CopilotFlowContextSnapshot? snapshot;
        try
        {
            snapshot = await _read(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) { throw; }
        catch (CopilotFlowValidationScopeChangedException) { return InvalidCursor(); }
        catch (TimeoutException)
        {
            return Failure(CopilotToolFailureKind.Transient, "The current Flow editor did not provide a graph snapshot in time.");
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return Failure(CopilotToolFailureKind.Internal, "The current Flow graph could not be captured. No graph evidence was published.");
        }
        if (snapshot == null)
            return continuation != null ? InvalidCursor() : Failure(CopilotToolFailureKind.NotFound, "No active Flow editor is available. This does not establish that any saved or historical graph is empty.");
        if (!IsHex(snapshot.Revision, 64) || !IsHex(snapshot.SourceInstanceId, 32)) return InvalidSource();
        if (expectedRevision != null && !string.Equals(snapshot.Revision, expectedRevision, StringComparison.OrdinalIgnoreCase)
            || expectedSourceInstanceId != null && !string.Equals(snapshot.SourceInstanceId, expectedSourceInstanceId, StringComparison.OrdinalIgnoreCase)
            || continuation != null && (!string.Equals(snapshot.Revision, continuation.Revision, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(snapshot.SourceInstanceId, continuation.SourceInstanceId, StringComparison.OrdinalIgnoreCase)))
            return InvalidCursor();
        var allNodes = snapshot.Nodes.OrderBy(node => node.InstanceId, StringComparer.Ordinal).ToArray();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in allNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(node.InstanceId, "D", out var id) || id == Guid.Empty || !ids.Add(node.InstanceId)
                || !ValidPorts(node.InputPorts) || !ValidPorts(node.OutputPorts)
                || includeProperties && !ValidProperties(node.Parameters)) return InvalidSource();
        }
        var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in snapshot.Edges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ids.Contains(edge.SourceNodeId) || !ids.Contains(edge.TargetNodeId)
                || string.IsNullOrWhiteSpace(edge.SourcePortId) || edge.SourcePortId.Length > 128
                || string.IsNullOrWhiteSpace(edge.TargetPortId) || edge.TargetPortId.Length > 128
                || !edgeKeys.Add(EdgeKey(edge))) return InvalidSource();
        }
        var nodes = allNodes.Where(node => nodeId == null || string.Equals(node.InstanceId, nodeId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)).ToArray();
        var focusedIds = nodes.Select(node => node.InstanceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edges = snapshot.Edges.Where(edge => nodeId == null || focusedIds.Contains(edge.SourceNodeId) || focusedIds.Contains(edge.TargetNodeId))
            .OrderBy(edge => edge.SourceNodeId, StringComparer.Ordinal).ThenBy(edge => edge.SourcePortId, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetNodeId, StringComparer.Ordinal).ThenBy(edge => edge.TargetPortId, StringComparer.Ordinal).ToArray();
        var ports = nodes.SelectMany(node => node.InputPorts.OrderBy(port => port.PortId, StringComparer.Ordinal)
            .Select(port => new PortRow(node.InstanceId, "input", port)).Concat(node.OutputPorts.OrderBy(port => port.PortId, StringComparer.Ordinal)
            .Select(port => new PortRow(node.InstanceId, "output", port)))).ToArray();
        var properties = includeProperties ? nodes.SelectMany(NodeProperties).ToArray() : [];
        var keys = section switch
        {
            "nodes" => nodes.Select(node => node.InstanceId), "edges" => edges.Select(EdgeKey),
            "ports" => ports.Select(PortKey), _ => properties.Select(PropertyKey),
        };
        var streamKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(keys)))).ToLowerInvariant();
        var total = section switch { "nodes" => nodes.Length, "edges" => edges.Length, "ports" => ports.Length, _ => properties.Length };
        if (continuation != null && (continuation.StreamKey != streamKey || continuation.Offset >= total)) return InvalidCursor();
        cancellationToken.ThrowIfCancellationRequested();
        var result = Format(snapshot, nodes, edges, ports, properties, nodeId, includeProperties, section, maxNodes, maxEdges, maxItems, continuation?.Offset ?? 0, streamKey);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static string EdgeKey(CopilotFlowEdgeContextSnapshot edge) => JsonSerializer.Serialize(new[]
        { edge.SourceNodeId, edge.SourcePortId, edge.TargetNodeId, edge.TargetPortId });
    private static bool IsHex(string? value, int length) => value != null && value.Length == length && value.All(char.IsAsciiHexDigit);
    private static CopilotCapabilityResult InvalidSource() => Failure(CopilotToolFailureKind.Internal, "The graph source returned inconsistent instance, revision, node, or edge evidence; no graph was published.");
    private static CopilotCapabilityResult InvalidCursor() => Failure(CopilotToolFailureKind.Validation,
        "The graph cursor or expected identity is invalid, stale, or belongs to another editor or query. Restart the requested stream with current graph and source identifiers.");
    private static CopilotCapabilityResult Failure(CopilotToolFailureKind kind, string message) => new()
    {
        Success = false, FailureKind = kind, Summary = "The current graph was not inspected.", ErrorMessage = message,
    };
}
