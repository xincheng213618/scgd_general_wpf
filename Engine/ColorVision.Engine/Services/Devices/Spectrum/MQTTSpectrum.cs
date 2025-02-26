#pragma warning disable CS8604
using ColorVision.Engine.MQTT;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Messages;
using MQTTMessageLib;
using MQTTMessageLib.Spectrum;
using MQTTnet.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Google.Protobuf.WellKnownTypes;
using iText.Commons.Bouncycastle.Asn1.X509;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib;
using MQTTMessageLib.Algorithm;
using OpenTK.Compute.OpenCL;

namespace ColorVision.Engine.Services.Devices.Spectrum
{

    public class MQTTSpectrum : MQTTDeviceService<ConfigSpectrum>
    {
        public event EventHandler<SpectrumData> DataHandlerEvent;
        public Dictionary<string, MsgSend> cmdMap { get; set; }

        public DeviceSpectrum DeviceSpectrum { get; set; }

        public MQTTSpectrum(DeviceSpectrum DeviceSpectrum) : base(DeviceSpectrum.Config)
        {
            this.DeviceSpectrum = DeviceSpectrum;
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
            cmdMap = new Dictionary<string, MsgSend>();
        }


        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                log.Info(Msg);
                try
                {
                    MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    if (json.Code == 0 || json.Code == 102)
                    {
                        if (json.EventName == "SetParam")
                        {
                        }
                        else if (json.EventName == "Open")
                        {
                        }
                        else if (json.EventName == "GetData")
                        {
                            try
                            {
                                int MasterId = json.Data.MasterId;
                                var sss = SpectumResultDao.Instance.GetById(MasterId);
                                ViewResultSpectrum viewResultSpectrum = new(sss);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    DeviceSpectrum.View.AddViewResultSpectrum(viewResultSpectrum);
                                });
                            }
                            catch
                            {
                                ///旧版本兼容

                                JObject data = json.Data;
                                SpectrumData? colorParam = JsonConvert.DeserializeObject<SpectrumData>(JsonConvert.SerializeObject(data));
                                Application.Current.Dispatcher.Invoke(() => DataHandlerEvent?.Invoke(this,colorParam));
                            }


                        }
                        else if (json.EventName == "GetDataAuto")
                        {
                            JObject data = json.Data;
                            SpectrumData? colorParam = JsonConvert.DeserializeObject<SpectrumData>(JsonConvert.SerializeObject(data));
                            if (cmdMap.ContainsKey(json.MsgID))
                            {
                                Application.Current.Dispatcher.Invoke(() => DataHandlerEvent?.Invoke(this,colorParam));
                            }
                        }
                        else if (json.EventName == "Close")
                        {
                        }
                        else if (json.EventName == "GetParam")
                        {
                            AutoIntTimeParam param = JsonConvert.DeserializeObject<AutoIntTimeParam>(JsonConvert.SerializeObject(json.Data));
                            Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                DeviceSpectrum.Config.BeginIntegralTime = param.fTimeB;
                                DeviceSpectrum.Config.MaxIntegralTime = param.iLimitTime;
                            });
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

        public bool GetData(float IntTime, int AveNum, bool bUseAutoIntTime = false, bool bUseAutoDark = false, bool bUseAutoShutterDark = false)
        {
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            MsgSend msg = new()
            {
                EventName = "GetData",
                SerialNumber = sn,
                ServiceName = Config.Code,
                Params = new GetDataParamMQTT()
                {
                    IntTime = IntTime,
                    AveNum = AveNum,
                    BUseAutoIntTime = bUseAutoIntTime,
                    BUseAutoDark = bUseAutoDark,
                    BUseAutoShutterDark = bUseAutoShutterDark,
                }
            };
            PublishAsyncClient(msg);
            cmdMap.Add(msg.MsgID.ToString(), msg);
            return true;
        }

        public bool Close()
        {
            MsgSend msg = new()
            {
                EventName = "Close",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
            return true;
        }
        public MsgRecord SelfAdaptionInitDark()
        {
            var Params = new Dictionary<string, object>() { };
            Params.Add("SpectrumSelfAdaptionInitDark",Config.SelfAdaptionInitDark);
            MsgSend msg = new()
            {
                EventName = "InitAutoDark",
                SerialNumber = Config.Code,
                Params = Params
            };
            return PublishAsyncClient(msg);
        }

        public bool InitDark(float IntTime, int AveNum)
        {
            MsgSend msg = new()
            {
                EventName = "InitDark",
                ServiceName = Config.Code,
                Params = new Dictionary<string, object>() { { "IntegralTime", IntTime }, { "NumberOfAverage", AveNum } }

            };
            PublishAsyncClient(msg);
            return true;
        }

        public void GetDataAuto(float IntTime, int AveNum, bool bUseAutoIntTime = false, bool bUseAutoDark = false, bool bUseAutoShutterDark = false)
        {
            MsgSend msg = new()
            {
                EventName = "GetDataAuto",
                ServiceName = Config.Code,
                Params = new GetDataParamMQTT()
                {
                    IntTime = IntTime,
                    AveNum = AveNum,
                    BUseAutoIntTime = bUseAutoIntTime,
                    BUseAutoDark = bUseAutoDark,
                    BUseAutoShutterDark = bUseAutoShutterDark,
                }
            };
            PublishAsyncClient(msg);
            cmdMap.Add(msg.MsgID.ToString(), msg);
        }

        public void GetDataAutoStop()
        {
            MsgSend msg = new()
            {
                EventName = "GetDataAutoStop",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
            cmdMap.Clear();
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
