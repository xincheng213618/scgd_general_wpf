using ColorVision.UI;

namespace EventVWR
{
    public class ScreenRecorderPlugins : IPluginBase
    {
        public override string Header { get; set; } = "录像插件";
        public override string UpdateUrl { get; set; }
        public override string Description { get; set; } = "可以录屏";
    }
}
