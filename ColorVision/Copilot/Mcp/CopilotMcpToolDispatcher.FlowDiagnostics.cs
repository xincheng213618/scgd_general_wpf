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
            // These are excerpts selected by the flow host, not structured fault records.
            var failureExcerpts = snapshot?.FailureEvidence.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray()
                ?? Array.Empty<string>();
            var reportedFailureText = failureExcerpts.Length > 0
                ? string.Join(Environment.NewLine, failureExcerpts)
                : snapshot?.RecentFailureSummary ?? string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision flow failure diagnosis");
            builder.AppendLine("Mode: read-only diagnosis");
            builder.AppendLine("Would execute: False");
            builder.AppendLine("Flow execution allowed: False");
            builder.AppendLine($"Requested node: {EmptyLabel(nodeQuery)}");
            builder.AppendLine($"Log query: {EmptyLabel(logQuery)}");
            builder.AppendLine($"Requested maximum log lines: {maxLogLines}");

            builder.AppendLine();
            builder.AppendLine("## Observed Flow State");
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
            }

            builder.AppendLine();
            builder.AppendLine("## Flow Host Failure Excerpts");
            builder.AppendLine("Source: active flow snapshot. These host-selected excerpts are clues, not verified failures or root causes.");
            builder.AppendLine(string.IsNullOrWhiteSpace(reportedFailureText)
                ? "- No failure excerpt was provided; this does not establish that the flow succeeded."
                : RedactForDisplay(TrimLong(reportedFailureText, 4000)));

            builder.AppendLine();
            builder.AppendLine("## Related Node Configuration");
            builder.AppendLine("Node names, parameter names and configured values do not establish a fault.");
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
                    builder.AppendLine($"- Parameters: {RedactForDisplay(FormatFlowParameters(matchedNode.Parameters))}");
            }

            builder.AppendLine();
            builder.AppendLine("## Template Configuration");
            if (string.IsNullOrWhiteSpace(templateJson))
            {
                builder.AppendLine("- No active template JSON editor context is available.");
            }
            else
            {
                builder.AppendLine($"- Active template source: {EmptyLabel(liveContext?.SourceId)}");
                builder.AppendLine("These are configuration fields to inspect, not evidence that their values caused a failure.");
                AppendTemplateFieldHints(builder, templateJson, reportedFailureText);
            }

            builder.AppendLine();
            builder.AppendLine("## Recent Log Clues");
            builder.AppendLine("Source: recent application log search. These lines are not correlated to the current flow run, batch or node.");
            if (logResult.Success)
            {
                builder.AppendLine(RedactForDisplay(TrimLong(string.Join(Environment.NewLine, new[] { logResult.Summary, logResult.Content }.Where(value => !string.IsNullOrWhiteSpace(value))), 5000)));
            }
            else
            {
                builder.AppendLine($"- Recent log unavailable: {EmptyLabel(logResult.ErrorMessage ?? logResult.Summary)}");
            }

            builder.AppendLine();
            builder.AppendLine("## Diagnosis Limits and Next Checks");
            builder.AppendLine("- No root cause has been established by this tool. Status text, configuration fields and search keywords are not failure evidence.");
            builder.AppendLine("- The flow snapshot, selected node, template editor and application log are captured separately; they may describe different operations.");
            builder.AppendLine("- Match the flow/batch, node and timestamps in the original execution records before attributing a log entry or proposing a parameter change.");

            builder.AppendLine();
            builder.AppendLine("## Suggested Next MCP Calls");
            if (matchedNode != null)
                builder.AppendLine($"- preview_flow_action {{ \"action\": \"trace_recent_failure\", \"node_id\": \"{EscapeForInlineJson(matchedNode.NodeId)}\" }}");
            builder.AppendLine("- get_diagnostic_bundle { \"max_chars\": 12000 }");
            builder.AppendLine("- Use suggest_template_patch only when the user requests an adjustment and the correlated evidence supports specific fields; then preview_template_patch with explicit proposed_changes.");
            builder.AppendLine("No flow was started, stopped, run, rerun, or modified.");

            return CopilotMcpToolCallResult.Ok(RedactForDiagnostics(builder.ToString().TrimEnd()));
        }
    }
}
