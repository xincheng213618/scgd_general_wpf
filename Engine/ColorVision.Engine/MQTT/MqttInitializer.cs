﻿using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.MQTT
{
    public class MqttInitializer : IInitializer
    {
        private readonly IMessageUpdater _messageUpdater;

        public MqttInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public int Order => 2;
        public async Task InitializeAsync()
        {
            if (!MQTTSetting.Instance.IsUseMQTT)
            {
                _messageUpdater.Update("已经跳过MQTT服务器连接");
                await Task.Delay(10);
                return;
            }
            _messageUpdater.Update("正在检测MQTT服务器连接情况");


            bool isConnect = await MQTTControl.GetInstance().Connect();
            _messageUpdater.Update($"MQTT服务器连接{(MQTTControl.GetInstance().IsConnect ? Properties.Resources.Success : Properties.Resources.Failure)}");
            if (isConnect) return;

            if (MQTTControl.Config.Host == "127.0.0.1")
            {
                _messageUpdater.Update("检测到配置本机服务，正在尝试查找本机服务mosquitto");
                try
                {
                    ServiceController ServiceController = new ServiceController("Mosquitto Broker");
                    if (ServiceController != null)
                    {
                        _messageUpdater.Update($"检测服务mosquitto，状态{ServiceController.Status}，正在尝试启动服务");
                        if (Tool.ExecuteCommandAsAdmin("net start mosquitto"))
                        {
                            isConnect = await MQTTControl.GetInstance().Connect();
                            if (isConnect) return;
                        }
                        //if (!Common.Utilities.Tool.IsAdministrator())
                        //    Tool.RestartAsAdmin();
                        //ServiceController.Start();
                    }
                }
                catch
                {

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