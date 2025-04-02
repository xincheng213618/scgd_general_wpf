using ColorVision.UI;

namespace SystemMonitor
{
    public class SystemMonitorPlugins : IPluginBase
    {
        public override string Header { get; set; } = "性能监控";
        public override string Description { get; set; } = "增强的电脑性能监控插件";
    }
}
