using ColorVision.Engine.MQTT;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates.Flow
{
    public class IInitializerFlow : IInitializer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TemplateInitializer));

        public string Name => "Flow";

        public IEnumerable<string> Dependencies => Array.Empty<string>();

        public int Order => 10;

        public Task InitializeAsync()
        {
            log.Info("init flow");
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
          
            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            return Task.CompletedTask;
        }
    }
}
