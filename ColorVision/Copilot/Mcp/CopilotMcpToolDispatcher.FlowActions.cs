using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private async Task<CopilotMcpToolCallResult> PreviewFlowActionAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var action = NormalizeActionName(GetString(arguments, "action"));
            if (string.IsNullOrWhiteSpace(action))
                return CopilotMcpToolCallResult.Fail("missing_flow_action", "The preview_flow_action tool requires action.");

            if (IsForbiddenFlowExecutionAction(action))
            {
                return CopilotMcpToolCallResult.Fail(
                    "flow_execution_not_supported",
                    "risk_level: confirmation-required\nwould_execute: False\nexecution_status: not_supported_current_stage\nFlow start/stop/run requests are intentionally not executed by ColorVision MCP. Suggested next step: inspect the flow summary and preview a node-specific diagnostic action instead.");
            }

            if (action != "select_node" && action != "open_node_property" && action != "inspect_node_errors" && action != "explain_node" && action != "trace_recent_failure")
            {
                return CopilotMcpToolCallResult.Fail("unsupported_flow_preview_action", "Supported preview actions: select_node, open_node_property, inspect_node_errors, explain_node, trace_recent_failure. Flow start/stop/run is not supported.");
            }

            var snapshot = await _environment.FlowSnapshotProvider(cancellationToken);
            if (snapshot == null)
                return CopilotMcpToolCallResult.Ok("No active flow is available. would_execute: False");

            var nodeQuery = FirstNonEmpty(GetString(arguments, "node_id"), GetString(arguments, "node_name"), GetString(arguments, "node"));
            var matchedNode = FindFlowNode(snapshot, nodeQuery);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision flow action preview");
            builder.AppendLine($"Action: {action}");
            builder.AppendLine("Mode: preview only");
            builder.AppendLine("Would execute: False");
            builder.AppendLine("Flow execution allowed: False");
            builder.AppendLine($"Flow name: {EmptyLabel(snapshot.FlowName)}");
            builder.AppendLine($"Node count: {snapshot.Nodes.Count}");
            builder.AppendLine($"Requested node: {EmptyLabel(nodeQuery)}");

            if (matchedNode != null)
            {
                builder.AppendLine($"Matched node: {EmptyLabel(FirstNonEmpty(matchedNode.Title, matchedNode.NodeName, matchedNode.NodeId))}");
                builder.AppendLine($"Matched node id: {EmptyLabel(matchedNode.NodeId)}");
                builder.AppendLine($"Matched node type: {EmptyLabel(matchedNode.NodeType)}");
                builder.AppendLine($"Matched node selected: {matchedNode.IsSelected}");
                if (action == "inspect_node_errors" || action == "trace_recent_failure")
                {
                    builder.AppendLine($"Node mark: {EmptyLabel(matchedNode.Mark)}");
                    builder.AppendLine($"Recent flow failure summary: {EmptyLabel(snapshot.RecentFailureSummary)}");
                }
                if (action == "explain_node")
                {
                    builder.AppendLine($"Node active: {matchedNode.IsActive}");
                    AppendList(builder, "Node inputs", matchedNode.Inputs);
                    AppendList(builder, "Node outputs", matchedNode.Outputs);
                    if (matchedNode.Parameters.Count > 0)
                        builder.AppendLine($"Node parameters: {RedactForDisplay(string.Join(", ", matchedNode.Parameters.Select(item => $"{item.Name}={item.Value}")))}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(nodeQuery))
            {
                builder.AppendLine("Matched node: (none)");
                builder.AppendLine("Available nodes:");
                foreach (var node in snapshot.Nodes.Take(20))
                    builder.AppendLine($"- {EmptyLabel(FirstNonEmpty(node.Title, node.NodeName, node.NodeId))} [{EmptyLabel(node.NodeId)}]");
            }

            if (action == "trace_recent_failure")
            {
                builder.AppendLine($"Recent failure summary: {EmptyLabel(snapshot.RecentFailureSummary)}");
                builder.AppendLine($"Last node: {EmptyLabel(snapshot.LastNodeSummary)}");
                if (!string.IsNullOrWhiteSpace(snapshot.RecentRunMessage))
                {
                    builder.AppendLine("Recent run message:");
                    builder.AppendLine(TrimLong(RedactForDisplay(snapshot.RecentRunMessage), 2000));
                }
            }

            builder.AppendLine("Suggested next steps:");
            foreach (var suggestion in BuildFlowPreviewSuggestions(action, matchedNode, snapshot))
                builder.AppendLine("- " + suggestion);

            builder.AppendLine("No flow was started, stopped, run, rerun, or modified.");
            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }
    }
}
