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
        private readonly Func<SystemMonitorSetting> _configFactory;
        private readonly Func<ISystemMonitorStatusSource> _monitorFactory;
        private readonly IConfigReloadNotifier? _configReloadNotifier;
        private SystemMonitorSetting? _config;
        private ISystemMonitorStatusSource? _monitor;

        public event EventHandler? StatusBarItemsChanged;

        public SystemMonitorIStatusBarProvider()
            : this(
                () => ConfigService.Instance.GetRequiredService<SystemMonitorSetting>(),
                SystemMonitors.GetInstance,
                ConfigService.Instance as IConfigReloadNotifier)
        {
        }

        private SystemMonitorIStatusBarProvider(
            Func<SystemMonitorSetting> configFactory,
            Func<ISystemMonitorStatusSource> monitorFactory,
            IConfigReloadNotifier? configReloadNotifier = null)
        {
            _configFactory = configFactory;
            _monitorFactory = monitorFactory;
            _configReloadNotifier = configReloadNotifier;
            BindCurrentConfig();
            if (_configReloadNotifier != null)
                _configReloadNotifier.ConfigsReloaded += ConfigReloadNotifier_ConfigsReloaded;
        }

        private SystemMonitorSetting BindCurrentConfig()
        {
            var config = _configFactory();
            if (ReferenceEquals(_config, config))
                return config;

            // Import and ReloadFromDisk replace ConfigHandler.Configs. Rebind both the
            // provider and an existing monitor so metadata and timer policy share C1.
            if (_config != null)
                _config.PropertyChanged -= Config_PropertyChanged;

            _config = config;
            _config.PropertyChanged += Config_PropertyChanged;
            UpdateMonitorConfiguration(config);
            return config;
        }

        private void ConfigReloadNotifier_ConfigsReloaded(object? sender, EventArgs e)
        {
            BindCurrentConfig();
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (IsStatusBarVisibilityProperty(e.PropertyName))
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateMonitorConfiguration(SystemMonitorSetting config)
        {
            if (_monitor is ISystemMonitorConfigurable configurableMonitor)
                configurableMonitor.UpdateConfiguration(config);
        }

        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            var config = BindCurrentConfig();
            if (!HasVisibleStatusItem(config))
                return Array.Empty<StatusBarMeta>();

            var monitor = _monitor ??= _monitorFactory();
            UpdateMonitorConfiguration(config);
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
                    Name = Resources.CPU,
                    Description = Resources.ShowCPU,
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
                    Name = Resources.RAM,
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
                double maxDiskUsage = monitor.GetMaximumDiskUsage();
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

        private static bool HasVisibleStatusItem(SystemMonitorSetting config)
        {
            return config.IsShowTime
                || config.IsShowRAM
                || config.IsShowCPU
                || config.IsShowUptime
                || config.IsShowDisk;
        }

        private static bool IsStatusBarVisibilityProperty(string? propertyName)
        {
            return propertyName == nameof(SystemMonitorSetting.IsShowTime)
                || propertyName == nameof(SystemMonitorSetting.IsShowRAM)
                || propertyName == nameof(SystemMonitorSetting.IsShowCPU)
                || propertyName == nameof(SystemMonitorSetting.IsShowUptime)
                || propertyName == nameof(SystemMonitorSetting.IsShowDisk);
        }
    }
}
