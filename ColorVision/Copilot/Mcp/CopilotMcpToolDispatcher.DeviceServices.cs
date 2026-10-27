using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp;

internal sealed partial class CopilotMcpToolDispatcher
{
    internal async Task<CopilotMcpToolCallResult> InspectDeviceServicesAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deviceRef = arguments != null && arguments.TryGetValue("device_ref", out var device) ? device.GetString() : null;
        var expectedSource = arguments != null && arguments.TryGetValue("expected_source_instance_id", out var source) ? source.GetString() : null;
        var maximum = arguments != null && arguments.TryGetValue("max_devices", out var count) ? count.GetInt32() : 40;
        var cursor = arguments != null && arguments.TryGetValue("cursor", out var continuation) ? continuation.GetString() : null;
        var result = await new CopilotDeviceServicesInspectionService(_environment.DeviceServicesSnapshotProvider)
            .InspectAsync(deviceRef, expectedSource, maximum, cursor, cancellationToken);
        return ToMcpResult(result, "device_services_inspection_failed", contentOnly: true);
    }
}
