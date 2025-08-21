using ColorVision.Engine.MQTT;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates.Flow
{
    public class IInitializerFlow : IInitializer
    {
        public IInitializerFlow(IMessageUpdater messageUpdater)
        {

        }

        public string Name => "Flow";

        public IEnumerable<string> Dependencies => Array.Empty<string>();

        public int Order => 10;

        public Task InitializeAsync()
        {
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            MQTTControl.GetInstance().SubscribeCache("RC_local/Camera/SVR.Camera.Default/CMD");
            return Task.CompletedTask;
        }
    }
}
