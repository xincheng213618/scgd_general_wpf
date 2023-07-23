using ColorVision.MVVM;
using ColorVision.SettingUp;
using log4net;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.MQTT
{
    public class BaseService:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(BaseService));

        public BaseService()
        {
            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.ApplicationMessageReceivedAsync +=  (arg) =>
            {

                if (arg.ApplicationMessage.Topic == SubscribeTopic)
                {
                    string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                    try
                    {
                        MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                        if (json == null)
                            return Task.CompletedTask;
                        MsgReturnChanged?.Invoke(json);
                    }
                    catch(Exception ex)
                    {
                        log.Warn(ex);
                        return Task.CompletedTask;
                    }
                }
                return Task.CompletedTask;
            };

            var timer = new System.Timers.Timer
            {
                Interval = TimeSpan.FromSeconds(1).TotalMilliseconds,
                AutoReset = true,
            };
            timer.Elapsed += Timer_Elapsed;
            timer.Start();

            MsgReturnChanged += (e) =>
            {
                if (e.EventName == "Heartbeat")
                {
                    LastAliveTime = DateTime.Now;
                    IsAlive = true;
                }
            };
        }
        public string NickName { get => _NickName; set { _NickName = value; NotifyPropertyChanged(); } }
        private string _NickName = string.Empty;


        public static MQTTSetting MQTTSetting { get => GlobalSetting.GetInstance().SoftwareConfig.MQTTSetting; }
        public static int AliveTimeout { get => MQTTSetting.AliveTimeout; }

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
        public MsgReturnHandler MsgReturnChanged { get; set; }



        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }
        public MQTTControl MQTTControl { get; set; }
        public ulong ServiceID { get; set; }
        public string CameraID { get; set; }

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
            msg.CameraID = CameraID;
            string json = JsonConvert.SerializeObject(msg, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, json, false));
        }
    }
}
