using ColorVision.Engine.MQTT;
using ColorVision.UI;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing;

public sealed class FlowInitializer : IInitializer
{
    public string Name => "Flow";
    public int Order => 10;

    public Task InitializeAsync()
    {
        MQTTConfig config = MQTTSetting.Instance.MQTTConfig;
        FlowEngineLib.MQTTHelper.SetDefaultCfg(config.Host, config.Port, config.UserName, config.UserPwd, false, null);
        return Task.CompletedTask;
    }
}
