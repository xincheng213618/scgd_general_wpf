using ColorVision.MVVM;
using System;

namespace ColorVision.MQTT.Config
{
    public class ServiceConfig : ViewModelBase, IMQTTServiceConfig, IHeartbeat
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 服务类型
        /// </summary>
        public string Type { get; set; }

        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        public bool IsAlive { get => _IsAlive; set { if (value == _IsAlive) return; _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;
        public DateTime LastAliveTime { get => _LastAliveTime; set { _LastAliveTime = value; NotifyPropertyChanged(); } }
        private DateTime _LastAliveTime = DateTime.MinValue;
    }
}
