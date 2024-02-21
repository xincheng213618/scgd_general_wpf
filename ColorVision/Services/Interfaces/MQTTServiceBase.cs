using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.RC;
using ColorVision.Services.Msg;
using log4net;
using MQTTMessageLib;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace ColorVision.Services
{
    public class MQTTServiceBase : ViewModelBase, IHeartbeat, IServiceConfig, IDisposable
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(MQTTServiceBase));
        private static readonly log4net.ILog logger = LogManager.GetLogger(typeof(MQTTServiceBase));
        public MQTTSetting MQTTSetting { get; set; }
        public MQTTControl MQTTControl { get; set; }

        public event EventHandler Connected;
        public event EventHandler DisConnected;

        public MQTTServiceBase()
        {
            MQTTControl = MQTTControl.GetInstance();
            MQTTSetting = MQTTControl.Setting;
            MQTTControl.ApplicationMessageReceivedAsync += Processing;
            var timer = new Timer
            {
                Interval = TimeSpan.FromSeconds(1).TotalMilliseconds,
                AutoReset = true,
            };
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        private Task Processing(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                try
                {
                    MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;

                    if (json.EventName == "Heartbeat")
                    {
                        LastAliveTime = DateTime.Now;
                        if (!IsAlive)
                        {
                            Connected?.Invoke(this, new EventArgs());
                        }
                        IsAlive = true;
                        return Task.CompletedTask;
                    }
                    //Token unavailable
                    if (json.Code == -10)
                    {
                        MQTTRCService.GetInstance().QueryServices();
                        return Task.CompletedTask;
                    }

                    if (json.Code != 0 && json.Code != 1)
                    {
                        MsgReturnReceived?.Invoke(json);
                        return Task.CompletedTask;
                    }

                    bool msgee = false;
                    lock (_locker)
                    {
                        if (timers.TryGetValue(json.MsgID, out var value))
                        {
                            value.Enabled = false;
                            timers.Remove(json.MsgID);
                            msgee = true;
                            MsgReturnReceived?.Invoke(json);
                        }
                        MsgRecord foundMsgRecord = MsgRecords.FirstOrDefault(record => record.MsgID == json.MsgID);
                        if (foundMsgRecord != null)
                        {
                            foundMsgRecord.ReciveTime = DateTime.Now;
                            foundMsgRecord.MsgReturn = json;
                            foundMsgRecord.MsgRecordState = json.Code == 0 ? MsgRecordState.Success : MsgRecordState.Fail;
                            MsgRecords.Remove(foundMsgRecord);
                        }
                    }
                    ///这里是因为这里先加载相机上，所以加在这里
                    if (!msgee)
                        MsgReturnReceived?.Invoke(json);
                }
                catch (Exception ex)
                {
                    if (log.IsErrorEnabled)
                        log.Error(ex);
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            TimeSpan sp = DateTime.Now - LastAliveTime;
            if (sp > TimeSpan.FromMilliseconds(HeartbeatTime))
            {
                DisConnected?.Invoke(sender ,new EventArgs());
                IsAlive = false;
            }
            else
            {
                IsAlive = true;
            }
        }
        public MsgReturnHandler MsgReturnReceived { get; set; }
        public virtual string SubscribeTopic { get; set; }
        public virtual string SendTopic { get; set; }
        public virtual string DeviceCode { get; set; }
        public string SnID { get; set; }
        public string ServiceName { get; set; }
        public virtual string ServiceToken { get; set; }

        public virtual int HeartbeatTime { get => _HeartbeatTime; set { _HeartbeatTime = value; NotifyPropertyChanged(); } }
        private int _HeartbeatTime = 2000;

        public virtual DateTime LastAliveTime { get => _LastAliveTime; set { _LastAliveTime = value; NotifyPropertyChanged(); } }
        private DateTime _LastAliveTime = DateTime.MinValue;

        public virtual bool IsAlive { get => _IsAlive; set { _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;

        private  Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        private  readonly object _locker = new();

        private List<MsgRecord> MsgRecords = new List<MsgRecord>();

        /// <summary>
        /// 这里修改成可以继承的
        /// </summary>
        /// <param name="msg"></param>
        internal virtual MsgRecord PublishAsyncClient(MsgSend msg,double Timeout = 30000)
        {
            Guid guid = Guid.NewGuid();
            msg.MsgID = guid.ToString();
            msg.DeviceCode = DeviceCode;
            msg.Token = ServiceToken;
            ///这里是为了兼容只前的写法，后面会修改掉
            if (string.IsNullOrWhiteSpace(msg.ServiceName))
            {
                msg.ServiceName = SendTopic;
            }

            string json = JsonConvert.SerializeObject(msg, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, json, false));

            MsgRecord msgRecord = new MsgRecord { SendTopic = SendTopic, SubscribeTopic = SubscribeTopic, MsgID = guid.ToString(), SendTime = DateTime.Now, MsgSend = msg, MsgRecordState = MsgRecordState.Send };

            Application.Current.Dispatcher.Invoke(() =>
            {
                MQTTSetting.MsgRecords.Insert(0, msgRecord);
                MsgRecords.Add(msgRecord);
            });

            Timer timer = new Timer(Timeout);
            timer.Elapsed += (s, e) =>
            {
                timer.Enabled = false;
                lock (_locker) { timers.Remove(guid.ToString()); }
                msgRecord.MsgRecordState = MsgRecordState.Timeout;
                MsgRecords.Remove(msgRecord);
            };
            timer.AutoReset = false;
            timer.Enabled = true;
            timers.Add(guid.ToString(), timer);
            timer.Start();
            return msgRecord;
        }

        public virtual void Dispose()
        {
            MQTTControl.ApplicationMessageReceivedAsync -= Processing;
            GC.SuppressFinalize(this);
        }

        public virtual void UpdateStatus(MQTTNodeService nodeService)
        {
            ServiceToken = nodeService.ServiceToken;
        }
    }
}
