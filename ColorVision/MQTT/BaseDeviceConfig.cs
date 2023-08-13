using ColorVision.MVVM;
using System;
using System.ComponentModel;

namespace ColorVision.MQTT
{
    /// <summary>
    /// 基础硬件配置信息
    /// </summary>
    public class BaseDeviceConfig : ViewModelBase, IMQTTServiceConfig, IHeartbeat
    {
        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

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
}
