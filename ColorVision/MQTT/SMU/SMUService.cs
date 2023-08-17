using ColorVision.MQTT.Spectrum;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.MQTT.SMU
{
    public delegate void MQTTSMUHeartbeatHandler(HeartbeatParam heartbeat);
    public delegate void MQTTSMUScanResultHandler(SMUScanResultData data);
    public delegate void MQTTSMUResultHandler(SMUResultData data);

    public class SMUService : BaseService<SMUConfig>
    {
        public event MQTTSMUHeartbeatHandler HeartbeatHandlerEvent;
        public event MQTTSMUScanResultHandler ScanResultHandlerEvent;
        public event MQTTSMUResultHandler ResultHandlerEvent;
        public SMUService(SMUConfig sMUConfig) : base(sMUConfig)
        {
            this.Config = sMUConfig;

            this.SendTopic = sMUConfig.SendTopic;
            this.SubscribeTopic = sMUConfig.SubscribeTopic;

            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                try
                {
                    MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    if (json.Code == 0)
                    {
                        if (json.EventName == "Init")
                        {
                            ServiceID = json.ServiceID;
                        }
                        else if (json.EventName == "SetParam")
                        {
                            //MessageBox.Show("SetParam");
                        }
                        else if (json.EventName == "Open")
                        {
                            //MessageBox.Show("Open");
                        }
                        else if (json.EventName == "GetData")
                        {
                            SMUResultData data = JsonConvert.DeserializeObject<SMUResultData>(JsonConvert.SerializeObject(json.Data));
                            Application.Current.Dispatcher.Invoke(() => ResultHandlerEvent?.Invoke(data));
                        }
                        else if (json.EventName == "Scan")
                        {
                            SMUScanResultData data = JsonConvert.DeserializeObject<SMUScanResultData>(JsonConvert.SerializeObject(json.Data));
                            Application.Current.Dispatcher.Invoke(() => ScanResultHandlerEvent?.Invoke(data));
                        }
                        else if (json.EventName == "Close")
                        {
                            //MessageBox.Show("Close");
                        }
                        else if (json.EventName == "Uninit")
                        {
                            //MessageBox.Show("Uninit");
                        }
                        else if (json.EventName == "Heartbeat")
                        {
                            HeartbeatParam heartbeat = JsonConvert.DeserializeObject<HeartbeatParam>(JsonConvert.SerializeObject(json.Data));
                            Application.Current.Dispatcher.Invoke(() => HeartbeatHandlerEvent?.Invoke(heartbeat));
                        }
                    }
                }
                catch
                {
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }


        public bool SetParam()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam"
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Open(bool isNet, string devName)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = new SMUOpenParam() { DevName = devName, IsNet = isNet, }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool GetData(bool isSourceV, double measureVal, double lmtVal)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetData",
                Params = new SMUGetDataParam() { IsSourceV = isSourceV, MeasureVal = measureVal, LimitVal = lmtVal }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Close()
        {
            //if (ServiceID == 0)
            //{
            //    MessageBox.Show("请先初始化");
            //    return false;
            //}
            MsgSend msg = new MsgSend
            {
                EventName = "Close"
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Scan(bool isSourceV, double startMeasureVal, double stopMeasureVal, double lmtVal, int number)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Scan",
                Params = new SMUScanParam() { IsSourceV = isSourceV, StartMeasureVal = startMeasureVal, StopMeasureVal = stopMeasureVal, LimitVal = lmtVal, Number = number }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool CloseOutput()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "CloseOutput"
            };
            PublishAsyncClient(msg);
            return true;
        }
    }
}
