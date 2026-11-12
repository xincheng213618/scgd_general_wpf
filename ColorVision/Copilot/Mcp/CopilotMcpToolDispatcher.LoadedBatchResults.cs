using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolDispatcher
{
    internal async Task<CopilotMcpToolCallResult> InspectLoadedBatchResultsAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceRef = arguments != null && arguments.TryGetValue("source_ref", out var source) ? source.GetString() : null;
        var expectedSource = arguments != null && arguments.TryGetValue("expected_source_instance_id", out var owner) ? owner.GetString() : null;
        var resultRef = arguments != null && arguments.TryGetValue("result_ref", out var result) ? result.GetString() : null;
        var maximum = arguments != null && arguments.TryGetValue("max_items", out var count) ? count.GetInt32() : 40;
        var cursor = arguments != null && arguments.TryGetValue("cursor", out var continuation) ? continuation.GetString() : null;
        var captured = await new CopilotLoadedBatchResultsInspectionService(_environment.LoadedBatchResultsSnapshotProvider)
            .InspectAsync(sourceRef, expectedSource, resultRef, maximum, cursor, cancellationToken);
        return ToMcpResult(captured, "loaded_batch_results_inspection_failed", contentOnly: true);
    }
}
