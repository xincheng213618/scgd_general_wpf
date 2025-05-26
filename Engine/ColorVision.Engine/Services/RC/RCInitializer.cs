using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.RC
{
    public class RCInitializer : InitializerBase
    {
        private readonly IMessageUpdater _messageUpdater;

        public RCInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }
        public override string Name => nameof(RCInitializer);
        public override IEnumerable<string> Dependencies => new List<string>() { nameof(MqttInitializer) };
        public override int Order => 4;
        public override async Task InitializeAsync()
        {
            if (!RCSetting.Instance.IsUseRCService)
            {
                _messageUpdater.Update("跳过注册中心连接");
                return;
            }

            _messageUpdater.Update("正在尝试连接注册中心");
            bool isConnect = await MqttRCService.GetInstance().Connect();
            if (isConnect) return;

            try
            {
                ServiceController serviceController = new ServiceController("RegistrationCenterService");
                try
                {
                    var status = serviceController.Status; // 如果服务不存在会抛出异常
                    _messageUpdater.Update("检测到本地注册中心配置, 正在尝试启动");

                    if (status == ServiceControllerStatus.Stopped || status == ServiceControllerStatus.Paused)
                    {
                        if (Tool.IsAdministrator())
                        {
                            serviceController.Start();
                            serviceController.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(5));
                        }
                        else
                        {
                            if (!Tool.ExecuteCommandAsAdmin("net start RegistrationCenterService"))
                            {
                                _messageUpdater.Update("以管理员权限启动 RegistrationCenterService 服务失败。");
                                ShowManualConnectDialog();
                                return;
                            }
                        }
                    }
                    else if (status == ServiceControllerStatus.Running)
                    {
                        _messageUpdater.Update("RegistrationCenterService 服务已在运行。");
                    }

                    isConnect = await MqttRCService.GetInstance().Connect();
                    if (isConnect) return;
                }
                catch (InvalidOperationException)
                {
                    _messageUpdater.Update("未检测到 RegistrationCenterService 服务，请确认已正确安装。");
                    ShowManualConnectDialog();
                    return;
                }
            }
            catch (Exception ex)
            {
                _messageUpdater.Update("查找服务时异常: " + ex.Message);
                ShowManualConnectDialog();
                return;
            }

            ShowManualConnectDialog();

            void ShowManualConnectDialog()
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RCServiceConnect connect = new() { Owner = Application.Current.GetActiveWindow() };
                    connect.ShowDialog();
                });
            }
        }
    }
}
