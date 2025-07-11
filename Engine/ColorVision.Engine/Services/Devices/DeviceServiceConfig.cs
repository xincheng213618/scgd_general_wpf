﻿using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Core;
using CVCommCore;
using Newtonsoft.Json;
using System;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices
{
    public class BaseConfig: ViewModelBase, IServiceConfig, IHeartbeat
    {
        [Browsable(false)]
        public string SubscribeTopic { get => _SubscribeTopic; set { _SubscribeTopic = value; NotifyPropertyChanged(); } }
        private string _SubscribeTopic;

        [Browsable(false)]
        public string SendTopic { get => _SendTopic; set { _SendTopic = value; NotifyPropertyChanged(); } }
        private string _SendTopic;

        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        [Browsable(false)]
        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code;

        //Token
        [JsonIgnore, Browsable(false)]
        public string ServiceToken { get => _ServiceToken; set { _ServiceToken = value; NotifyPropertyChanged(); } }
        private string _ServiceToken;

        /// <summary>
        /// 心跳时间
        /// </summary>
        [Browsable(false)]
        public int HeartbeatTime { get => _HeartbeatTime; set { _HeartbeatTime = value; NotifyPropertyChanged(); } }
        private int _HeartbeatTime = 5000;

        /// <summary>
        /// 是否存活
        /// </summary>
        [JsonIgnore, Browsable(false)]
        public bool IsAlive { get => _IsAlive; set { _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;
        [JsonIgnore, Browsable(false)]
        public DateTime LastAliveTime { get => _LastAliveTime; set { _LastAliveTime = value; NotifyPropertyChanged(); } }
        private DateTime _LastAliveTime = DateTime.MinValue;

        [JsonIgnore, Browsable(false)]
        public DeviceStatusType DeviceStatus { get => _DeviceStatus; set { if (_DeviceStatus == value) return; _DeviceStatus = value; DeviceStatusChanged?.Invoke(value); NotifyPropertyChanged(); } }
        private DeviceStatusType _DeviceStatus = DeviceStatusType.Closed;

        public event DeviceStatusChangedHandler DeviceStatusChanged;

    }
    public delegate void DeviceStatusChangedHandler(DeviceStatusType deviceStatus);

    /// <summary>
    /// 基础硬件配置信息
    /// </summary>
    public class DeviceServiceConfig : BaseConfig
    {
        /// <summary>
        /// 设备序号
        /// </summary>
        [Browsable(false)]
        public string Id { get => _Id; set { _Id = value; NotifyPropertyChanged(); } }
        private string _Id;

        /// <summary>
        /// 许可
        /// </summary>
        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;
    }
}
