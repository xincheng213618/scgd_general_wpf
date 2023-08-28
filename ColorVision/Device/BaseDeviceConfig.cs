using ColorVision.MQTT;
using ColorVision.MVVM;
using Newtonsoft.Json;
using System;

namespace ColorVision.Device
{
    /// <summary>
    /// 基础硬件配置信息
    /// </summary>
    public class BaseDeviceConfig : ViewModelBase, IServiceConfig, IHeartbeat
    {
        public string SubscribeTopic { get => _SubscribeTopic; set { _SubscribeTopic = value; NotifyPropertyChanged(); } }
        private string _SubscribeTopic;

        public string SendTopic { get => _SendTopic; set { _SendTopic = value; NotifyPropertyChanged(); } }
        private string _SendTopic;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code;

        public int HeartbeatTime { get => _HeartbeatTime; set { _HeartbeatTime = value; NotifyPropertyChanged(); } }
        private int _HeartbeatTime;
        /// <summary>
        /// 是否存活
        /// </summary>
        public bool IsAlive { get => _IsAlive; set { _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;
        public DateTime LastAliveTime { get => _LastAliveTime; set { _LastAliveTime = value; NotifyPropertyChanged(); } }
        private DateTime _LastAliveTime = DateTime.MinValue;

        /// <summary>
        /// 设备序号
        /// </summary>
        public string ID { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }
        private string _ID;

        public string MD5 { get => _MD5; set { _MD5 = value; NotifyPropertyChanged(); } }
        private string _MD5;
    }

    public delegate void HeartbeatEventHandler(HeartbeatParam heartbeat);

    public class HeartbeatParam
    {
        public DeviceStatus DeviceStatus { get; set; }

        [JsonProperty("time")]
        public string Time { get; set; }
    }
}
