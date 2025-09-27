using ColorVision.Common.Utilities;
using ColorVision.UI;
using log4net;
using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.MQTT
{
    public class MqttInitializer : InitializerBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MqttInitializer));

        public MqttInitializer() { }
        public override string Name => nameof(MqttInitializer);
        public override int Order => 2;
        public override async Task InitializeAsync()
        {
            if (!MQTTSetting.Instance.IsUseMQTT)
            {
                log.Info("已经跳过MQTT服务器连接");
                return;
            }
            log.Info("正在检测MQTT服务器连接情况");

            bool isConnect = await MQTTControl.GetInstance().Connect();
            log.Info($"MQTT服务器连接{(MQTTControl.GetInstance().IsConnect ? Properties.Resources.Success : Properties.Resources.Failure)}");
            if (isConnect) return;

            if (MQTTControl.Config.Host == "127.0.0.1" || MQTTControl.Config.Host == "localhost")
            {
                log.Info("检测到配置本机服务，正在尝试查找本机服务mosquitto");
                try
                {
                    ServiceController serviceController = new ServiceController("Mosquitto Broker");
                    try
                    {
                        var status = serviceController.Status;
                        log.Info($"检测服务mosquitto，状态 {status}，正在尝试启动服务");

                        if (status == ServiceControllerStatus.Stopped || status == ServiceControllerStatus.Paused)
                        {
                            if (Tool.IsAdministrator())
                            {
                                serviceController.Start();
                            }
                            else
                            {
                                if (!Tool.ExecuteCommandAsAdmin("net start mosquitto"))
                                {
                                    log.Info("以管理员权限启动 mosquitto 服务失败。");
                                    return;
                                }
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
                        log.Info("未检测到 Mosquitto Broker 服务，请确认已正确安装。");
                    }
                }
                catch (Exception ex)
                {
                    log.Info(ex.Message);
                }
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                MQTTConnect mQTTConnect = new() { Owner = Application.Current.GetActiveWindow() };
                mQTTConnect.ShowDialog();
            });
        }
    }
    }
