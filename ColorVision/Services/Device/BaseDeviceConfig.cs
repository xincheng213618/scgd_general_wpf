using ColorVision.MVVM;
using ColorVision.Services;
using Newtonsoft.Json;
using System;

namespace ColorVision.Device
{
    public class BaseConfig: ViewModelBase, IServiceConfig, IHeartbeat
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

        /// <summary>
        /// 心跳时间
        /// </summary>
        public int HeartbeatTime { get => _HeartbeatTime; set { _HeartbeatTime = value; NotifyPropertyChanged(); } }
        private int _HeartbeatTime = 2000;
        /// <summary>
        /// 是否存活
        /// </summary>
        public bool IsAlive { get => _IsAlive; set { _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;
        public DateTime LastAliveTime { get => _LastAliveTime; set { _LastAliveTime = value; NotifyPropertyChanged(); } }
        private DateTime _LastAliveTime = DateTime.MinValue;

        public void SetLiveTime(DateTime liveTime, int overTime, bool isLive)
        {
            this.LastAliveTime = liveTime;
            this.IsAlive = isLive;
            if (overTime > 0) this.HeartbeatTime = overTime;
        }
    }

    public enum DeviceServiceStatus
    {
        Offline,
        Online
    }

    /// <summary>
    /// 基础硬件配置信息
    /// </summary>
    public class BaseDeviceConfig : BaseConfig
    {
        /// <summary>
        /// 设备序号
        /// </summary>
        public string ID { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }
        private string _ID;

        public string MD5 { get => _MD5; set { _MD5 = value; NotifyPropertyChanged(); } }
        private string _MD5;



        public bool IsOnline { get => DeviceServiceStatus == DeviceServiceStatus.Online; }
        private DeviceServiceStatus _DeviceServiceStatus;
        public DeviceServiceStatus DeviceServiceStatus
        {
            get => _DeviceServiceStatus;
            set
            {
                _DeviceServiceStatus = value;
                NotifyPropertyChanged(nameof(IsOnline));
            }
        }

    }




    public delegate void HeartbeatHandler(HeartbeatParam heartbeat);

    public class HeartbeatParam
    {
        public DeviceStatus DeviceStatus { get; set; }

        [JsonProperty("time")]
        public string Time { get; set; }
    }

    public class DeviceHeartbeatParam
    {
        public string DeviceName { get; set; }
        public DeviceStatus DeviceStatus { get; set; }
    }
}
