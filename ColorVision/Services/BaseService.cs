#pragma warning disable CS8602  

using ColorVision.MQTT;
using System;
using System.Threading.Tasks;

namespace ColorVision.Services
{
    public class BaseService<T> : BaseService where T : BaseServiceConfig
    {
        public T Config { get; set; }

        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; } }
        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; } }

        public override int HeartbeatTime { get => Config.HeartbeatTime; set { Config.HeartbeatTime = value; NotifyPropertyChanged(); } }

        public override bool IsAlive { get => Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); } }

        public override DateTime LastAliveTime { get => Config.LastAliveTime; set => Config.LastAliveTime = value; }


        public void UpdateServiceConfig(IServiceConfig config)
        {
            Task.Run(() => MQTTControl.UnsubscribeAsyncClientAsync(Config.SubscribeTopic));

            Config.SendTopic = config.SendTopic;
            Config.SubscribeTopic = config.SubscribeTopic;
            MQTTControl.SubscribeCache(Config.SubscribeTopic);
        }

        public BaseService(T Config) : base()
        {
            this.Config = Config;
            MQTTControl.SubscribeCache(Config.SubscribeTopic);
        }
    }



}
