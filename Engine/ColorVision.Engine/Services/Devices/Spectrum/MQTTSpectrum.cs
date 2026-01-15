using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates.Flow;
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
        public bool SelfAdaptionInitDark { get; set; }

        public bool AutoInitDark { get; set; }

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
                log.Debug(Msg);
                try
                {
                    MsgReturn msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);

                    if (Config.Code != null && msg.DeviceCode != Config.Code) return Task.CompletedTask;

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
                        else if (msg.EventName == "GetData" || msg.EventName == "EQE.GetData")
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
                                SpectumResultEntity model = DB.Queryable<SpectumResultEntity>().Where(x => x.Id == masterId).First();
                                DB.Dispose();
                                log.Info($"GetData MasterId:{masterId} ");
                                if (model != null)
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        try
                                        {
                                            ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(model);
                                            Device.View.AddViewResultSpectrum(viewResultSpectrum);
                                            double? IntegralTime = msg?.Data?.IntegralTime;
                                            Device.DisplayConfig.IntTime = (float)IntegralTime;

                                        }
                                        catch (Exception ex)
                                        {
                                            log.Error(ex);
                                        }
                                    });
                                }

      
                            }
                        }
                        else if (msg.EventName == "GetDataAuto" || msg.EventName == "EQE.GetDataAuto")
                        {

                            //未来全面启用4.0之后移除
                            log.Info(FlowEngineManager.GetInstance().ServiceVersion);
                            if (FlowEngineManager.GetInstance().ServiceVersion> new Version(4, 0, 1, 104))
                            {
                                if (msg != null && msg.Data != null && msg?.Data?.MasterId != null && msg?.Data?.MasterId > 0)
                                {
                                    int masterId = msg.Data?.MasterId;
                                    using var DB = new SqlSugarClient(new ConnectionConfig  { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                                    SpectumResultEntity model = DB.Queryable<SpectumResultEntity>().Where(x => x.Id == masterId).First();

                                    log.Info($"GetData MasterId:{masterId} ");
                                    if (model != null)
                                    {
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(model);
                                            Device.View.AddViewResultSpectrum(viewResultSpectrum);

                                            try
                                            {
                                                double? IntegralTime = msg?.Data?.IntegralTime;
                                                Device.DisplayConfig.IntTime = (float)IntegralTime;

                                            }
                                            catch (Exception ex)
                                            {
                                                log.Error(ex);
                                            }
                                        });
                                    }

          
                                }
                            }
                            else
                            {
                                JObject data = msg.Data;
                                SpectrumData? colorParam = JsonConvert.DeserializeObject<SpectrumData>(JsonConvert.SerializeObject(data));
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    ViewResultSpectrum viewResultSpectrum = new ViewResultSpectrum(colorParam.Data);
                                    Device.View.AddViewResultSpectrum(viewResultSpectrum);
                                });
                            }
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


        public MsgRecord GetEqe()
        {
            var Param = new Dictionary<string, object>();
            MsgSend msg = new()
            {
                EventName = "EQE.GetData",
                Params = Param
            };
            Param.Add("IntegralTime", Device.DisplayConfig.IntTime);
            Param.Add("NumberOfAverage", Device.DisplayConfig.AveNum);
            Param.Add("AutoInitDark", Config.IsAutoDark);
            Param.Add("SelfAdaptionInitDark", Config.IsShutter);
            Param.Add("AutoIntegration", Device.DisplayConfig.IsAutoIntTime);
            Param.Add("AFactor", ViewSpectrumConfig.Instance.divisor);
            Param.Add("OutputDataFilename", "EQEData.json");

            var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            SMUResultModel sMUResultModel = new SMUResultModel() { VResult = (float)Device.DisplayConfig.V, IResult = (float)Device.DisplayConfig.I };
            int MasterId = DB.Insertable(sMUResultModel).ExecuteReturnIdentity();
            DB.Dispose();

            var SMUData = new Dictionary<string, object>() { { "V", Device.DisplayConfig.V }, { "I", Device.DisplayConfig.I },{ "Channel",0 },{ "MasterId", MasterId },{ "MasterResultType", 200 } };
            Param.Add("SMUData", SMUData);
            MsgRecord msgRecord = PublishAsyncClient(msg);
            return msgRecord;
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

        public MsgRecord GetData()
        {
            var Param = new Dictionary<string, object>();
            MsgSend msg = new()
            {
                EventName = "GetData",
                Params = Param
            };
            Param.Add("IntegralTime", Device.DisplayConfig.IntTime);
            Param.Add("NumberOfAverage", Device.DisplayConfig.AveNum);
            Param.Add("AutoInitDark", Config.IsAutoDark);
            Param.Add("SelfAdaptionInitDark", Config.IsShutter);
            Param.Add("AutoIntegration", Device.DisplayConfig.IsAutoIntTime);
            Param.Add("IsWithND", Config.IsWithND);
            if (Device.DisplayConfig.IsLuminousFluxMode)
            {
                msg.EventName = "EQE.GetData";
                Param.Add("AFactor", ViewSpectrumConfig.Instance.divisor);
                Param.Add("OutputDataFilename", "EQEData.json");
                var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                SMUResultModel sMUResultModel = new SMUResultModel() { VResult = (float)Device.DisplayConfig.V, IResult = (float)Device.DisplayConfig.I };
                int MasterId = DB.Insertable(sMUResultModel).ExecuteReturnIdentity();
                DB.Dispose();

                var SMUData = new Dictionary<string, object>() { { "V", Device.DisplayConfig.V }, { "I", Device.DisplayConfig.I }, { "Channel", 0 }, { "MasterId", MasterId }, { "MasterResultType", 200 } };
                Param.Add("SMUData", SMUData);
            }

            MsgRecord msgRecord= PublishAsyncClient(msg);
            return msgRecord;
        }
        public MsgRecord Close()
        {
            MsgSend msg = new()
            {
                EventName = "Close",
            };
            return PublishAsyncClient(msg);
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
            Params.Add("PortNum", Device.DisplayConfig.PortNum);
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


        public MsgRecord InitDark()
        {
            MsgSend msg = new()
            {
                EventName = "InitDark",
                Params = new Dictionary<string, object>() { { "IntegralTime", Device.DisplayConfig.IntTime }, { "NumberOfAverage", Device.DisplayConfig.AveNum } }

            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetDataAuto()
        {
            var Param = new Dictionary<string, object>();
            MsgSend msg = new()
            {
                EventName = "GetDataAuto",
                Params = Param
            };
            Param.Add("IntegralTime", Device.DisplayConfig.IntTime);
            Param.Add("NumberOfAverage", Device.DisplayConfig.AveNum);
            Param.Add("AutoInitDark", Config.IsAutoDark);
            Param.Add("SelfAdaptionInitDark", Config.IsShutter);
            Param.Add("AutoIntegration", Device.DisplayConfig.IsAutoIntTime);
            Param.Add("IsWithND", Config.IsWithND);

            if (Device.DisplayConfig.IsLuminousFluxMode)
            {
                msg.EventName = "EQE.GetDataAuto";

                Param.Add("AFactor", ViewSpectrumConfig.Instance.divisor);
                Param.Add("OutputDataFilename", "EQEData.json");
                var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                SMUResultModel sMUResultModel = new SMUResultModel() { VResult = (float)Device.DisplayConfig.V, IResult = (float)Device.DisplayConfig.I };
                int MasterId = DB.Insertable(sMUResultModel).ExecuteReturnIdentity();
                DB.Dispose();

                var SMUData = new Dictionary<string, object>() { { "V", Device.DisplayConfig.V }, { "I", Device.DisplayConfig.I }, { "Channel", 0 }, { "MasterId", MasterId }, { "MasterResultType", 200 } };
                Param.Add("SMUData", SMUData);
            }
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetDataAutoStop()
        {
            MsgSend msg = new()
            {
                EventName = "GetDataAutoStop",
                ServiceName = Config.Code,
            };
            return PublishAsyncClient(msg);
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
