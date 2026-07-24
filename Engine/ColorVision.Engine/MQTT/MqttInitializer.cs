using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.ServiceHost;
using log4net;
using System;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace ColorVision.Engine.MQTT
{
    public class MqttInitializer : InitializerBase
    {
        private const string MosquittoServiceName = "mosquitto";
        private static readonly ILog log = LogManager.GetLogger(typeof(MqttInitializer));

        public MqttInitializer() { }
        public override string Name => nameof(MqttInitializer);
        public override int Order => 2;
        public override async Task InitializeAsync()
        {
            log.Info("Checking the MQTT server connection.");

            bool isConnect = await MQTTControl.GetInstance().Connect();
            log.Info($"MQTT server connection {(MQTTControl.GetInstance().IsConnect ? "succeeded" : "failed")}.");
            if (isConnect) return;

            if (MQTTControl.Config.Host == "127.0.0.1" || MQTTControl.Config.Host == "localhost")
            {
                log.Info("The MQTT endpoint is local; checking for the mosquitto Windows service.");
                try
                {
                    ServiceController serviceController = new ServiceController(MosquittoServiceName);
                    try
                    {
                        var status = serviceController.Status;
                        log.Info($"Detected the mosquitto service with status {status}; attempting to start it if needed.");

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
                        else if (status == ServiceControllerStatus.Running)
                        {
                            log.Info("The mosquitto service is already running.");
                        }

                        isConnect = await MQTTControl.GetInstance().Connect();
                        if (isConnect) return;
                    }
                    catch (InvalidOperationException)
                    {
                        log.Info("The mosquitto service was not found. Confirm that it is installed correctly.");
                    }
                }
                catch (Exception ex)
                {
                    log.Info(ex.Message);
                }
            }
        }
    }
    }
