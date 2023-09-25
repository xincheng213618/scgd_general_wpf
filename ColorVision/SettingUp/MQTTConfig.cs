using ColorVision.MQTT;
using ColorVision.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.SettingUp
{
    public class MQTTSetting : ViewModelBase
    {
        private static readonly object _locker = new();
        public MQTTSetting()
        {
            if (File.Exists(GlobalConst.MQTTMsgRecordsFileName))
            {
                try {
                    MsgRecords = JsonConvert.DeserializeObject<ObservableCollection<MsgRecord>>(File.ReadAllText(GlobalConst.MQTTMsgRecordsFileName)) ?? new ObservableCollection<MsgRecord>();
                }
                catch {
                    MsgRecords = new ObservableCollection<MsgRecord>();
                }
            }
            else
                MsgRecords = new ObservableCollection<MsgRecord>();


            var timer = new System.Timers.Timer
            {
                Interval = TimeSpan.FromSeconds(1).TotalMilliseconds,
                AutoReset = true,
            };
            timer.Elapsed +=(s,e)=>
            {
                lock (_locker)
                {
                    int itemsToRemoveCount = MsgRecords.Count - CacheLength;
                    if (itemsToRemoveCount > 0)
                        for (int i = 0; i < itemsToRemoveCount; i++)
                            if (MsgRecords.Count > 1)
                                MsgRecords.RemoveAt(MsgRecords.Count - 1);
                }
            };
            timer.Start();
            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };
                string jsonString = JsonConvert.SerializeObject(MsgRecords, settings);
                File.WriteAllText(GlobalConst.MQTTMsgRecordsFileName, jsonString);
            };
        }
        /// <summary>
        /// 是否显示心跳
        /// </summary>
        public bool IsShieldHeartbeat { get => _IsShieldHeartbeat; set { _IsShieldHeartbeat = value; NotifyPropertyChanged(); } }
        private bool _IsShieldHeartbeat;

        /// <summary>
        /// 只显示选中的
        /// </summary>
        public bool ShowSelect { get => _ShowSelect; set { _ShowSelect = value; NotifyPropertyChanged(); } }
        private bool _ShowSelect;

        public int AliveTimeout { get => _AliveTimeout; set { _AliveTimeout = value; NotifyPropertyChanged(); } }
        private int _AliveTimeout = 60;

        public int SendTimeout { get => _SendTimeout; set { _SendTimeout = value; NotifyPropertyChanged(); } }
        private int _SendTimeout = 10;

        public int CacheLength { get => _CacheLength; set { _CacheLength = value; NotifyPropertyChanged(); } }
        private int _CacheLength = 1000;

        [System.Text.Json.Serialization.JsonIgnore]
        public ObservableCollection<MsgRecord> MsgRecords { get; set; }
    }


    /// <summary>
    /// MQTT配置
    /// </summary>
    public class MQTTConfig : ViewModelBase
    {
        /// <summary>
        /// 连接名称
        /// </summary>
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;


        /// <summary>
        /// IP地址
        /// </summary>
        public string Host { get => _Host; set { _Host = value; NotifyPropertyChanged(); } }
        private string _Host = "127.0.0.1";

        /// <summary>
        /// 端口地址
        /// </summary>
        public int Port { get => _Port; set {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                NotifyPropertyChanged(); 
            } }
        private int _Port = 1883;

        /// <summary>
        /// 账号
        /// </summary>
        public string UserName { get=>_UserName; set { _UserName = value; NotifyPropertyChanged(); } }
        private string _UserName = string.Empty;

        /// <summary>
        /// 密码
        /// </summary>
        public string UserPwd { get => _UserPwd; set { _UserPwd = value; NotifyPropertyChanged(); } }
        private string _UserPwd = string.Empty;

    }
}
