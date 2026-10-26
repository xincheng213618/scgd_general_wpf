using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        internal async Task<CopilotMcpToolCallResult> ListFlowIncidentsAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = arguments != null && arguments.TryGetValue("state", out var rawState)
                ? rawState.GetString()!
                : "active";
            var maxResults = arguments != null && arguments.TryGetValue("max_results", out var rawMaxResults)
                ? rawMaxResults.GetInt32()
                : 20;
            var cursor = arguments != null && arguments.TryGetValue("cursor", out var rawCursor)
                ? rawCursor.GetString()
                : null;
            var result = await _environment.FlowIncidentQueryProvider(state, maxResults, cursor, cancellationToken);
            return result.Success
                ? CopilotMcpToolCallResult.Ok(result.Content)
                : ToMcpResult(result, "flow_incident_query_failed");
        }
    }
}
