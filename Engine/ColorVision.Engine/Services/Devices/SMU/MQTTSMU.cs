using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using MQTTMessageLib.SMU;
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.SMU
{
    public class MQTTSMU : MQTTDeviceService<ConfigSMU>
    {
        public DeviceSMU DeviceSMU { get; set; }
        public MQTTSMU(DeviceSMU deviceSMU, ConfigSMU sMUConfig) : base(sMUConfig)
        {
            DeviceSMU = deviceSMU;
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
                    MsgReturn msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (msg == null)
                        return Task.CompletedTask;

                    if (msg.EventName == "GetData")
                    {
                        if (msg != null && msg.Data != null && msg.Data.MasterId != null && msg.Data.MasterId > 0)
                        {
                            int masterId = msg.Data.MasterId;
                            SMUResultModel model = MySqlControl.GetInstance().DB.Queryable<SMUResultModel>().Where(x => x.Id == masterId).First();
                            if (model != null)
                            {
                                ViewResultSMU viewResultSpectrum = new ViewResultSMU(model);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    DeviceSMU.View.AddViewResultSMU(viewResultSpectrum);
                                    Config.I = model.IResult;
                                    Config.V = model.VResult;
                                });
                            }
                        }


                    }
                    else if (msg.EventName == "Scan")
                    {
                        Configs.SMUScanResultData data = JsonConvert.DeserializeObject<Configs.SMUScanResultData>(JsonConvert.SerializeObject(msg.Data));
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            DeviceSMU.View.AddViewResultSMU(new ViewResultSMU(Config.IsSourceV ? MeasurementType.Voltage : MeasurementType.Current, (float)Config.StopMeasureVal, data.VList, data.IList));
                        });
                    }
                }
                catch(Exception ex)
                {
                    log.Error(ex);
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }


        public bool SetParam()
        {
            MsgSend msg = new()
            {
                EventName = "SetParam",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public MsgRecord Open(bool isNet, string devName)
        {
            MsgSend msg = new()
            {
                EventName = MQTTSMUEventEnum.Event_Open,
                Params = new SMUOpenParam() { DevName = devName, IsNet = isNet, }
            };
            return PublishAsyncClient(msg);
        }

        public bool GetData(bool isSourceV, double measureVal, double lmtVal)
        {
            MsgSend msg = new()
            {
                EventName = MQTTSMUEventEnum.Event_GetData,
                Params = new SMUGetDataParam() { IsSourceV = isSourceV, MeasureValue = measureVal, LimitValue = lmtVal }
            };
            MsgRecord msgRecord = PublishAsyncClient(msg);
            return true;
        }


        public MsgRecord Close()
        {
            MsgSend msg = new()
            {
                EventName = MQTTSMUEventEnum.Event_Close,
            };
            return PublishAsyncClient(msg);
        }

        public bool Scan(bool isSourceV, double startMeasureVal, double stopMeasureVal, double lmtVal, int number)
        {
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var Params = new Dictionary<string, object>();
            Params.Add("DeviceParam", new SMUScanParam() { IsSourceV = isSourceV, BeginValue = startMeasureVal, EndValue = stopMeasureVal, LimitValue = lmtVal, Points = number });
            MsgSend msg = new()
            {
                EventName = MQTTSMUEventEnum.Event_Scan,
                SerialNumber = sn,
                Params = Params,
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool CloseOutput()
        {
            MsgSend msg = new()
            {
                EventName = MQTTSMUEventEnum.Event_CloseOutput,
            };
            PublishAsyncClient(msg);
            return true;
        }
    }
}
