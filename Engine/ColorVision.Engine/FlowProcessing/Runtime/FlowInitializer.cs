using ColorVision.Engine.MQTT;
using ColorVision.UI;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing;

public sealed class FlowInitializer : IInitializer
{
    public string Name => "Flow";
    // ConfigHandler is initialized before the IInitializer pipeline. Configure
    // FlowEngineLib before TemplateInitializer can materialize MQTT start nodes.
    public int Order => 0;

    public Task InitializeAsync()
    {
        MQTTConfig config = MQTTSetting.Instance.MQTTConfig;
        FlowEngineLib.MQTTHelper.SetDefaultCfg(config.Host, config.Port, config.UserName, config.UserPwd, false, null);
        return Task.CompletedTask;
    }
}
