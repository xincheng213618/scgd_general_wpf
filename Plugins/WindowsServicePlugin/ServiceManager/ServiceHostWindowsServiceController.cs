using ColorVision.UI.ServiceHost;

namespace WindowsServicePlugin.ServiceManager
{
    internal enum ServiceHostServiceOperation
    {
        Start,
        Stop,
        Restart,
        Terminate,
    }

    internal static class ServiceHostWindowsServiceController
    {
        public static async Task<bool> InstallAsync(
            string serviceName,
            string executablePath,
            Action<string> logCallback,
            string? displayName = null,
            bool startAfterInstall = false,
            string startType = "delayed-auto",
            string? description = null)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                logCallback("服务名为空，不能执行后台服务安装");
                return false;
            }

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                logCallback("服务可执行文件路径为空，不能执行后台服务安装");
                return false;
            }

            string name = string.IsNullOrWhiteSpace(displayName) ? serviceName : displayName;
            try
            {
                logCallback($"正在通过 ColorVisionServiceHost 后台安装 {name} ({serviceName})...");
                ServiceHostResponse response = await ColorVisionServiceHostClient.Default
                    .InstallServiceAsync(serviceName, executablePath, name, description ?? $"ColorVision service: {name}", startType, startAfterInstall)
                    .ConfigureAwait(true);

                if (response.Success)
                {
                    logCallback($"后台服务执行成功: {name} 安装完成");
                    return true;
                }

                logCallback($"后台服务执行失败: {name} 安装失败，{response.Message}");
                if (response.Message.Contains("Unsupported command", StringComparison.OrdinalIgnoreCase))
                {
                    logCallback("当前 ColorVisionServiceHost 版本过旧，请先在“更新 -> ColorVision Service Host”中更新后台服务。");
                }

                return false;
            }
            catch (Exception ex)
            {
                logCallback($"ColorVisionServiceHost 不可用或执行失败: {ex.Message}");
                logCallback("请先在“更新 -> ColorVision Service Host”中安装/更新后台服务。");
                return false;
            }
        }

        public static async Task<bool> UninstallAsync(
            string serviceName,
            Action<string> logCallback,
            string? displayName = null)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                logCallback("服务名为空，不能执行后台服务卸载");
                return false;
            }

            string name = string.IsNullOrWhiteSpace(displayName) ? serviceName : displayName;
            try
            {
                logCallback($"正在通过 ColorVisionServiceHost 后台卸载 {name} ({serviceName})...");
                ServiceHostResponse response = await ColorVisionServiceHostClient.Default
                    .UninstallServiceAsync(serviceName)
                    .ConfigureAwait(true);

                if (response.Success)
                {
                    logCallback($"后台服务执行成功: {name} 卸载完成");
                    return true;
                }

                logCallback($"后台服务执行失败: {name} 卸载失败，{response.Message}");
                if (response.Message.Contains("Unsupported command", StringComparison.OrdinalIgnoreCase))
                {
                    logCallback("当前 ColorVisionServiceHost 版本过旧，请先在“更新 -> ColorVision Service Host”中更新后台服务。");
                }

                return false;
            }
            catch (Exception ex)
            {
                logCallback($"ColorVisionServiceHost 不可用或执行失败: {ex.Message}");
                logCallback("请先在“更新 -> ColorVision Service Host”中安装/更新后台服务。");
                return false;
            }
        }

        public static async Task<bool> ExecuteAsync(
            string serviceName,
            ServiceHostServiceOperation operation,
            Action<string> logCallback,
            string? displayName = null,
            string? executablePath = null)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                logCallback("服务名为空，不能执行后台服务操作");
                return false;
            }

            string name = string.IsNullOrWhiteSpace(displayName) ? serviceName : displayName;
            string label = GetOperationLabel(operation);

            try
            {
                logCallback($"正在通过 ColorVisionServiceHost 后台{label} {name} ({serviceName})...");
                ServiceHostResponse response = operation switch
                {
                    ServiceHostServiceOperation.Start => await ColorVisionServiceHostClient.Default.StartServiceAsync(serviceName).ConfigureAwait(true),
                    ServiceHostServiceOperation.Stop => await ColorVisionServiceHostClient.Default.StopServiceAsync(serviceName).ConfigureAwait(true),
                    ServiceHostServiceOperation.Restart => await ColorVisionServiceHostClient.Default.RestartServiceAsync(serviceName).ConfigureAwait(true),
                    ServiceHostServiceOperation.Terminate => await ColorVisionServiceHostClient.Default.TerminateServiceAsync(serviceName, executablePath).ConfigureAwait(true),
                    _ => throw new InvalidOperationException($"Unsupported service operation: {operation}"),
                };

                if (response.Success)
                {
                    logCallback($"后台服务执行成功: {name} {label}完成");
                    return true;
                }

                logCallback($"后台服务执行失败: {name} {label}失败，{response.Message}");
                if (response.Message.Contains("Unsupported command", StringComparison.OrdinalIgnoreCase))
                {
                    logCallback("当前 ColorVisionServiceHost 版本过旧，请先在“更新 -> ColorVision Service Host”中更新后台服务。");
                }

                return false;
            }
            catch (Exception ex)
            {
                logCallback($"ColorVisionServiceHost 不可用或执行失败: {ex.Message}");
                logCallback("请先在“更新 -> ColorVision Service Host”中安装/更新后台服务。");
                return false;
            }
        }

        public static string GetOperationLabel(ServiceHostServiceOperation operation)
        {
            return operation switch
            {
                ServiceHostServiceOperation.Start => "启动",
                ServiceHostServiceOperation.Stop => "停止",
                ServiceHostServiceOperation.Restart => "重启",
                ServiceHostServiceOperation.Terminate => "终止",
                _ => "操作",
            };
        }
    }
}
