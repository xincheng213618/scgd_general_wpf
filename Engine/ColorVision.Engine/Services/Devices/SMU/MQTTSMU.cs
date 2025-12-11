using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.SMU.Views;
using MQTTMessageLib.SMU;
using MQTTnet.Client;
using Newtonsoft.Json;
using SqlSugar;
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
                //Msg = "{\"Version\":\"1.0\",\"EventName\":\"Scan\",\"ServiceName\":\"RC_local/SMU/SVR.SMU.Default/CMD\",\"DeviceName\":null,\"DeviceCode\":\"DEV.SMU.Default\",\"SerialNumber\":\"20251027T150635.2185024\",\"Code\":0,\"MsgID\":\"4504c606-531e-47ab-96d2-4b1bdb8d8ef5\",\"Data\":{\"VList\":[1.520087,2.581165,2.620323,2.648037,2.672302,2.693535,2.713528,2.73214,2.749928,2.767574],\"IList\":[0.0,0.011115,0.02222721,0.03333874,0.04445305,0.0555611,0.06666915,0.07778762,0.08889776,0.1000051],\"ScanList\":[0.0,0.011111111111111112,0.022222222222222223,0.033333333333333326,0.044444444444444446,0.05555555555555556,0.06666666666666665,0.07777777777777777,0.08888888888888889,0.1]}}";
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
                            var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, });
                            SMUResultModel model = DB.Queryable<SMUResultModel>().Where(x => x.Id == masterId).First();
                            DB.Dispose();
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
                            ViewResultSMU viewResultSMU = new ViewResultSMU(Config.IsSourceV ? MeasurementType.Voltage : MeasurementType.Current, (float)Config.StopMeasureVal, data.VList, data.IList);
                            viewResultSMU.CreateTime = DateTime.Now;
                            viewResultSMU.Id = -1;
                            DeviceSMU.View.AddViewResultSMU(viewResultSMU);
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

        public class SMUGetDataParam
        {
            public bool IsSourceV { set; get; }
            public double MeasureValue { set; get; }

            public SMUChannelType Channel { get; set; }

            public double LimitValue { set; get; }
        }

        public bool GetData(bool isSourceV, double measureVal, double lmtVal, SMUChannelType channel)
        {
            MsgSend msg = new()
            {
                EventName = MQTTSMUEventEnum.Event_GetData,
                Params = new SMUGetDataParam() { IsSourceV = isSourceV, MeasureValue = measureVal, LimitValue = lmtVal,Channel =channel }
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

        public class SMUScanParam
        {
            public bool IsSourceV { set; get; }
            public double BeginValue { set; get; }
            public double EndValue { set; get; }
            public double LimitValue { set; get; }
            public int Points { set; get; }

            public SMUChannelType Channel { get; set; }

        }
        public MsgRecord Scan(bool isSourceV, double startMeasureVal, double stopMeasureVal, double lmtVal, int number, SMUChannelType channel)
        {
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var Params = new Dictionary<string, object>();
            Params.Add("DeviceParam", new SMUScanParam() { IsSourceV = isSourceV, BeginValue = startMeasureVal, EndValue = stopMeasureVal, LimitValue = lmtVal, Points = number, Channel = channel });
            MsgSend msg = new()
            {
                EventName = MQTTSMUEventEnum.Event_Scan,
                SerialNumber = sn,
                Params = Params,
            };
            return PublishAsyncClient(msg);
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
