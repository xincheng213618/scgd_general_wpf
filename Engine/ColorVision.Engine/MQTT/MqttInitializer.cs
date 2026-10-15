using ColorVision.UI;
using ColorVision.UI.ServiceHost;
using log4net;
using System;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace ColorVision.Engine.MQTT
{
    public class MqttInitializer : InitializerBase, IInitializerDependencies
    {
        private const string MosquittoServiceName = "mosquitto";
        private static readonly ILog log = LogManager.GetLogger(typeof(MqttInitializer));

        public override string Name => nameof(MqttInitializer);
        public override int Order => 2;
        public System.Collections.Generic.IReadOnlyCollection<string> Dependencies => [];
        public override async Task InitializeAsync()
        {
            MQTTControl mqtt = MQTTControl.GetInstance();
            if (await mqtt.Connect()) return;

            try
            {
                using ServiceController service = new(MosquittoServiceName);
                ServiceControllerStatus status = service.Status;
                log.Info($"Detected the mosquitto service with status {status}.");
                if (status == ServiceControllerStatus.Stopped || status == ServiceControllerStatus.Paused)
                {
                    ServiceHostResponse response = await ColorVisionServiceHostClient.Default.StartServiceAsync(
                        MosquittoServiceName,
                        timeoutSeconds: 45,
                        timeout: TimeSpan.FromSeconds(60));
                    if (!response.Success)
                    {
                        log.Info($"ColorVisionServiceHost failed to start the mosquitto service: {response.Message}");
                        return;
                    }
                }
                await mqtt.Connect();
            }
            catch (InvalidOperationException)
            {
                log.Info("The mosquitto service was not found. Confirm that it is installed correctly.");
            }
            catch (Exception ex)
            {
                log.Info(ex.Message);
            }
        }
    }
}
