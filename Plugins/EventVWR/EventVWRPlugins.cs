using ColorVision.UI;

namespace EventVWR
{
    public class EventVWRPlugins : IPluginBase
    {
        public override string Header { get; set; } = "事件插件";
        public override string Description { get; set; } = "增强的异常管理,提供事件插件和Dump设置";
    }
}
