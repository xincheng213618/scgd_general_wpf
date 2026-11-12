using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolDispatcher
{
    internal async Task<CopilotMcpToolCallResult> ValidateFlowGraphAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var revision = arguments!["expected_revision"].GetString()!;
        var result = await new CopilotFlowGraphValidationService(_environment.FlowGraphValidator).ValidateAsync(revision, cancellationToken);
        return ToMcpResult(result, "flow_graph_validation_failed", contentOnly: true);
    }
}
