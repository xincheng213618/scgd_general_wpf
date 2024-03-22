using ColorVision.Common.MVVM;
using ColorVision.Services.Msg;
using ColorVision.Settings;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ColorVision.MQTT
{
    public class MQTTSetting : ViewModelBase
    {
        private static readonly object _locker = new();
        private  static string MQTTMsgRecordsFileName { get => ConfigHandler.GetInstance().MQTTMsgRecordsFileName; }

        public MQTTSetting()
        {
            if (File.Exists(MQTTMsgRecordsFileName))
            {
                try
                {
                    MsgRecords = JsonConvert.DeserializeObject<ObservableCollection<MsgRecord>>(File.ReadAllText(MQTTMsgRecordsFileName)) ?? new ObservableCollection<MsgRecord>();
                }
                catch
                {
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
            timer.Elapsed += (s, e) =>
            {
                lock (_locker)
                {
                    int itemsToRemoveCount = MsgRecords.Count - CacheLength;
                    if (itemsToRemoveCount > 0)
                        for (int i = 0; i < itemsToRemoveCount; i++)
                            if (MsgRecords.Count > 1)
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MsgRecords.RemoveAt(MsgRecords.Count - 1);
                                });
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
                File.WriteAllText(MQTTMsgRecordsFileName, jsonString);
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

        public int CacheLength { get => _CacheLength; set { _CacheLength = value; NotifyPropertyChanged(); } }
        private int _CacheLength = 1000;

        [System.Text.Json.Serialization.JsonIgnore]
        public ObservableCollection<MsgRecord> MsgRecords { get; set; }
    }
}
