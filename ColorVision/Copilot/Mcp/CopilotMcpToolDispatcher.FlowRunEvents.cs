using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolDispatcher
{
    internal async Task<CopilotMcpToolCallResult> InspectFlowRunEventsAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runId = arguments!["run_record_id"].GetInt32();
        var maximum = arguments.TryGetValue("max_events", out var count) ? count.GetInt32() : 40;
        var cursor = arguments.TryGetValue("cursor", out var continuation) ? continuation.GetString() : null;
        var result = await _environment.FlowRunEventsInspector(runId, maximum, cursor, cancellationToken);
        return ToMcpResult(result, "flow_run_events_inspection_failed", contentOnly: true);
    }
}
