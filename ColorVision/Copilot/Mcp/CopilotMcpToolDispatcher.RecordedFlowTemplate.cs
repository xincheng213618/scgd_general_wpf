using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolDispatcher
{
    internal async Task<CopilotMcpToolCallResult> InspectRecordedFlowTemplateAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runId = arguments!["run_record_id"].GetInt32();
        var nodeId = arguments.TryGetValue("node_id", out var node) ? node.GetString() : null;
        var maximum = arguments.TryGetValue("max_nodes", out var count) ? count.GetInt32() : 80;
        var result = await _environment.RecordedFlowTemplateInspector(runId, nodeId, maximum, cancellationToken);
        return result.Success ? CopilotMcpToolCallResult.Ok(result.Content) : ToMcpResult(result, "recorded_flow_template_inspection_failed");
    }
}
