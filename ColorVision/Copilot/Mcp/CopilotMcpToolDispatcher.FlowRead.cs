using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private async Task<CopilotMcpToolCallResult> GetFlowSummaryAsync(CancellationToken cancellationToken)
        {
            var snapshot = await _environment.FlowSnapshotProvider(cancellationToken);
            if (snapshot == null)
                return CopilotMcpToolCallResult.Ok("No active flow is available.");

            return CopilotMcpToolCallResult.Ok(FormatFlowSnapshot(snapshot));
        }

        private async Task<CopilotMcpToolCallResult> GetFlowGraphAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var snapshot = await _environment.FlowSnapshotProvider(cancellationToken);
            if (snapshot == null)
                return CopilotMcpToolCallResult.Ok("No active flow is available.");

            var nodeId = GetString(arguments, "node_id");
            var includeProperties = GetBool(arguments, "include_properties") ?? false;
            var maxNodes = Math.Clamp(GetInt(arguments, "max_nodes") ?? 80, 1, 200);
            var candidates = string.IsNullOrWhiteSpace(nodeId)
                ? snapshot.Nodes
                : snapshot.Nodes.Where(node => string.Equals(node.InstanceId, nodeId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.NodeId, nodeId, StringComparison.OrdinalIgnoreCase)).ToArray();
            var nodes = candidates.Take(maxNodes).ToArray();
            var nodeIds = nodes.Select(node => node.InstanceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var edges = snapshot.Edges.Where(edge => nodeIds.Contains(edge.SourceNodeId) && nodeIds.Contains(edge.TargetNodeId)).ToArray();
            var payload = new
            {
                format = "colorvision.flow-graph.v1",
                snapshot.Revision,
                snapshot.FlowName,
                snapshot.TemplateId,
                snapshot.Status,
                snapshot.IsRunning,
                totalNodeCount = snapshot.Nodes.Count,
                returnedNodeCount = nodes.Length,
                isTruncated = candidates.Count > nodes.Length,
                nodes = nodes.Select(node => new
                {
                    node.InstanceId,
                    node.NodeId,
                    node.TypeKey,
                    node.RuntimeType,
                    node.CategoryPath,
                    node.Title,
                    node.NodeName,
                    node.NodeType,
                    node.DeviceCode,
                    position = new { node.Left, node.Top, node.Width, node.Height },
                    node.Mark,
                    node.IsActive,
                    node.IsSelected,
                    inputs = node.InputPorts,
                    outputs = node.OutputPorts,
                    properties = includeProperties
                        ? node.Parameters.Select(property => new { property.Name, Value = CopilotMcpAuditLogger.RedactArgument(property.Name, property.Value) }).ToArray()
                        : Array.Empty<object>(),
                }),
                edges,
            };
            return CopilotMcpToolCallResult.Ok(JsonSerializer.Serialize(payload, StructuredJsonOptions));
        }

        private async Task<CopilotMcpToolCallResult> GetFlowNodeCatalogAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var query = GetString(arguments, "query");
            var maxResults = Math.Clamp(GetInt(arguments, "max_results") ?? 30, 1, 100);
            var catalog = await _environment.FlowNodeCatalogProvider(query, maxResults, cancellationToken);
            if (catalog == null)
                return CopilotMcpToolCallResult.Ok("No active Flow node catalog is available.");

            var payload = new
            {
                format = "colorvision.flow-node-catalog.v1",
                catalog.Query,
                catalog.TotalMatches,
                catalog.IsTruncated,
                nodeTypes = catalog.NodeTypes,
                guidance = "Choose an exact typeKey. If several camera nodes match, ask which camera/device behavior is intended before proposing a mutation.",
            };
            return CopilotMcpToolCallResult.Ok(JsonSerializer.Serialize(payload, StructuredJsonOptions));
        }
    }
}
