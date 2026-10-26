using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        internal async Task<CopilotMcpToolCallResult> InspectFlowIncidentAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var incidentId = arguments!["incident_id"].GetInt64();
            var maxEvents = arguments.TryGetValue("max_events", out var rawMaxEvents)
                ? rawMaxEvents.GetInt32()
                : 40;
            var result = await _environment.FlowIncidentInspector(incidentId, maxEvents, cancellationToken);
            return result.Success
                ? CopilotMcpToolCallResult.Ok(result.Content)
                : ToMcpResult(result, "flow_incident_inspection_failed");
        }
    }
}
