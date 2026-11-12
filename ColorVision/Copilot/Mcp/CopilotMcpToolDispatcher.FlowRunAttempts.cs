using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolDispatcher
{
    internal async Task<CopilotMcpToolCallResult> InspectFlowRunAttemptsAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runId = arguments!["run_record_id"].GetInt32();
        var nodeId = arguments.TryGetValue("node_id", out var node) ? node.GetString() : null;
        var maximum = arguments.TryGetValue("max_attempts", out var count) ? count.GetInt32() : 40;
        var cursor = arguments.TryGetValue("cursor", out var continuation) ? continuation.GetString() : null;
        if (nodeId != null && string.IsNullOrWhiteSpace(nodeId))
            return ToMcpResult(new CopilotCapabilityResult { Success = false, FailureKind = CopilotToolFailureKind.Validation,
                ErrorMessage = "node_id cannot be whitespace only." }, "flow_run_attempts_inspection_failed", contentOnly: true);
        var result = await _environment.FlowRunAttemptsInspector(runId, nodeId, maximum, cursor, cancellationToken);
        return ToMcpResult(result, "flow_run_attempts_inspection_failed", contentOnly: true);
    }
}
