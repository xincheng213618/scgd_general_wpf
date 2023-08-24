using ColorVision.MVVM;
using ColorVision.SettingUp;
using Google.Protobuf.WellKnownTypes;
using HandyControl.Expression.Shapes;
using HslCommunication.MQTT;
using log4net;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.MQTT
{

    /// <summary>
    /// MQTT服务接口
    /// </summary>
    public interface IMQTTServiceConfig
    {
        /// <summary>
        /// 发送信道
        /// </summary>
        public string SendTopic { get; set; }

        /// <summary>
        /// 监听信道
        /// </summary>
        public string SubscribeTopic { get; set; }
    }

    /// <summary>
    /// 心跳接口
    /// </summary>
    public interface IHeartbeat
    {
        public DateTime LastAliveTime { get; set; }

        public bool IsAlive { get; set; }
    }

    public class BaseService<T> : BaseService where T :BaseDeviceConfig
    {
        public T Config { get; set; }

        public BaseService(T config)
        {
            Config = config;
            ServiceName = Config.Name;
        }
    }


    public class BaseService:ViewModelBase, IHeartbeat, IMQTTServiceConfig, IDisposable 
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(BaseService));

        public MQTTConfig MQTTConfig { get; set; }
        public MQTTSetting MQTTSetting { get; set; }


        public event EventHandler Connected;

        public Dictionary<string, MsgSend> cmdMap { get; set; }

        public BaseService()
        {
            cmdMap = new Dictionary<string, MsgSend>();
            MQTTControl = MQTTControl.GetInstance();
            MQTTConfig = MQTTControl.MQTTConfig;
            MQTTSetting = MQTTControl.MQTTSetting;
            MQTTControl.ApplicationMessageReceivedAsync += Processing;
            var timer = new System.Timers.Timer
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

                    lock (_locker)
                    {


                        if (timers.TryGetValue(json.MsgID, out var value))
                        {
                            value.Enabled = false;
                            timers.Remove(json.MsgID);
                        }
                        MsgRecord foundMsgRecord = MsgRecords.FirstOrDefault(record => record.MsgID == json.MsgID);
                        if (foundMsgRecord != null)
                        {
                            foundMsgRecord.ReciveTime = DateTime.Now;
                            foundMsgRecord.MsgReturn = json;
                            foundMsgRecord.MsgRecordState = json.Code == 0 ? MsgRecordState.Success : MsgRecordState.Fail;
                            MsgRecords.Remove(foundMsgRecord);
                            MsgReceived?.Invoke(foundMsgRecord.MsgSend, json);
                        }
                    }
                    ///这里是因为这里先加载相机上，所以加在这里
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
        public MsgReturnHandler MsgReturnReceived { get; set; }
        public MsgHandler MsgReceived { get; set; }

        


        public virtual string SubscribeTopic { get; set; }
        public virtual string SendTopic { get; set; }
        public MQTTControl MQTTControl { get; set; }
        public ulong ServiceID { get; set; }
        public string SnID { get; set; }
        public string ServiceName { get; set; }

        public virtual DateTime LastAliveTime { get => _LastAliveTime; set { _LastAliveTime = value; NotifyPropertyChanged(); } } 
        private DateTime _LastAliveTime = DateTime.MinValue;

        public virtual bool IsAlive { get => _IsAlive; set { if (value == _IsAlive) return;  _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;


        private static Dictionary<string, Timer> timers = new Dictionary<string, Timer>();

        private static readonly object _locker = new();

        private List<MsgRecord> MsgRecords = new List<MsgRecord>();

        /// <summary>
        /// 这里修改成可以继承的
        /// </summary>
        /// <param name="msg"></param>
        internal virtual void PublishAsyncClient(MsgSend msg)
        {
            Guid guid = Guid.NewGuid(); 
            msg.MsgID = guid;
            msg.ServiceID = ServiceID;
            msg.SnID = SnID;
            ///这里是为了兼容只前的写法，后面会修改掉
            if (string.IsNullOrWhiteSpace(msg.ServiceName))
            {
                msg.ServiceName = SendTopic;
            }
            string json = JsonConvert.SerializeObject(msg, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, json, false));

            if (msg.EventName == "CM_GetAllSnID")
                return;

            MsgRecord msgRecord = new MsgRecord {SendTopic=SendTopic,SubscribeTopic =SubscribeTopic ,MsgID = guid.ToString(), SendTime = DateTime.Now, MsgSend = msg,MsgRecordState = MsgRecordState.Send};
            
            MQTTSetting.MsgRecords.Insert(0,msgRecord);

            MsgRecords.Add(msgRecord);

            Timer timer = new Timer(MQTTSetting.SendTimeout*1000);
            timer.Elapsed += (s, e) =>
            {
                timer.Enabled = false;
                lock (_locker) { timers.Remove(guid.ToString()); }
                msgRecord.MsgRecordState = MsgRecordState.Timeout;
                MsgRecords.Remove(msgRecord);
            };
            timer.AutoReset = false;
            timer.Enabled = true;
            timer.Start();
            timers.Add(guid.ToString(), timer);

        }

        public void Dispose()
        {
            MQTTControl.ApplicationMessageReceivedAsync -= Processing;
            GC.SuppressFinalize(this);
        }
    }

    public class MsgRecord:ViewModelBase, IMQTTServiceConfig
    {
        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        public string MsgID { get; set; }
        public DateTime SendTime { get => _SendTime; set { _SendTime = value; NotifyPropertyChanged(); } }
        private DateTime _SendTime;
        public DateTime ReciveTime { get => _ReciveTime; set { _ReciveTime = value; NotifyPropertyChanged(); } }
        private DateTime _ReciveTime;
        public MsgSend MsgSend { get; set; }
        public MsgReturn MsgReturn { get; set; }

        public MsgRecordState MsgRecordState { get => _MsgRecordState; set 
            {
                _MsgRecordState = value;
                NotifyPropertyChanged();
                if (value == MsgRecordState.Success ||  value == MsgRecordState.Fail)
                {
                    NotifyPropertyChanged(nameof(IsRecive));
                    NotifyPropertyChanged(nameof(MsgReturn));
                }
                else if (value == MsgRecordState.Timeout)
                {
                    NotifyPropertyChanged(nameof(IsTimeout));
                }
                else
                {
                    NotifyPropertyChanged(nameof(MsgReturn));
                }

                NotifyPropertyChanged(nameof(IsSend));

            }
        }
        private MsgRecordState _MsgRecordState { get; set; }

        [JsonIgnore]
        public bool IsSend { get => MsgRecordState == MsgRecordState.Send; }
        [JsonIgnore]
        public bool IsRecive { get => MsgRecordState == MsgRecordState.Success || MsgRecordState == MsgRecordState.Fail; }
        [JsonIgnore]
        public bool IsTimeout { get => MsgRecordState == MsgRecordState.Timeout; }

    }
    public enum MsgRecordState
    {
        Send,
        Success,
        Fail,
        Timeout
    }



}
