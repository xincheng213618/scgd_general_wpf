using ColorVision.MQTT;
using ColorVision.Services;
using ColorVision.Services.Device;
using ColorVision.Services.Msg;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Device.SMU
{
    public delegate void MQTTSMUScanResultHandler(SMUScanResultData data);
    public delegate void MQTTSMUResultHandler(SMUResultData data);

    public class SMUService : BaseDevService<ConfigSMU>
    {
        public event MQTTSMUScanResultHandler ScanResultEvent;
        public event MQTTSMUResultHandler ResultEvent;
        public SMUService(ConfigSMU sMUConfig) : base(sMUConfig)
        {
            Config = sMUConfig;

            SendTopic = sMUConfig.SendTopic;
            SubscribeTopic = sMUConfig.SubscribeTopic;

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
                    if (json == null || !json.ServiceName.Equals(Config.Code, StringComparison.Ordinal))
                        return Task.CompletedTask;

                    if (json.Code == 0)
                    {
                        if (json.EventName == "Init")
                        {
                        }
                        else if (json.EventName == "SetParam")
                        {
                        }
                        else if (json.EventName == "Open")
                        {
                        }
                        else if (json.EventName == "GetData")
                        {
                            SMUResultData data = JsonConvert.DeserializeObject<SMUResultData>(JsonConvert.SerializeObject(json.Data));
                            Application.Current.Dispatcher.Invoke(() => ResultEvent?.Invoke(data));
                        }
                        else if (json.EventName == "Scan")
                        {
                            SMUScanResultData data = JsonConvert.DeserializeObject<SMUScanResultData>(JsonConvert.SerializeObject(json.Data));
                            Application.Current.Dispatcher.Invoke(() => ScanResultEvent?.Invoke(data));
                        }
                        else if (json.EventName == "Close")
                        {
                        }
                        else if (json.EventName == "Uninit")
                        {
                        }
                        else if (json.EventName == "Heartbeat" && json.ServiceName.Equals(this.ServiceName, System.StringComparison.Ordinal))
                        {
                            List<DeviceHeartbeatParam> devs_heartbeat = JsonConvert.DeserializeObject<List<DeviceHeartbeatParam>>(JsonConvert.SerializeObject(json.Data));
                            if (devs_heartbeat != null && devs_heartbeat.Count > 0) DoHeartbeat(devs_heartbeat);
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
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Open(bool isNet, string devName)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                ServiceName = Config.Code,
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
                ServiceName = Config.Code,
                Params = new SMUGetDataParam() { IsSourceV = isSourceV, MeasureValue = measureVal, LimitValue = lmtVal }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Close()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Close",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Scan(bool isSourceV, double startMeasureVal, double stopMeasureVal, double lmtVal, int number)
        {
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            MsgSend msg = new MsgSend
            {
                EventName = "Scan",
                ServiceName = Config.Code,
                SerialNumber = sn,
                Params = new SMUScanParam() { IsSourceV = isSourceV, StartMeasureVal = startMeasureVal, StopMeasureVal = stopMeasureVal, LimitVal = lmtVal, Number = number }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool CloseOutput()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "CloseOutput",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
            return true;
        }
    }
}
