﻿using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Core;
using System;
using System.Threading.Tasks;

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

        public override int HeartbeatTime { get => Config.HeartbeatTime; set { Config.HeartbeatTime = value; NotifyPropertyChanged(); } }

        public override bool IsAlive { get => Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); } }

        public override DateTime LastAliveTime { get => Config.LastAliveTime; set => Config.LastAliveTime = value; }

        public override string ServiceToken { get => Config.ServiceToken; set => Config.ServiceToken = value; }



        public void UpdateServiceConfig(IServiceConfig config)
        {
            Task.Run(() => MQTTControl.UnsubscribeAsyncClientAsync(Config.SubscribeTopic));

            Config.SendTopic = config.SendTopic;
            Config.SubscribeTopic = config.SubscribeTopic;
            MQTTControl.SubscribeCache(Config.SubscribeTopic);
        }

        public void UpdateServiceConfig(string SendTopic,string SubscribeTopic)
        {
            Task.Run(() => MQTTControl.UnsubscribeAsyncClientAsync(Config.SubscribeTopic));

            Config.SendTopic = SendTopic;
            Config.SubscribeTopic = SubscribeTopic;
            MQTTControl.SubscribeCache(Config.SubscribeTopic);
        }

        public void UpdateServiceConfig(T config)
        {
            Task.Run(() => MQTTControl.UnsubscribeAsyncClientAsync(Config.SubscribeTopic));

            Config.SendTopic = config.SendTopic;
            Config.SubscribeTopic = config.SubscribeTopic;
            MQTTControl.SubscribeCache(Config.SubscribeTopic);
        }


        public MQTTServiceTerminalBase(T Config) : base()
        {
            this.Config = Config;
            MQTTControl.SubscribeCache(Config.SubscribeTopic);
        }
    }



}
