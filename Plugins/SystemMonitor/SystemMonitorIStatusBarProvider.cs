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
                    Name = "Time",
                    Description = Resources.PerformanceTest,
                    Order =12,
                    Type =StatusBarType.Text,
                    BindingName  = nameof(SystemMonitors.Time),
                    VisibilityBindingName ="Config.IsShowTime",
                    Source = SystemMonitors.GetInstance()
                },
                new StatusBarMeta()
                {
                    Type =StatusBarType.Text,
                    BindingName  = nameof(SystemMonitors.GetUptime),
                    VisibilityBindingName ="Config.IsShowTime",
                    Source = SystemMonitors.GetInstance()
                },
                new StatusBarMeta()
                {
                    Name = "RAM",
                    Description = Resources.PerformanceTest,
                    Order =10,
                    Type =StatusBarType.Text,
                    BindingName  = nameof(SystemMonitors.MemoryThis),
                    VisibilityBindingName ="Config.IsShowRAM",
                    Source = SystemMonitors.GetInstance()
                }
            };
        }

    }
}
