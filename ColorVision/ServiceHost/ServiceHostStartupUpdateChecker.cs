using log4net;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ServiceHost
{
    internal static class ServiceHostStartupUpdateChecker
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceHostStartupUpdateChecker));
        private static bool _isChecking;

        public static async Task CheckAndPromptAsync(Window? owner)
        {
            if (_isChecking)
                return;

            _isChecking = true;
            try
            {
                ServiceHostStatus status = await ColorVisionServiceHostManager.QueryStatusAsync().ConfigureAwait(true);
                if (!status.IsPackageAvailable || status.PackageVersion == null)
                    return;

                if (status.NeedsInstall)
                {
                    await PromptInstallAsync(owner, status, "检测到 ColorVision 后台服务尚未安装，是否现在安装？\n\n首次安装需要管理员权限。").ConfigureAwait(true);
                    return;
                }

                if (!status.NeedsUpdate)
                    return;

                if (status.CanSelfUpdate)
                {
                    await PromptSelfUpdateAsync(owner, status).ConfigureAwait(true);
                    return;
                }

                await PromptInstallAsync(owner, status, "检测到 ColorVision 后台服务需要更新，但当前服务未运行，是否现在重新安装/更新？\n\n此操作需要管理员权限。").ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                log.Warn("Service host startup update check failed.", ex);
            }
            finally
            {
                _isChecking = false;
            }
        }

        private static async Task PromptSelfUpdateAsync(Window? owner, ServiceHostStatus status)
        {
            string message =
                $"检测到 ColorVision 后台服务有新版本，是否自动更新？\n\n" +
                $"当前运行版本: {FormatVersion(status.RunningVersion ?? status.InstalledVersion)}\n" +
                $"新版本: {FormatVersion(status.PackageVersion)}\n\n" +
                "服务已在运行，更新过程将由后台服务自己完成，通常不需要 UAC。";

            if (MessageBox.Show(owner, message, "ColorVision Service Host", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            ServiceHostOperationResult result = await ColorVisionServiceHostManager.SelfUpdateAsync().ConfigureAwait(true);
            if (result.Success)
            {
                MessageBox.Show(owner, "后台服务更新已开始，稍后会自动重启服务。", "ColorVision Service Host", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(owner, $"后台服务自更新失败：\n{result.Summary}", "ColorVision Service Host", MessageBoxButton.OK, MessageBoxImage.Warning);
                await PromptInstallAsync(owner, status, "当前后台服务可能版本过旧，无法自更新。是否改用管理员权限重新安装/更新？").ConfigureAwait(true);
            }
        }

        private static async Task PromptInstallAsync(Window? owner, ServiceHostStatus status, string message)
        {
            string fullMessage =
                $"{message}\n\n" +
                $"已安装版本: {FormatVersion(status.InstalledVersion)}\n" +
                $"包内版本: {FormatVersion(status.PackageVersion)}";

            if (MessageBox.Show(owner, fullMessage, "ColorVision Service Host", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            ServiceHostOperationResult result = await ColorVisionServiceHostManager.InstallAsync().ConfigureAwait(true);
            if (result.Success)
            {
                MessageBox.Show(owner, "ColorVision 后台服务已安装/更新。", "ColorVision Service Host", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(owner, $"ColorVision 后台服务安装/更新失败：\n{result.Summary}", "ColorVision Service Host", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string FormatVersion(Version? version)
        {
            return version?.ToString() ?? "未知";
        }
    }
}
