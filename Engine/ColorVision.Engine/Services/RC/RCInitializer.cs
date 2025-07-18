#pragma warning disable
using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.RC
{

    public static class ServiceManagerUitl
    {
        /// <summary>
        /// 根据服务名获取服务的可执行文件路径（安装路径）
        /// </summary>
        public static string? GetServiceExecutablePath(string serviceName)
        {
            string regPath = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
            using (var key = Registry.LocalMachine.OpenSubKey(regPath))
            {
                if (key == null) return null;
                var imagePath = key.GetValue("ImagePath") as string;
                if (string.IsNullOrWhiteSpace(imagePath)) return null;

                // 去除可能的引号和参数，只取主程序路径
                imagePath = imagePath.Trim();
                if (imagePath.StartsWith("\""))
                {
                    int endQuote = imagePath.IndexOf('\"', 1);
                    if (endQuote > 1)
                        imagePath = imagePath.Substring(1, endQuote - 1);
                }
                else
                {
                    int firstSpace = imagePath.IndexOf(' ');
                    if (firstSpace > 0)
                        imagePath = imagePath.Substring(0, firstSpace);
                }
                return imagePath;
            }
        }
    }

    public class ServiceConfig:IConfig
    {
        public static ServiceConfig Instance => ConfigService.Instance.GetRequiredService<ServiceConfig>();

        public string RegistrationCenterService { get; set; } 
        public string CVMainService_dev { get; set; }
        public string CVMainService_x64 { get; set; }

        public string CVArchService { get; set; }
    }




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


        public async Task SetServiceConfig()
        {
            await Task.Delay(100);
            ServiceConfig.Instance.RegistrationCenterService = ServiceManagerUitl.GetServiceExecutablePath("RegistrationCenterService") ?? string.Empty;
            ServiceConfig.Instance.CVMainService_dev = ServiceManagerUitl.GetServiceExecutablePath("CVMainService_dev") ?? string.Empty;
            ServiceConfig.Instance.CVMainService_x64 = ServiceManagerUitl.GetServiceExecutablePath("CVMainService_x64") ?? string.Empty;
            ServiceConfig.Instance.CVArchService = ServiceManagerUitl.GetServiceExecutablePath("CVArchService") ?? string.Empty;       
        }

        public override async Task InitializeAsync()
        {
            if (!RCSetting.Instance.IsUseRCService)
            {
                _messageUpdater.Update("跳过注册中心连接");
                return;
            }

            _messageUpdater.Update("正在尝试连接注册中心");
            bool isConnect = await MqttRCService.GetInstance().Connect();
            if (isConnect)
            {
                if (!File.Exists(ServiceConfig.Instance.RegistrationCenterService))
                {
                   _= Task.Run(SetServiceConfig);
                }
                return;
            }

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
                    if (File.Exists(ServiceConfig.Instance.RegistrationCenterService))
                    {
                        try
                        {
                            string creatrcmd = string.Empty;
                            string crcmd(string serviceName,string exePath)
                            {
                                string Cmd = $"sc create {serviceName} binPath= \"{exePath}\" start= delayed-auto DisplayName= \"{serviceName}\"";
                                return Cmd;
                            }
                            creatrcmd += crcmd("RegistrationCenterService", ServiceConfig.Instance.RegistrationCenterService);
                            if (File.Exists(ServiceConfig.Instance.CVMainService_x64))
                            {
                                creatrcmd += "&&";
                                creatrcmd +=  crcmd("CVMainService_x64", ServiceConfig.Instance.CVMainService_x64);
                            }
                            if (File.Exists(ServiceConfig.Instance.CVMainService_dev))
                            {
                                creatrcmd += "&&";
                                creatrcmd += crcmd("CVMainService_dev", ServiceConfig.Instance.CVMainService_dev);
                            }
                            if (File.Exists(ServiceConfig.Instance.CVArchService))
                            {
                                creatrcmd += "&&";
                                creatrcmd += crcmd("CVArchService", ServiceConfig.Instance.CVArchService);
                            }
                            if (!Tool.ExecuteCommandAsAdmin(creatrcmd))
                            {
                                _messageUpdater.Update("创建服务失败");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"创建服务失败: {ex.Message}");
                        }

                        if (!Tool.ExecuteCommandAsAdmin("net start RegistrationCenterService"))
                        {
                            _messageUpdater.Update("以管理员权限启动 RegistrationCenterService 服务失败。");
                            ShowManualConnectDialog();
                            return;
                        }
                        isConnect = await MqttRCService.GetInstance().Connect();
                        if (isConnect) return;
                    }


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
