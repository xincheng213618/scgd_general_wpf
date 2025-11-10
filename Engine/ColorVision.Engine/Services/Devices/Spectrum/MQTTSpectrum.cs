using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using MQTTMessageLib;
using MQTTMessageLib.Spectrum;
using MQTTnet.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    public class GetDataParam
    {
        [JsonProperty("IntegralTime")]
        public float IntTime { get; set; }
        [JsonProperty("NumberOfAverage")]
        public int AveNum { get; set; }
        [JsonProperty("AutoIntegration")]
        public bool BUseAutoIntTime { get; set; }
        [JsonProperty("SelfAdaptionInitDark")]
        public bool BUseAutoDark { get; set; }
        [JsonProperty("AutoInitDark")]
        public bool BUseAutoShutterDark { get; set; }

        public bool IsWithND { get; set; }
    }

    public class MQTTSpectrum : MQTTDeviceService<ConfigSpectrum>
    {
        public DeviceSpectrum Device { get; set; }

        public MQTTSpectrum(DeviceSpectrum DeviceSpectrum) : base(DeviceSpectrum.Config)
        {
            this.Device = DeviceSpectrum;
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
        }


        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                log.Info(Msg);
                try
                {
                    MsgReturn msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (msg == null)
                        return Task.CompletedTask;
                    if (msg.Code == 0 || msg.Code == 102)
                    {
                        if (msg.EventName == "SetParam")
                        {
                        }
                        else if (msg.EventName == "Open")
                        {
                        }
                        else if (msg.EventName == "GetData")
                        {
                            if (msg !=null && msg.Data != null && msg?.Data?.MasterId != null && msg?.Data?.MasterId > 0)
                            {
                                int masterId = msg.Data?.MasterId;
                                var DB = new SqlSugarClient(new ConnectionConfig
                                {
                                    ConnectionString = MySqlControl.GetConnectionString(),
                                    DbType = SqlSugar.DbType.MySql,
                                    IsAutoCloseConnection = true
                                });
                                SpectumResultModel model = DB.Queryable<SpectumResultModel>().Where(x => x.Id == masterId).First();
                                DB.Dispose();
                                if (model != null)
                                {
                                    ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(model);
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        Device.View.AddViewResultSpectrum(viewResultSpectrum);
                                    });
                                }
                            }
                        }
                        else if (msg.EventName == "GetDataAuto")
                        {
                            JObject data = msg.Data;
                            SpectrumData? colorParam = JsonConvert.DeserializeObject<SpectrumData>(JsonConvert.SerializeObject(data));
                            ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(colorParam.Data);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Device.View.AddViewResultSpectrum(viewResultSpectrum);
                            });

                        }
                        else if (msg.EventName == "Close")
                        {
                        }
                        else if (msg.EventName == "GetParam")
                        {
                            AutoIntTimeParam param = JsonConvert.DeserializeObject<AutoIntTimeParam>(JsonConvert.SerializeObject(msg.Data));
                            if (param != null)
                            {
                                Application.Current.Dispatcher.BeginInvoke(() =>
                                {
                                    Device.Config.BeginIntegralTime = param.fTimeB;
                                    Device.Config.MaxIntegralTime = param.iLimitTime;
                                });
                            }
                            else
                            {
                                log.Info("GetParam is null");
                            }
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
        public MsgRecord GetAllSnID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });
        public MsgRecord GetCameraID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetSnID" });

        public void GetParam()
        {
            MsgSend msg = new()
            {
                EventName = "GetParam",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
        }

        public bool SetParam(int iLimitTime, float fTimeB)
        {
            MsgSend msg = new()
            {
                EventName = "SetParam",
                ServiceName = Config.Code,
                Params = new Dictionary<string, object>() { { "IntegralTime", iLimitTime }, { "NumberOfAverage", fTimeB } }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public MsgRecord Open()
        {
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new()
            {
                EventName = "Open",
                ServiceName = Config.Code,
                Params = Params
            };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = -1, Name = string.Empty });
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetData(float IntTime, int AveNum, bool bUseAutoIntTime = false, bool bUseAutoDark = false, bool bUseAutoShutterDark = false)
        {
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            MsgSend msg = new()
            {
                EventName = "GetData",
                SerialNumber = sn,
                ServiceName = Config.Code,
                Params = new GetDataParam()
                {
                    IntTime = IntTime,
                    AveNum = AveNum,
                    BUseAutoIntTime = bUseAutoIntTime,
                    BUseAutoDark = bUseAutoDark,
                    BUseAutoShutterDark = bUseAutoShutterDark,
                    IsWithND =Config.IsWithND
                }
            };
            MsgRecord msgRecord= PublishAsyncClient(msg);
            return msgRecord;
        }
        public bool Close()
        {
            MsgSend msg = new()
            {
                EventName = "Close",
            };

            PublishAsyncClient(msg);
            return true;
        }
        public MsgRecord SelfAdaptionInitDark()
        {
            MsgSend msg = new()
            {
                EventName = "InitAutoDark",
                Params = Config.SelfAdaptionInitDark
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord SetPort()
        {
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new()
            {
                EventName = "SetPort",
                Params = Params
            };
            Params.Add("PortNum", Device.DisplaySpectrumConfig.PortNum);
            return PublishAsyncClient(msg);
        }
        public MsgRecord GetPort()
        {
            MsgSend msg = new()
            {
                EventName = "GetPort",
            };
            return PublishAsyncClient(msg);
        }


        public MsgRecord InitDark(float IntTime, int AveNum)
        {
            MsgSend msg = new()
            {
                EventName = "InitDark",
                ServiceName = Config.Code,
                Params = new Dictionary<string, object>() { { "IntegralTime", IntTime }, { "NumberOfAverage", AveNum } }

            };
            return PublishAsyncClient(msg);
        }

        public void GetDataAuto(float IntTime, int AveNum, bool bUseAutoIntTime = false, bool bUseAutoDark = false, bool bUseAutoShutterDark = false)
        {
            MsgSend msg = new()
            {
                EventName = "GetDataAuto",
                ServiceName = Config.Code,
                Params = new GetDataParam()
                {
                    IntTime = IntTime,
                    AveNum = AveNum,
                    BUseAutoIntTime = bUseAutoIntTime,
                    BUseAutoDark = bUseAutoDark,
                    BUseAutoShutterDark = bUseAutoShutterDark,
                    IsWithND = Config.IsWithND
                }
            };
            PublishAsyncClient(msg);
        }

        public void GetDataAutoStop()
        {
            MsgSend msg = new()
            {
                EventName = "GetDataAutoStop",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
        }


        public void ShutterConnect()
        {
            MsgSend msg = new()
            {
                EventName = MQTTSpectrumEventEnum.Event_Shutter_Connect,
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
        }

        public void ShutterDisconnect()
        {
            MsgSend msg = new()
            {
                EventName = MQTTSpectrumEventEnum.Event_Shutter_Disconnect,
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
        }

        public void ShutterDoopen()
        {
            MsgSend msg = new()
            {
                EventName = MQTTSpectrumEventEnum.Event_Shutter_Doopen,
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
        }

        public void ShutterDoclose()
        {
            MsgSend msg = new()
            {
                EventName = MQTTSpectrumEventEnum.Event_Shutter_Doclose,
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
        }
    }
}
