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
        private async Task<CopilotMcpToolCallResult> DiagnoseFlowFailureAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var snapshot = await _environment.FlowSnapshotProvider(cancellationToken);
            var nodeQuery = FirstNonEmpty(GetString(arguments, "node_id"), GetString(arguments, "node_name"), GetString(arguments, "node"));
            var logQuery = FirstNonEmpty(GetString(arguments, "query"), GetString(arguments, "log_query"), "error");
            var maxLogLines = Math.Clamp(GetInt(arguments, "max_log_lines") ?? 120, 1, 300);
            var logResult = await _environment.RecentLogProvider(
                logQuery,
                CopilotRecentLogMode.RecentLines,
                maxLogLines,
                12000,
                cancellationToken);
            var liveContext = _environment.LiveContextProvider();
            var templateJson = ExtractCurrentTemplateJson();
            var matchedNode = snapshot == null ? null : FindFlowNode(snapshot, nodeQuery);
            var evidence = BuildFailureEvidenceText(snapshot, matchedNode, logResult, liveContext, templateJson);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision flow failure diagnosis");
            builder.AppendLine("Mode: read-only diagnosis");
            builder.AppendLine("Would execute: False");
            builder.AppendLine("Flow execution allowed: False");
            builder.AppendLine($"Requested node: {EmptyLabel(nodeQuery)}");
            builder.AppendLine($"Log query: {EmptyLabel(logQuery)}");
            builder.AppendLine($"Inspected log lines: {maxLogLines}");

            builder.AppendLine();
            builder.AppendLine("## Observed Symptoms");
            if (snapshot == null)
            {
                builder.AppendLine("- No active flow snapshot is available.");
            }
            else
            {
                builder.AppendLine($"- Flow: {EmptyLabel(snapshot.FlowName)}");
                builder.AppendLine($"- Status: {EmptyLabel(snapshot.Status)}");
                builder.AppendLine($"- Running: {snapshot.IsRunning}");
                builder.AppendLine($"- Batch status: {EmptyLabel(snapshot.BatchStatus)}");
                builder.AppendLine($"- Batch result: {EmptyLabel(snapshot.BatchResult)}");
                builder.AppendLine($"- Last node: {EmptyLabel(snapshot.LastNodeSummary)}");
                builder.AppendLine($"- Recent failure: {EmptyLabel(snapshot.RecentFailureSummary)}");
            }

            builder.AppendLine();
            builder.AppendLine("## Related Node");
            if (matchedNode == null)
            {
                builder.AppendLine(string.IsNullOrWhiteSpace(nodeQuery)
                    ? "- No node was selected or requested."
                    : "- No node matched the requested node id/name.");
            }
            else
            {
                builder.AppendLine($"- Title: {EmptyLabel(FirstNonEmpty(matchedNode.Title, matchedNode.NodeName, matchedNode.NodeId))}");
                builder.AppendLine($"- Id: {EmptyLabel(matchedNode.NodeId)}");
                builder.AppendLine($"- Type: {EmptyLabel(matchedNode.NodeType)}");
                builder.AppendLine($"- Active: {matchedNode.IsActive}");
                builder.AppendLine($"- Selected: {matchedNode.IsSelected}");
                builder.AppendLine($"- Mark: {EmptyLabel(matchedNode.Mark)}");
                if (matchedNode.Parameters.Count > 0)
                    builder.AppendLine($"- Parameters: {RedactForDisplay(string.Join(", ", matchedNode.Parameters.Select(item => $"{item.Name}={item.Value}")))}");
            }

            builder.AppendLine();
            builder.AppendLine("## Template Context");
            if (string.IsNullOrWhiteSpace(templateJson))
            {
                builder.AppendLine("- No active template JSON editor context is available.");
            }
            else
            {
                builder.AppendLine($"- Active template source: {EmptyLabel(liveContext?.SourceId)}");
                AppendTemplateFieldHints(builder, templateJson, evidence);
            }

            builder.AppendLine();
            builder.AppendLine("## Recent Log Clues");
            if (logResult.Success)
            {
                builder.AppendLine(RedactForDisplay(TrimLong(string.Join(Environment.NewLine, new[] { logResult.Summary, logResult.Content }.Where(value => !string.IsNullOrWhiteSpace(value))), 5000)));
            }
            else
            {
                builder.AppendLine($"- Recent log unavailable: {EmptyLabel(logResult.ErrorMessage ?? logResult.Summary)}");
            }

            builder.AppendLine();
            builder.AppendLine("## Likely Causes");
            foreach (var cause in BuildLikelyFailureCauses(evidence))
                builder.AppendLine("- " + cause);

            builder.AppendLine();
            builder.AppendLine("## Suggested Next MCP Calls");
            if (matchedNode != null)
                builder.AppendLine($"- preview_flow_action {{ \"action\": \"trace_recent_failure\", \"node_id\": \"{EscapeForInlineJson(matchedNode.NodeId)}\" }}");
            builder.AppendLine("- get_diagnostic_bundle { \"max_chars\": 12000 }");
            builder.AppendLine("- suggest_template_patch { \"intent\": \"summarize the suspected parameter adjustment\", \"node_name\": \"" + EscapeForInlineJson(FirstNonEmpty(matchedNode?.Title ?? string.Empty, nodeQuery)) + "\" }");
            builder.AppendLine("- preview_template_patch only after choosing explicit proposed_changes.");
            builder.AppendLine("No flow was started, stopped, run, rerun, or modified.");

            return CopilotMcpToolCallResult.Ok(RedactForDiagnostics(builder.ToString().TrimEnd()));
        }
    }
}
