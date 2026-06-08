using ColorVision.Common.MVVM;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using log4net;
using MQTTnet;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace ColorVision.Engine.Services
{
    public class MQTTServiceBase : ViewModelBase, IDisposable
    {
        internal static readonly ILog log = LogManager.GetLogger(typeof(MQTTServiceBase));
        public MQTTControl MQTTControl { get; set; }

        public virtual DeviceStatusType DeviceStatus { get; set; }


        public MQTTServiceBase()
        {
            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.ApplicationMessageReceivedAsync += Processing;
        }

        public void SubscribeCache() => MQTTControl.SubscribeCache(SubscribeTopic);


        private Task Processing(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
                try
                {
                    MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;

                    lock (_locker)
                    {
                        if (_msgTimers.TryGetValue(json.MsgID, out var value))
                        {
                            value.Enabled = false;
                            _msgTimers.Remove(json.MsgID);
                        }
                        MsgRecord foundMsgRecord = _msgRecords.FirstOrDefault(record => record.MsgID == json.MsgID);
                        if (foundMsgRecord != null)
                        {
                            foundMsgRecord.ReciveTime = DateTime.Now;
                            foundMsgRecord.MsgReturn = json;
                            foundMsgRecord.MsgRecordState = json.Code == 0 ? MsgRecordState.Success : MsgRecordState.Fail;
                            _msgRecords.Remove(foundMsgRecord);
                        }
                    }
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

        public MsgReturnHandler MsgReturnReceived { get; set; }
        public virtual string SubscribeTopic { get; set; }
        public virtual string SendTopic { get; set; }
        public virtual string DeviceCode { get; set; }
        public string ServiceName { get; set; }
        public virtual string ServiceToken { get; set; }

        private  Dictionary<string, Timer> _msgTimers = new();

        private  readonly object _locker = new();

        private List<MsgRecord> _msgRecords = new();

        /// <summary>
        /// 这里修改成可以继承的
        /// </summary>
        /// <param name="msg"></param>
        internal virtual MsgRecord PublishAsyncClient(MsgSend msg,double timeout = 30000)
        {
            Guid guid = Guid.NewGuid();
            msg.MsgID ??= guid.ToString();
            msg.DeviceCode ??= DeviceCode;
            msg.Token ??= ServiceToken;
            msg.ServiceName ??= SendTopic;

            string json = JsonConvert.SerializeObject(msg, Formatting.None);

            _ = MQTTControl.PublishAsyncClient(SendTopic, json, false);


            MsgRecord msgRecord = new() { SendTopic = SendTopic, SubscribeTopic = SubscribeTopic, MsgID = msg.MsgID, SendTime = DateTime.Now, MsgSend = msg, MsgRecordState = MsgRecordState.Sended };
            _msgRecords.Add(msgRecord);

            Task.Run(() =>
            {
                MsgRecordDataBaseHelper.Insert(msgRecord);
            });

            var timer = new Timer(timeout)
            {
                AutoReset = false,
                Enabled = true,
            };
            timer.Elapsed += (s, e) =>
            {
                timer.Enabled = false;
                timer.Dispose();
                lock (_locker)
                {
                    _msgTimers.Remove(msg.MsgID);
                    msgRecord.MsgRecordState = MsgRecordState.Timeout;
                    _msgRecords.Remove(msgRecord);
                }
            };
            lock (_locker)
            {
                _msgTimers.Add(msg.MsgID, timer);
            }
            timer.Start();
            return msgRecord;
        }

        public virtual void Dispose()
        {
            MQTTControl.ApplicationMessageReceivedAsync -= Processing;
            lock (_locker)
            {
                foreach (var timer in _msgTimers.Values)
                    timer.Dispose();
                _msgTimers.Clear();
                _msgRecords.Clear();
            }

            GC.SuppressFinalize(this);
        }
    }
}
