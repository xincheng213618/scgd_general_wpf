using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Engine.Services.PhyCameras.Licenses
{
    /// <summary>
    /// 许可证检查初始化器 - 在应用程序启动时检查所有物理相机的许可证状态
    /// </summary>
    public class LicenseCheckInitializer : MainWindowInitializedBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LicenseCheckInitializer));

        public LicenseCheckInitializer()
        {
            // Set order to run after database initialization but before other initializers
            Order = 100;
        }

        public override async Task Initialize()
        {
            try
            {
                log.Info("开始检查相机许可证状态...");

                // Wait a bit for PhyCameraManager to fully initialize
                await Task.Delay(500);

                // Get configuration
                var config = ConfigService.Instance.GetRequiredService<LicenseNotificationConfig>();

                // Get all physical cameras
                var phyCameras = PhyCameraManager.GetInstance().PhyCameras;

                if (phyCameras == null || phyCameras.Count == 0)
                {
                    log.Info("没有找到物理相机，跳过许可证检查");
                    return;
                }

                // Check for expired or expiring licenses
                var expiredCameras = new List<LicenseExpiryInfo>();
                var expiringCameras = new List<LicenseExpiryInfo>();

                foreach (var camera in phyCameras)
                {
                    if (camera.CameraLicenseModel == null || camera.CameraLicenseModel.ExpiryDate == null)
                    {
                        // No license or invalid license
                        continue;
                    }

                    var expiryDate = camera.CameraLicenseModel.ExpiryDate.Value;
                    var daysRemaining = (expiryDate - DateTime.Now).Days;

                    if (expiryDate < DateTime.Now)
                    {
                        // Licenses has expired
                        var info = new LicenseExpiryInfo
                        {
                            Camera = camera,
                            CameraName = camera.Name ?? camera.Code ?? "未知相机",
                            StatusMessage = $"许可证已过期",
                            LicenseInfo = $"过期日期: {expiryDate:yyyy-MM-dd}",
                            StatusColor = new SolidColorBrush(Colors.Red),
                            StatusTextColor = new SolidColorBrush(Colors.Red),
                            IsExpired = true,
                            DaysRemaining = daysRemaining
                        };
                        expiredCameras.Add(info);
                    }
                    else if (daysRemaining <= config.WarningDaysBeforeExpiry)
                    {
                        // Licenses is expiring soon
                        var info = new LicenseExpiryInfo
                        {
                            Camera = camera,
                            CameraName = camera.Name ?? camera.Code ?? "未知相机",
                            StatusMessage = $"许可证即将过期 (剩余 {daysRemaining} 天)",
                            LicenseInfo = $"过期日期: {expiryDate:yyyy-MM-dd}",
                            StatusColor = new SolidColorBrush(Colors.Orange),
                            StatusTextColor = new SolidColorBrush(Colors.DarkOrange),
                            IsExpired = false,
                            DaysRemaining = daysRemaining
                        };
                        expiringCameras.Add(info);
                    }
                }

                // Check if we need to show notification
                bool shouldShowExpired = expiredCameras.Count > 0 && !config.DontShowExpiredAgain;
                bool shouldShowExpiring = expiringCameras.Count > 0 && !config.DontShowExpiringAgain;

                if (!shouldShowExpired && !shouldShowExpiring)
                {
                    if (expiredCameras.Count > 0 || expiringCameras.Count > 0)
                    {
                        log.Info($"检测到 {expiredCameras.Count} 个过期许可证和 {expiringCameras.Count} 个即将过期许可证，但用户已选择不再显示提示");
                    }
                    return;
                }

                // Combine cameras to show (expired first, then expiring)
                var camerasToShow = new List<LicenseExpiryInfo>();
                if (shouldShowExpired)
                {
                    camerasToShow.AddRange(expiredCameras);
                }
                if (shouldShowExpiring)
                {
                    camerasToShow.AddRange(expiringCameras);
                }

                if (camerasToShow.Count == 0)
                {
                    return;
                }

                log.Info($"检测到 {expiredCameras.Count} 个过期许可证和 {expiringCameras.Count} 个即将过期许可证，显示通知窗口");

                // Show notification window on UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        var notificationWindow = new LicenseExpiryNotificationWindow(camerasToShow)
                        {
                            Owner = Application.Current.MainWindow,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        };

                        var result = notificationWindow.ShowDialog();

                        // Save "don't show again" preferences
                        if (notificationWindow.DontShowAgain)
                        {
                            if (shouldShowExpired && expiredCameras.Count > 0)
                            {
                                config.DontShowExpiredAgain = true;
                            }
                            if (shouldShowExpiring && expiringCameras.Count > 0)
                            {
                                config.DontShowExpiringAgain = true;
                            }
                            ConfigService.Instance.SaveConfigs();
                            log.Info("用户选择不再显示许可证过期提示");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error("显示许可证通知窗口时出错", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                log.Error("许可证检查过程中出错", ex);
            }
        }
    }
}
