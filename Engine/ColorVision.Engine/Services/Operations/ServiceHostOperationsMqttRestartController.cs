using ColorVision.UI.Desktop.Operations;
using ColorVision.UI.ServiceHost;
using System;

namespace ColorVision.Engine.Services.Operations
{
    public sealed class ServiceHostOperationsMqttRestartController : IOperationsMqttRestartController
    {
        public OperationsMqttRestartResult Restart()
        {
            try
            {
                ServiceHostResponse response = ColorVisionServiceHostClient.Default
                    .RestartServiceAsync("mosquitto", timeoutSeconds: 60, timeout: TimeSpan.FromSeconds(90))
                    .GetAwaiter().GetResult();
                return new OperationsMqttRestartResult(
                    response.Success,
                    response.Success
                        ? $"servicehost:{response.RequestId}"
                        : $"servicehost:{response.RequestId}:{ClassifyFailure(response.Message)}");
            }
            catch (Exception ex)
            {
                return new OperationsMqttRestartResult(
                    false,
                    $"servicehost_error:{ex.GetType().Name}");
            }
        }

        private static string ClassifyFailure(string message)
        {
            if (message.Equals("unsupported_protocol_version", StringComparison.OrdinalIgnoreCase))
                return "protocol_mismatch";
            if (message.Equals("broker_ticket_target_not_allowed", StringComparison.OrdinalIgnoreCase))
                return "ticket_scope_rejected";
            if (message.StartsWith("Unsupported service name:", StringComparison.OrdinalIgnoreCase))
                return "service_not_allowed";
            if (message.StartsWith("Service was not found:", StringComparison.OrdinalIgnoreCase))
                return "service_not_found";
            if (message.Equals("service restart failed", StringComparison.OrdinalIgnoreCase))
                return "restart_failed";
            return "servicehost_rejected";
        }
    }
}
