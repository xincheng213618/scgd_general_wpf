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
            log.Info("正在检测MQTT服务器连接情况");

            bool isConnect = await MQTTControl.GetInstance().Connect();
            log.Info($"MQTT服务器连接{(MQTTControl.GetInstance().IsConnect ? Properties.Resources.Success : Properties.Resources.Failure)}");
            if (isConnect) return;

            if (MQTTControl.Config.Host == "127.0.0.1" || MQTTControl.Config.Host == "localhost")
            {
                log.Info("检测到配置本机服务，正在尝试查找本机服务mosquitto");
                try
                {
                    ServiceController serviceController = new ServiceController(MosquittoServiceName);
                    try
                    {
                        var status = serviceController.Status;
                        log.Info($"检测服务mosquitto，状态 {status}，正在尝试启动服务");

                        if (status == ServiceControllerStatus.Stopped || status == ServiceControllerStatus.Paused)
                        {
                            ServiceHostResponse response = await ColorVisionServiceHostClient.Default.StartServiceAsync(
                                MosquittoServiceName,
                                timeoutSeconds: 45,
                                timeout: TimeSpan.FromSeconds(60));

                            if (!response.Success)
                            {
                                log.Info($"ColorVisionServiceHost 启动 mosquitto 服务失败：{response.Message}");
                                return;
                            }
                        }
                        else if (status == ServiceControllerStatus.Running)
                        {
                            log.Info("mosquitto 服务已在运行。");
                        }

                        isConnect = await MQTTControl.GetInstance().Connect();
                        if (isConnect) return;
                    }
                    catch (InvalidOperationException)
                    {
                        log.Info("未检测到 mosquitto 服务，请确认已正确安装。");
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
