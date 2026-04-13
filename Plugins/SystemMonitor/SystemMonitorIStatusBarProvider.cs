using ColorVision.UI;
using ColorVision.UI.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using SystemMonitor.Properties;

namespace SystemMonitor
{
    public class SystemMonitorIStatusBarProvider : IStatusBarProviderUpdatable
    {
        public event EventHandler? StatusBarItemsChanged;

        public SystemMonitorIStatusBarProvider()
        {
            // 仅在可见性配置变更时刷新状态栏项，数据更新由绑定自动处理
            var monitor = SystemMonitors.GetInstance();
            monitor.Config.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SystemMonitorSetting.IsShowTime)
                 || e.PropertyName == nameof(SystemMonitorSetting.IsShowRAM)
                 || e.PropertyName == nameof(SystemMonitorSetting.IsShowCPU)
                 || e.PropertyName == nameof(SystemMonitorSetting.IsShowUptime)
                 || e.PropertyName == nameof(SystemMonitorSetting.IsShowDisk))
                {
                    StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
                }
            };
        }

        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            var monitor = SystemMonitors.GetInstance();
            var config = monitor.Config;
            var items = new List<StatusBarMeta>();

            // 时间
            if (config.IsShowTime)
            {
                items.Add(new StatusBarMeta
                {
                    Id = "SystemMonitor_Time",
                    Name = Resources.ShowTime,
                    Description = Resources.PerformanceTest,
                    Order = 9999,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SystemMonitors.Time),
                    Source = monitor,
                    IsVisible = config.IsShowTime,
                });
            }
            if (config.IsShowUptime)
            {
                items.Add(new StatusBarMeta
                {
                    Id = "SystemMonitor_Uptime",
                    Name = Resources.Uptime,
                    Description = Resources.Uptime,
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Right,
                    Order = 2,
                    BindingName = nameof(SystemMonitors.GetUptime),
                    Source = monitor,
                    IsVisible = config.IsShowUptime,
                });
            }
            if (config.IsShowCPU)
            {
                // CPU 使用率 (更醒目的显示)
                items.Add(new StatusBarMeta
                {
                    Id = "SystemMonitor_CPU",
                    Name = "CPU",
                    Description = "CPU Usage",
                    Order = 9998,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SystemMonitors.CPUStatusText),
                    Source = monitor,
                    IsVisible = config.IsShowCPU,
                });
            }


            if (config.IsShowRAM)
            {
                // RAM 内存 (更醒目的显示)
                items.Add(new StatusBarMeta
                {
                    Id = "SystemMonitor_RAM",
                    Name = "RAM",
                    Description = Resources.ShowRAM,
                    Order = 9997,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SystemMonitors.RAMStatusText),
                    Source = monitor,
                    IsVisible = config.IsShowRAM,
                });
            }







            if (config.IsShowDisk)
            {
                // 磁盘健康图标 - 根据最大使用率选择图标颜色
                double maxDiskUsage = monitor.Drives.Any() ? monitor.Drives.Max(d => d.UsagePercent) : 0;
                string diskIcon = maxDiskUsage > 90 ? "DrawingImageHardDiskFull"
                                : maxDiskUsage > 70 ? "DrawingImageHardDiskRed"
                                : "DrawingImageHardDisk";

                items.Add(new StatusBarMeta
                {
                    Id = "SystemMonitor_Disk",
                    Name = Resources.StorageManagement,
                    Description = Resources.StorageManagement,
                    Order = 9996,
                    Type = StatusBarType.Icon,
                    IconResourceKey = diskIcon,
                    BindingName = nameof(SystemMonitors.TotalDiskFree),
                    Source = monitor,
                    IsVisible = config.IsShowDisk,
                });

            }
            return items;
        }
    }
}
