using ColorVision.Engine.MQTT;
using ColorVision.UI.Desktop.Operations;
using ColorVision.UI.ServiceHost;
using System;
using System.ComponentModel;
using System.Linq;
using System.ServiceProcess;

namespace ColorVision.Engine.Services.Operations
{
    public sealed class WindowsOperationsServiceHealthProvider : IOperationsServiceHealthProvider
    {
        private const string MosquittoServiceName = "mosquitto";

        public OperationsServiceHealthReport Capture()
        {
            DateTimeOffset observedAt = DateTimeOffset.UtcNow;
            OperationsServiceHealthItem serviceHost = CaptureWindowsService(
                OperationsServiceIds.ServiceHost,
                "ColorVision 后台服务",
                ServiceHostProtocol.ServiceName,
                maintenanceSupported: false,
                observedAt);
            OperationsServiceHealthItem mqttBroker = UsesLocalMqttBroker()
                ? CaptureWindowsService(
                    OperationsServiceIds.MqttBroker,
                    "MQTT 消息服务",
                    MosquittoServiceName,
                    maintenanceSupported: true,
                    observedAt)
                : new OperationsServiceHealthItem
                {
                    ServiceId = OperationsServiceIds.MqttBroker,
                    Title = "MQTT 消息服务",
                    Status = "not_applicable",
                    Installed = false,
                    Healthy = true,
                    MaintenanceSupported = false,
                    StatusSource = "application-config",
                    ObservedAt = observedAt,
                };
            OperationsServiceHealthItem[] services = [serviceHost, mqttBroker];
            return new OperationsServiceHealthReport
            {
                Available = true,
                AllHealthy = services.All(item => item.Healthy),
                GeneratedAt = observedAt,
                Services = services,
            };
        }

        private static bool UsesLocalMqttBroker()
        {
            try
            {
                string host = MQTTControl.Config.Host?.Trim() ?? string.Empty;
                return host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                    || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || host.Equals("::1", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }

        private static OperationsServiceHealthItem CaptureWindowsService(
            string serviceId,
            string title,
            string windowsServiceName,
            bool maintenanceSupported,
            DateTimeOffset observedAt)
        {
            try
            {
                using ServiceController controller = new(windowsServiceName);
                string status = NormalizeStatus(controller.Status);
                return new OperationsServiceHealthItem
                {
                    ServiceId = serviceId,
                    Title = title,
                    Status = status,
                    Installed = true,
                    Healthy = status == "running",
                    MaintenanceSupported = maintenanceSupported,
                    StatusSource = "windows-service-control-manager",
                    ObservedAt = observedAt,
                };
            }
            catch (InvalidOperationException)
            {
                return Missing(serviceId, title, observedAt);
            }
            catch (Exception ex) when (ex is Win32Exception or NotSupportedException)
            {
                return new OperationsServiceHealthItem
                {
                    ServiceId = serviceId,
                    Title = title,
                    Status = "unknown",
                    Installed = true,
                    Healthy = false,
                    MaintenanceSupported = false,
                    StatusSource = "windows-service-control-manager",
                    ObservedAt = observedAt,
                };
            }
        }

        private static OperationsServiceHealthItem Missing(string serviceId, string title, DateTimeOffset observedAt) => new()
        {
            ServiceId = serviceId,
            Title = title,
            Status = "not_installed",
            Installed = false,
            Healthy = false,
            MaintenanceSupported = false,
            StatusSource = "windows-service-control-manager",
            ObservedAt = observedAt,
        };

        private static string NormalizeStatus(ServiceControllerStatus status) => status switch
        {
            ServiceControllerStatus.Running => "running",
            ServiceControllerStatus.Stopped => "stopped",
            ServiceControllerStatus.Paused => "paused",
            ServiceControllerStatus.StartPending => "start_pending",
            ServiceControllerStatus.StopPending => "stop_pending",
            ServiceControllerStatus.ContinuePending => "continue_pending",
            ServiceControllerStatus.PausePending => "pause_pending",
            _ => "unknown",
        };
    }
}
