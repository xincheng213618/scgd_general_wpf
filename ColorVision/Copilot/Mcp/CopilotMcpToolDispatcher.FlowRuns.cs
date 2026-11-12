using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolDispatcher
{
    internal async Task<CopilotMcpToolCallResult> ListFlowRunsAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var flowKey = arguments != null && arguments.TryGetValue("flow_key", out var rawFlowKey) ? rawFlowKey.GetString() : null;
        var maximum = arguments != null && arguments.TryGetValue("max_results", out var rawMaximum) ? rawMaximum.GetInt32() : 20;
        var cursor = arguments != null && arguments.TryGetValue("cursor", out var rawCursor) ? rawCursor.GetString() : null;
        if (flowKey != null && string.IsNullOrWhiteSpace(flowKey))
            return ToMcpResult(new CopilotCapabilityResult { Success = false, FailureKind = CopilotToolFailureKind.Validation,
                ErrorMessage = "flow_key cannot be whitespace only." }, "flow_run_query_failed");
        var result = await _environment.FlowRunQueryProvider(flowKey, maximum, cursor, cancellationToken);
        return ToMcpResult(result, "flow_run_query_failed", contentOnly: true);
    }
}
