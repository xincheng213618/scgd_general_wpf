using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolDispatcher
{
    internal async Task<CopilotMcpToolCallResult> CompareRecordedFlowTemplatesAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var baseline = arguments!["baseline_run_record_id"].GetInt32();
        var current = arguments["current_run_record_id"].GetInt32();
        var maximum = arguments.TryGetValue("max_changes", out var count) ? count.GetInt32() : 50;
        var cursor = arguments.TryGetValue("cursor", out var continuation) ? continuation.GetString() : null;
        var result = await _environment.RecordedFlowTemplateComparer(baseline, current, maximum, cursor, cancellationToken);
        return ToMcpResult(result, "recorded_flow_template_comparison_failed", contentOnly: true);
    }
}
