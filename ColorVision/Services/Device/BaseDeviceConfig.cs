using ColorVision.MVVM;
using Newtonsoft.Json;
using System;

namespace ColorVision.Services.Device
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

        public DeviceStatus DeviceStatus { get => _DeviceStatus; set { _DeviceStatus = value; NotifyPropertyChanged(nameof(BtnDeviceStatus)); } }
        private DeviceStatus _DeviceStatus = DeviceStatus.Closed;

        [JsonIgnore]
        public string BtnDeviceStatus
        {
            get
            {
                string text = _DeviceStatus.ToString();
                switch (_DeviceStatus)
                {
                    case DeviceStatus.Unknown:
                        break;
                    case DeviceStatus.Closed:
                        text = "打开";
                        break;
                    case DeviceStatus.Closing:
                        break;
                    case DeviceStatus.Opened:
                        text = "关闭";
                        break;
                    case DeviceStatus.Opening:
                        break;
                    case DeviceStatus.Busy:
                        break;
                    case DeviceStatus.Free:
                        break;
                    case DeviceStatus.UnInit:
                        break;
                    case DeviceStatus.Init:
                        break;
                    case DeviceStatus.UnConnected:
                        break;
                    default:
                        break;
                }
                return text;
            }
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
        public string ID { get => _SNID; set {  _SNID = value; NotifyPropertyChanged(); } }
        private string _SNID;

        public string SNID { get => ID; }

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
