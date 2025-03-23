using ColorVision.UI;

namespace ScreenRecorder
{
    public class ScreenRecorderPlugins : IPluginBase
    {
        public override string Header { get; set; } = "录像插件";
        public override string Description { get; set; } = "可以录屏";
    }
}
