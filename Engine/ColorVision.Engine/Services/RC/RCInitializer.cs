#pragma warning disable
using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using log4net;
using log4net.Util;
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

        public ServiceInfo RegistrationCenterServiceInfo { get; set; } = new ServiceInfo();

        public ServiceInfo CVMainService_devInfo { get; set; } = new ServiceInfo();

        public ServiceInfo CVMainService_x64Info { get; set; } = new ServiceInfo();

        public ServiceInfo CVArchServiceInfo { get; set; } = new ServiceInfo();
    }




    public class RCInitializer : InitializerBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TemplateInitializer));


        public override string Name => nameof(RCInitializer);
        public override IEnumerable<string> Dependencies => new List<string>() { nameof(MqttInitializer) };
        public override int Order => 4;


        public async Task SetServiceConfig()
        {
            await Task.Delay(1000);
            // 获取详细信息
            ServiceConfig.Instance.RegistrationCenterServiceInfo = ServiceInfo.FromServiceName("RegistrationCenterService");
            ServiceConfig.Instance.CVMainService_devInfo = ServiceInfo.FromServiceName("CVMainService_dev");
            ServiceConfig.Instance.CVMainService_x64Info = ServiceInfo.FromServiceName("CVMainService_x64");
            ServiceConfig.Instance.CVArchServiceInfo = ServiceInfo.FromServiceName("CVArchService");

            // 保持兼容性，同时更新路径字符串
            ServiceConfig.Instance.RegistrationCenterService = ServiceConfig.Instance.RegistrationCenterServiceInfo.ExecutablePath;
            ServiceConfig.Instance.CVMainService_dev = ServiceConfig.Instance.CVMainService_devInfo.ExecutablePath;
            ServiceConfig.Instance.CVMainService_x64 = ServiceConfig.Instance.CVMainService_x64Info.ExecutablePath;
            ServiceConfig.Instance.CVArchService = ServiceConfig.Instance.CVArchServiceInfo.ExecutablePath;
        }

        public override async Task InitializeAsync()
        {
            if (!RCSetting.Instance.IsUseRCService)
            {
                log.Info("跳过注册中心连接");
                return;
            }

            log.Info("正在尝试连接注册中心");
            bool isConnect = await MqttRCService.GetInstance().Connect();
            if (isConnect)
            {
                _ = Task.Run(SetServiceConfig);
                return;
            }

            try
            {
                ServiceController serviceController = new ServiceController("RegistrationCenterService");
                try
                {
                    var status = serviceController.Status; // 如果服务不存在会抛出异常
                    log.Info("检测到本地注册中心配置, 正在尝试启动");

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
                                log.Info("以管理员权限启动 RegistrationCenterService 服务失败。");
                                ShowManualConnectDialog();
                                return;
                            }
                        }
                    }
                    else if (status == ServiceControllerStatus.Running)
                    {
                        log.Info("RegistrationCenterService 服务已在运行。");
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
                                log.Info("创建服务失败");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"创建服务失败: {ex.Message}");
                        }

                        if (!Tool.ExecuteCommandAsAdmin("net start RegistrationCenterService"))
                        {
                            log.Info("以管理员权限启动 RegistrationCenterService 服务失败。");
                            ShowManualConnectDialog();
                            return;
                        }
                        isConnect = await MqttRCService.GetInstance().Connect();
                        if (isConnect) return;
                    }


                    log.Info("未检测到 RegistrationCenterService 服务，请确认已正确安装。");
                    ShowManualConnectDialog();
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
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
