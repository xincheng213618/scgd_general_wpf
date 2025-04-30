using ColorVision.UI;

namespace WindowsServicePlugin.PluginConfig
{
    public class PluginWindowService : IPluginBase
    {
        public override string Header { get; set; } = "视彩服务插件";
        public override string Description { get; set; } = "增强的服务管理工具，提供服务日志、服务更新，一起一些其他和服务相关的功能";
    }
}
