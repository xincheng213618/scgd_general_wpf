#pragma warning disable
using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using ColorVision.UI.ServiceHost;
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

            // 服务掉线/被 Windows 更新移除时，不能用空路径覆盖之前保存的可执行文件路径。
            UpdateSavedServicePath(ServiceConfig.Instance.RegistrationCenterServiceInfo, path => ServiceConfig.Instance.RegistrationCenterService = path);
            UpdateSavedServicePath(ServiceConfig.Instance.CVMainService_devInfo, path => ServiceConfig.Instance.CVMainService_dev = path);
            UpdateSavedServicePath(ServiceConfig.Instance.CVMainService_x64Info, path => ServiceConfig.Instance.CVMainService_x64 = path);
            UpdateSavedServicePath(ServiceConfig.Instance.CVArchServiceInfo, path => ServiceConfig.Instance.CVArchService = path);
        }

        public override async Task InitializeAsync()
        {

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
                        ServiceHostResponse response = await ColorVisionServiceHostClient.Default.StartServiceAsync(
                            "RegistrationCenterService",
                            timeoutSeconds: 45,
                            timeout: TimeSpan.FromSeconds(60));

                        if (!response.Success)
                        {
                            log.Info($"ColorVisionServiceHost 启动 RegistrationCenterService 服务失败：{response.Message}");
                            return;
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
                        bool rcInstalled = await InstallMissingServiceViaServiceHostAsync(
                            "RegistrationCenterService",
                            ServiceConfig.Instance.RegistrationCenterService,
                            startAfterInstall: true);

                        await InstallMissingServiceViaServiceHostAsync(
                            "CVMainService_x64",
                            ServiceConfig.Instance.CVMainService_x64,
                            startAfterInstall: false);

                        await InstallMissingServiceViaServiceHostAsync(
                            "CVMainService_dev",
                            ServiceConfig.Instance.CVMainService_dev,
                            startAfterInstall: false);

                        await InstallMissingServiceViaServiceHostAsync(
                            "CVArchService",
                            ServiceConfig.Instance.CVArchService,
                            startAfterInstall: false);

                        if (rcInstalled)
                        {
                            isConnect = await MqttRCService.GetInstance().Connect();
                            if (isConnect) return;
                        }
                    }


                    log.Info("未检测到 RegistrationCenterService 服务，请确认已正确安装。");
                    return;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return;
            }
        }

        private static async Task<bool> InstallMissingServiceViaServiceHostAsync(string serviceName, string? executablePath, bool startAfterInstall)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return false;

            if (ServiceExists(serviceName))
                return true;

            log.Info($"{serviceName} 服务未安装，正在通过 ColorVisionServiceHost 静默安装。");
            ServiceHostResponse response = await ColorVisionServiceHostClient.Default.InstallServiceAsync(
                serviceName,
                executablePath,
                displayName: serviceName,
                description: $"ColorVision service: {serviceName}",
                startAfterInstall: startAfterInstall,
                timeoutSeconds: 45,
                timeout: TimeSpan.FromSeconds(90));

            if (!response.Success)
            {
                log.Warn($"ColorVisionServiceHost 安装 {serviceName} 失败：{response.Message}");
                return false;
            }

            return true;
        }

        private static bool ServiceExists(string serviceName)
        {
            try
            {
                using ServiceController serviceController = new(serviceName);
                _ = serviceController.Status;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static void UpdateSavedServicePath(ServiceInfo serviceInfo, Action<string> updatePath)
        {
            if (serviceInfo.Exists && File.Exists(serviceInfo.ExecutablePath))
                updatePath(serviceInfo.ExecutablePath);
        }
    }
}
