using ColorVision.UI;
using ColorVision.UI.Configs;
using System.Collections.Generic;
using SystemMonitor.Properties;

namespace SystemMonitor
{
    public class SystemMonitorIStatusBarProvider : IStatusBarProvider
    {

        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            return new List<StatusBarMeta>
            {
                new StatusBarMeta()
                {
                    Id = "SystemMonitor_Time",
                    Name = "Time",
                    Description = Resources.PerformanceTest,
                    Order = 9999,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SystemMonitors.Time),
                    Source = SystemMonitors.GetInstance()
                },
                new StatusBarMeta()
                {
                    Id = "SystemMonitor_Uptime",
                    Name = "Uptime",
                    Description = "System Uptime",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Right,
                    Order = 2,
                    BindingName = nameof(SystemMonitors.GetUptime),
                    Source = SystemMonitors.GetInstance()
                },
                new StatusBarMeta()
                {
                    Id = "SystemMonitor_RAM",
                    Name = "RAM",
                    Description = Resources.PerformanceTest,
                    Order = 9999,
                    Type = StatusBarType.Text,
                    BindingName = nameof(SystemMonitors.MemoryThis),
                    Source = SystemMonitors.GetInstance()
                }
            };
        }

    }
}
