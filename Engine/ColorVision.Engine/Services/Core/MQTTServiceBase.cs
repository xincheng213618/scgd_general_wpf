using ColorVision.Common.MVVM;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.RC;
using CVCommCore;
using log4net;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;

namespace ColorVision.Engine.Services.Core
{
    public class MQTTServiceBase : ViewModelBase, IHeartbeat, IServiceConfig, IDisposable
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(MQTTServiceBase));
        public MQTTControl MQTTControl { get; set; }

        public virtual DeviceStatusType DeviceStatus { get; set; }
        public event EventHandler Connected;
        public event EventHandler DisConnected;

        private Timer timer;

        public MQTTServiceBase()
        {
            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.ApplicationMessageReceivedAsync += Processing;
             timer = new Timer
            {
                Interval = TimeSpan.FromMilliseconds(30).TotalMilliseconds,
                AutoReset = true,
            };
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        public void SubscribeCache() => MQTTControl.SubscribeCache(SubscribeTopic);


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
                        MqttRCService.GetInstance().QueryServices();
                        return Task.CompletedTask;
                    }
                    if (json.Code == 102)
                    {
                        return Task.CompletedTask;
                    }

                    if (json.Code != 0 && json.Code != 1 && json.Code != -1&& json.Code != -401)
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
                            try
                            {
                                MsgReturnReceived?.Invoke(json);
                            }
                            catch(Exception ex)
                            {
                                MsgRecord foundMsgRecord1 = MsgRecords.FirstOrDefault(record => record.MsgID == json.MsgID);
                                if (foundMsgRecord1 != null)
                                {
                                    foundMsgRecord1.ReciveTime = DateTime.Now;
                                    foundMsgRecord1.MsgReturn = json;
                                    foundMsgRecord1.ErrorMsg = ex.Message;
                                    foundMsgRecord1.MsgRecordState = json.Code == 0 ? MsgRecordState.Success : MsgRecordState.Fail;
                                    MsgRecords.Remove(foundMsgRecord1);
                                }
                            }
                        }
                        Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            MsgRecord foundMsgRecord = MsgRecords.FirstOrDefault(record => record.MsgID == json.MsgID);
                            if (foundMsgRecord != null)
                            {
                                foundMsgRecord.ReciveTime = DateTime.Now;
                                foundMsgRecord.MsgReturn = json;
                                foundMsgRecord.MsgRecordState = json.Code == 0 ? MsgRecordState.Success : MsgRecordState.Fail;
                                MsgRecords.Remove(foundMsgRecord);
                            }
                        });
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

            //这里其实有问题,但是返回信号并不标准，只能按照这种写法
            long overTime = 2* HeartbeatTime;
            if (sp > TimeSpan.FromMilliseconds(overTime))
            {
                DisConnected?.Invoke(sender, new EventArgs());
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
        public string ServiceName { get; set; }
        public virtual string ServiceToken { get; set; }

        public virtual int HeartbeatTime { get => _HeartbeatTime; set { _HeartbeatTime = value; NotifyPropertyChanged(); } }
        private int _HeartbeatTime = 2000;

        public virtual DateTime LastAliveTime { get; set; }

        public virtual bool IsAlive { get; set; }

        private  Dictionary<string, Timer> timers = new();

        private  readonly object _locker = new();

        private List<MsgRecord> MsgRecords = new();

        /// <summary>
        /// 这里修改成可以继承的
        /// </summary>
        /// <param name="msg"></param>
        internal virtual MsgRecord PublishAsyncClient(MsgSend msg,double Timeout = 30000)
        {
            if (Timeout == 30000)
            {
                Timeout =MQTTSetting.Instance.DefaultTimeout;
            }
            Guid guid = Guid.NewGuid();
            //这里修改成 如果在上一级已经赋值就不在这里强制修改
            msg.MsgID ??= guid.ToString();
            msg.DeviceCode ??= DeviceCode;
            msg.Token ??= ServiceToken;
            msg.ServiceName ??= SendTopic;

            string json = JsonConvert.SerializeObject(msg, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            Task.Run(() => MQTTControl.PublishAsyncClient(SendTopic, json, false));

            MsgRecord msgRecord = new() { SendTopic = SendTopic, SubscribeTopic = SubscribeTopic, MsgID = msg.MsgID, SendTime = DateTime.Now, MsgSend = msg, MsgRecordState = MsgRecordState.Sended };

            Application.Current.Dispatcher.Invoke(() =>
            {
                MsgConfig.Instance.MsgRecords.Insert(0, msgRecord);
                MsgRecords.Add(msgRecord);
            });

            Timer timer = new(Timeout);
            timer.Elapsed += (s, e) =>
            {
                timer.Enabled = false;
                lock (_locker) { timers.Remove(msg.MsgID); }
                msgRecord.MsgRecordState = MsgRecordState.Timeout;
                MsgRecords.Remove(msgRecord);
            };
            timer.AutoReset = false;
            timer.Enabled = true;
            timers.Add(msg.MsgID, timer);
            timer.Start();
            return msgRecord;
        }

        public virtual void Dispose()
        {
            MQTTControl.ApplicationMessageReceivedAsync -= Processing;
            timer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
