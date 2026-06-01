using ColorVision.UI;

namespace EventVWR
{
    public class EventVWRPlugins : IPluginBase
    {
        public override string Header { get; set; } = Properties.Resources.EventVWR_PluginName;
        public override string Description { get; set; } = Properties.Resources.EventVWR_PluginDesc;
    }
}
