using ColorVision.MVVM;
using MQTTMessageLib;
using Newtonsoft.Json;
using System;

namespace ColorVision.Services.Devices
{
    public class BaseConfig: ViewModelBase, IServiceConfig, IHeartbeat
    {
        public string SubscribeTopic { get => _SubscribeTopic; set { _SubscribeTopic = value; NotifyPropertyChanged(); } }
        private string _SubscribeTopic;

        public string SendTopic { get => _SendTopic; set { _SendTopic = value; NotifyPropertyChanged(); } }
        private string _SendTopic;

        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code;

        //Token
        [JsonIgnore]
        public string ServiceToken { get => _ServiceToken; set { _ServiceToken = value; NotifyPropertyChanged(); } }
        private string _ServiceToken;

        /// <summary>
        /// 心跳时间
        /// </summary>
        public int HeartbeatTime { get => _HeartbeatTime; set { _HeartbeatTime = value; NotifyPropertyChanged(); } }
        private int _HeartbeatTime = 10000;

        /// <summary>
        /// 是否存活
        /// </summary>
        [JsonIgnore]
        public bool IsAlive { get => _IsAlive; set { _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;

        public DateTime LastAliveTime { get => _LastAliveTime; set { _LastAliveTime = value; NotifyPropertyChanged(); } }
        private DateTime _LastAliveTime = DateTime.MinValue;

        [JsonIgnore]
        public DeviceStatusType DeviceStatus { get => _DeviceStatus; set { if (_DeviceStatus == value) return; _DeviceStatus = value; DeviceStatusChanged?.Invoke(value); NotifyPropertyChanged(); } }
        private DeviceStatusType _DeviceStatus = DeviceStatusType.Closed;

        public event DeviceStatusChangedHandler DeviceStatusChanged;

    }

    public enum ServiceStatus
    {
        Offline,
        Online
    }

    /// <summary>
    /// 基础硬件配置信息
    /// </summary>
    public class DeviceServiceConfig : BaseConfig
    {
        /// <summary>
        /// 设备序号
        /// </summary>
        public string Id { get => _Id; set {  _Id = value; NotifyPropertyChanged(); } }
        private string _Id;
    }



    public delegate void HeartbeatHandler(HeartbeatParam heartbeat);

    public class HeartbeatParam
    {
        public DeviceStatusType DeviceStatus { get; set; }

        [JsonProperty("time")]
        public string Time { get; set; }
    }

    public class DeviceHeartbeatParam
    {
        public string DeviceName { get; set; }
        public DeviceStatusType DeviceStatus { get; set; }
    }
}
