using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        internal async Task<CopilotMcpToolCallResult> InspectFlowRunHistoryAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runId = arguments!["run_record_id"].GetInt32();
            var nodeId = arguments.TryGetValue("node_id", out var rawNodeId) ? rawNodeId.GetString() : null;
            var maxRuns = arguments.TryGetValue("max_runs", out var rawMaxRuns) ? rawMaxRuns.GetInt32() : 10;
            var result = await _environment.FlowRunHistoryInspector(runId, nodeId, maxRuns, cancellationToken);
            return result.Success
                ? CopilotMcpToolCallResult.Ok(result.Content)
                : ToMcpResult(result, "flow_run_history_inspection_failed");
        }
    }
}
