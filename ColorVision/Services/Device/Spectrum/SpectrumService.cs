using ColorVision.MQTT;
using ColorVision.Services.Msg;
using MQTTMessageLib;
using MQTTnet.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Device.Spectrum
{
    public delegate void MQTTSpectrumDataHandler(SpectumData? colorPara);
    public delegate void MQTTAutoParamHandler(AutoIntTimeParam colorPara);
    public delegate void MQTTSpectrumHeartbeatHandler(SpectumHeartbeatParam heartbeat);

    public class SpectrumService : BaseService<SpectrumConfig>
    {
        public event MQTTSpectrumDataHandler DataHandlerEvent;
        public event MQTTAutoParamHandler AutoParamHandlerEvent;
        public event MQTTSpectrumHeartbeatHandler HeartbeatHandlerEvent;



        public Dictionary<string, MsgSend> cmdMap { get; set; }

        public SpectrumService(SpectrumConfig spectrumConfig) : base(spectrumConfig)
        {
            Config = spectrumConfig;

            SendTopic = spectrumConfig.SendTopic;
            SubscribeTopic = spectrumConfig.SubscribeTopic;

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
                    //if (json.Code == 0)
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
                            JObject data = json.Data;
                            SpectumData? colorParam = JsonConvert.DeserializeObject<SpectumData>(JsonConvert.SerializeObject(data));
                            if (cmdMap.ContainsKey(json.MsgID))
                            {
                                Application.Current.Dispatcher.Invoke(() => DataHandlerEvent?.Invoke(colorParam));
                                cmdMap.Remove(json.MsgID);
                            }
                        }
                        else if (json.EventName == "GetDataAuto")
                        {
                            JObject data = json.Data;
                            SpectumData? colorParam = JsonConvert.DeserializeObject<SpectumData>(JsonConvert.SerializeObject(data));
                            if (cmdMap.ContainsKey(json.MsgID))
                            {
                                Application.Current.Dispatcher.Invoke(() => DataHandlerEvent?.Invoke(colorParam));
                                //cmdMap.Remove(json.MsgID);
                            }
                        }
                        else if (json.EventName == "Heartbeat" && json.ServiceName.Equals(this.ServiceName, System.StringComparison.Ordinal))
                        {
                            List<SpectumDeviceHeartbeatParam> devs_heartbeat = JsonConvert.DeserializeObject<List<SpectumDeviceHeartbeatParam>>(JsonConvert.SerializeObject(json.Data));
                            if (devs_heartbeat != null && devs_heartbeat.Count > 0) DoSpectumHeartbeat(devs_heartbeat);
                        }
                        else if (json.EventName == "Close")
                        {
                            //MessageBox.Show("Close");
                        }
                        else if (json.EventName == "Uninit")
                        {
                            //MessageBox.Show("Uninit");
                        }
                        else if (json.EventName == "GetParam")
                        {
                            AutoIntTimeParam param = JsonConvert.DeserializeObject<AutoIntTimeParam>(JsonConvert.SerializeObject(json.Data));
                            Application.Current.Dispatcher.Invoke(() => AutoParamHandlerEvent?.Invoke(param));
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

        public void DoSpectumHeartbeat(List<SpectumDeviceHeartbeatParam> devsheartbeat)
        {
            foreach (SpectumDeviceHeartbeatParam devheartbeat in devsheartbeat)
            {
                if (devheartbeat.DeviceName.Equals(Config.Code, StringComparison.Ordinal))
                {
                    SpectumHeartbeatParam heartbeat = new SpectumHeartbeatParam();
                    heartbeat.DeviceStatus = devheartbeat.DeviceStatus;
                    heartbeat.IsAutoGetData = devheartbeat.IsAutoGetData;
                    DoSpectumHeartbeat(heartbeat);
                }
            }
        }

        public void DoSpectumHeartbeat(SpectumHeartbeatParam heartbeat)
        {
            Application.Current.Dispatcher.Invoke(() => HeartbeatHandlerEvent?.Invoke(heartbeat));
        }

        public bool Init()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Init"
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool UnInit()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "UnInit",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public void GetParam()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetParam",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
        }

        public bool SetParam(int iLimitTime, float fTimeB)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                ServiceName = Config.Code,
                Params = new AutoIntTimeParam()
                {
                    iLimitTime = iLimitTime,
                    fTimeB = fTimeB
                }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Open()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                ServiceName = Config.Code
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool GetData(float IntTime, int AveNum, bool bUseAutoIntTime = false, bool bUseAutoDark = false, bool bUseAutoShutterDark = false)
        {
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            MsgSend msg = new MsgSend
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
            //if (ServiceID == 0)
            //{
            //    MessageBox.Show("请先初始化");
            //    return false;
            //}
            MsgSend msg = new MsgSend
            {
                EventName = "Close",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
            return true;
        }

        internal bool InitDark(float IntTime, int AveNum)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "InitDark",
                ServiceName = Config.Code,
                Params = new InitDarkParamMQTT()
                {
                    IntTime = IntTime,
                    AveNum = AveNum,
                }
            };
            PublishAsyncClient(msg);
            return true;
        }

        internal void GetDataAuto(float IntTime, int AveNum, bool bUseAutoIntTime = false, bool bUseAutoDark = false, bool bUseAutoShutterDark = false)
        {
            MsgSend msg = new MsgSend
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

        internal void GetDataAutoStop()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetDataAutoStop",
                ServiceName = Config.Code,
            };
            PublishAsyncClient(msg);
            cmdMap.Clear();
        }

        public override void UpdateStatus(MQTTNodeService nodeService)
        {
            base.UpdateStatus(nodeService);
            HeartbeatParam heartbeat = new HeartbeatParam();
            foreach (var item in nodeService.Devices)
            {
                if (Config.Code.Equals(item.Key, System.StringComparison.Ordinal))
                {
                    switch (item.Value)
                    {
                        case DeviceStatusType.Unknown:
                            heartbeat.DeviceStatus = DeviceStatus.Closed;
                            break;
                        case DeviceStatusType.Closed:
                            heartbeat.DeviceStatus = DeviceStatus.Closed;
                            break;
                        case DeviceStatusType.Closing:
                            heartbeat.DeviceStatus = DeviceStatus.Closing;
                            break;
                        case DeviceStatusType.Opened:
                            heartbeat.DeviceStatus = DeviceStatus.Opened;
                            break;
                        case DeviceStatusType.Opening:
                            heartbeat.DeviceStatus = DeviceStatus.Opening;
                            break;
                        case DeviceStatusType.Busy:
                            heartbeat.DeviceStatus = DeviceStatus.Busy;
                            break;
                        case DeviceStatusType.Free:
                            heartbeat.DeviceStatus = DeviceStatus.Free;
                            break;
                        default:
                            heartbeat.DeviceStatus = DeviceStatus.Closed;
                            break;
                    }
                }
            }

            DoHeartbeat(heartbeat);
        }
    }
}
