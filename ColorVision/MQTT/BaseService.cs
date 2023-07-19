using ColorVision.MVVM;
using ColorVision.SettingUp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.MQTT
{
    public class BaseService:ViewModelBase
    {
        public BaseService()
        {
            var timer = new System.Timers.Timer
            {
                Interval = TimeSpan.FromSeconds(30).TotalMilliseconds,
                AutoReset = true,
            };
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }
        public static MQTTSetting MQTTSetting { get => GlobalSetting.GetInstance().SoftwareConfig.MQTTSetting; }

        private void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (DateTime.Now - LastAliveTime > TimeSpan.FromSeconds(MQTTSetting.AliveTimeout))
            {
                IsAlive = false;
            }
            else
            {
                IsAlive = true;
            }
        }

        

        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }
        public MQTTControl MQTTControl { get; set; }
        public ulong ServiceID { get; set; }

        internal List<Guid> RunTimeUUID = new List<Guid> { Guid.NewGuid() };

        public DateTime LastAliveTime { get => _LastAliveTime; set { _LastAliveTime = value; NotifyPropertyChanged(); } } 
        private DateTime _LastAliveTime = DateTime.MinValue;

        public bool IsAlive { get => _IsAlive; set { if (value == _IsAlive) return;  _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;

        internal void PublishAsyncClient(MsgSend msg)
        {
            Guid guid = Guid.NewGuid();
            RunTimeUUID.Add(guid);

            msg.ServiceName = SendTopic;
            msg.MsgID = guid;
            msg.ServiceID = ServiceID;

            string json = JsonConvert.SerializeObject(msg, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, json, false));
        }
    }
}
