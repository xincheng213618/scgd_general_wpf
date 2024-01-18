using ColorVision.MQTT;
using ColorVision.Services.Device.SMU.Configs;
using ColorVision.Services.Msg;
using MQTTMessageLib.SMU;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Services.Device.SMU
{
    public class SMUService : DeviceServiceBase<ConfigSMU>
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
                    if (json == null)
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
                            Configs.SMUResultData data = JsonConvert.DeserializeObject<Configs.SMUResultData>(JsonConvert.SerializeObject(json.Data));
                            Application.Current.Dispatcher.Invoke(() => ResultEvent?.Invoke(data));
                        }
                        else if (json.EventName == "Scan")
                        {
                            Configs.SMUScanResultData data = JsonConvert.DeserializeObject<Configs.SMUScanResultData>(JsonConvert.SerializeObject(json.Data));
                            Application.Current.Dispatcher.Invoke(() => ScanResultEvent?.Invoke(data));
                        }
                        else if (json.EventName == "Close")
                        {
                        }
                        else if (json.EventName == "Uninit")
                        {
                        }
                        else if (json.EventName == "Heartbeat" && json.ServiceName.Equals(this.ServiceName, StringComparison.Ordinal))
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

        public MsgRecord Open(bool isNet, string devName)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTSMUEventEnum.Event_Open,
                ServiceName = Config.Code,
                Params = new SMUOpenParam() { DevName = devName, IsNet = isNet, }
            };
            return PublishAsyncClient(msg);
        }

        public bool GetData(bool isSourceV, double measureVal, double lmtVal)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTSMUEventEnum.Event_GetData,
                ServiceName = Config.Code,
                Params = new SMUGetDataParam() { IsSourceV = isSourceV, MeasureValue = measureVal, LimitValue = lmtVal }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public MsgRecord Close()
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTSMUEventEnum.Event_Close,
                ServiceName = Config.Code,
            };
            return PublishAsyncClient(msg);
        }

        public bool Scan(bool isSourceV, double startMeasureVal, double stopMeasureVal, double lmtVal, int number)
        {
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var Params = new Dictionary<string, object>();
            Params.Add("DeviceParam", new SMUScanParam() { IsSourceV = isSourceV, BeginValue = startMeasureVal, EndValue = stopMeasureVal, LimitValue = lmtVal, Points = number });
            MsgSend msg = new MsgSend
            {
                EventName = MQTTSMUEventEnum.Event_Scan,
                ServiceName = Config.Code,
                SerialNumber = sn,
                Params = Params,
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool CloseOutput()
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTSMUEventEnum.Event_CloseOutput,
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
            return true;
        }
    }
}
