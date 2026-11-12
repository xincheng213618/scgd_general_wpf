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

        internal async Task<CopilotMcpToolCallResult> GetFlowGraphAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var nodeId = arguments != null && arguments.TryGetValue("node_id", out var node) ? node.GetString() : null;
            var includeProperties = GetBool(arguments, "include_properties") ?? false;
            var maxNodes = GetInt(arguments, "max_nodes") ?? 80;
            var section = arguments != null && arguments.TryGetValue("section", out var selectedSection) ? selectedSection.GetString()! : "nodes";
            if (section == "properties" && !includeProperties)
                return CopilotMcpToolCallResult.Fail("invalid_arguments", "The properties detail stream requires include_properties=true.", CopilotToolFailureKind.Validation);
            var maxEdges = GetInt(arguments, "max_edges") ?? 80;
            var maxItems = GetInt(arguments, "max_items") ?? 80;
            var revision = arguments != null && arguments.TryGetValue("expected_revision", out var expected) ? expected.GetString() : null;
            var sourceInstance = arguments != null && arguments.TryGetValue("expected_source_instance_id", out var expectedSource) ? expectedSource.GetString() : null;
            var cursor = arguments != null && arguments.TryGetValue("cursor", out var continuation) ? continuation.GetString() : null;
            var result = await new CopilotFlowGraphInspectionService(_environment.FlowSnapshotProvider).InspectAsync(
                nodeId, includeProperties, maxNodes, section, maxEdges, revision, cursor, cancellationToken, sourceInstance, maxItems);
            return ToMcpResult(result, "flow_graph_inspection_failed", contentOnly: true);
        }

        internal async Task<CopilotMcpToolCallResult> GetFlowNodeCatalogAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
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
                guidance = "Choose an exact typeKey. Property declaredEnumNames preserve CLR member spelling; booleanStringCandidates are canonical strings for the default boolean descriptor. These are metadata, not a complete allowed-value list: custom descriptors and setters may use other syntax or reject candidates. Empty candidates do not mean read-only; inspect isWritable and descriptorType and use PreviewFlowPatch / preview_flow_patch to verify the exact proposed value. Do not infer numeric limits or flags combinations from this metadata. If algorithmHelpTopicId is non-empty, pass that exact ID to ReadAlgorithmHelp / read_algorithm_help for algorithm principles and parameter guidance; use InspectFlowGraph / get_flow_graph for current values. If several camera nodes match, ask which camera/device behavior is intended before proposing a mutation.",
            };
            return CopilotMcpToolCallResult.Ok(JsonSerializer.Serialize(payload, StructuredJsonOptions));
        }
    }
}
