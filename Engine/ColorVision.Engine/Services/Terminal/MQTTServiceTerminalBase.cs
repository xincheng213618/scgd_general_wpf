using ColorVision.Engine.MQTT;

namespace ColorVision.Engine.Services.Terminal
{
    public class MQTTServiceTerminalBase : MQTTServiceBase
    {

    }
    public class MQTTServiceTerminalBase<T> : MQTTServiceTerminalBase where T : TerminalServiceConfig
    {
        public T Config { get; set; }

        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; } }
        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; } }

        public override int HeartbeatTime { get => Config.HeartbeatTime; set { Config.HeartbeatTime = value; OnPropertyChanged(); } }

        public override string ServiceToken { get => Config.ServiceToken; set => Config.ServiceToken = value; }


        public MQTTServiceTerminalBase(T Config) : base()
        {
            this.Config = Config;
            MQTTControl.SubscribeCache(Config.SubscribeTopic);
        }
    }



}
