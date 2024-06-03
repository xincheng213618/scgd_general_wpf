using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.MQTT;
using ColorVision.Services.Msg;
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
using ColorVision.Common.Utilities;
using MQTTMessageLib.FileServer;
using System.Diagnostics;
using CVCommCore;

namespace ColorVision.Services.Devices.Spectrum
{
    public delegate void MQTTSpectrumDataHandler(SpectrumData? colorPara);
    public delegate void MQTTAutoParamHandler(AutoIntTimeParam colorPara);
    public delegate void MQTTSpectrumHeartbeatHandler(SpectrumHeartbeatParam heartbeat);

    public class MQTTSpectrum : MQTTDeviceService<ConfigSpectrum>
    {
        public event MQTTSpectrumDataHandler DataHandlerEvent;
        public event MQTTAutoParamHandler AutoParamHandlerEvent;
        public event MQTTSpectrumHeartbeatHandler HeartbeatHandlerEvent;

        public Dictionary<string, MsgSend> cmdMap { get; set; }

        public MQTTSpectrum(ConfigSpectrum spectrumConfig) : base(spectrumConfig)
        {
            Config = spectrumConfig;

            SendTopic = spectrumConfig.SendTopic;
            SubscribeTopic = spectrumConfig.SubscribeTopic;

            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
            cmdMap = new Dictionary<string, MsgSend>();
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
                        if (json.EventName == "SetParam")
                        {
                        }
                        else if (json.EventName == "Open")
                        {
                        }
                        else if (json.EventName == "GetData")
                        {
                            JObject data = json.Data;
                            SpectrumData? colorParam = JsonConvert.DeserializeObject<SpectrumData>(JsonConvert.SerializeObject(data));
                            if (cmdMap.ContainsKey(json.MsgID))
                            {
                                Application.Current.Dispatcher.Invoke(() => DataHandlerEvent?.Invoke(colorParam));
                                cmdMap.Remove(json.MsgID);
                            }
                        }
                        else if (json.EventName == "GetDataAuto")
                        {
                            JObject data = json.Data;
                            SpectrumData? colorParam = JsonConvert.DeserializeObject<SpectrumData>(JsonConvert.SerializeObject(data));
                            if (cmdMap.ContainsKey(json.MsgID))
                            {
                                Application.Current.Dispatcher.Invoke(() => DataHandlerEvent?.Invoke(colorParam));
                                //cmdMap.Remove(json.MsgID);
                            }
                        }
                        else if (json.EventName == "Heartbeat" && json.ServiceName.Equals(ServiceName, StringComparison.Ordinal))
                        {
                            List<SpectrumDeviceHeartbeatParam> devs_heartbeat = JsonConvert.DeserializeObject<List<SpectrumDeviceHeartbeatParam>>(JsonConvert.SerializeObject(json.Data));
                            if (devs_heartbeat != null && devs_heartbeat.Count > 0) DoSpectumHeartbeat(devs_heartbeat);
                        }
                        else if (json.EventName == "Close")
                        {
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

        public void DoSpectumHeartbeat(List<SpectrumDeviceHeartbeatParam> devsheartbeat)
        {
            foreach (SpectrumDeviceHeartbeatParam devheartbeat in devsheartbeat)
            {
                if (devheartbeat.DeviceName!=null && devheartbeat.DeviceName.Equals(Config.Code, StringComparison.Ordinal))
                {
                    SpectrumHeartbeatParam heartbeat = new();
                    heartbeat.DeviceStatus = devheartbeat.DeviceStatus;
                    heartbeat.IsAutoGetData = devheartbeat.IsAutoGetData;
                    DoSpectumHeartbeat(heartbeat);
                }
            }
        }

        public void DoSpectumHeartbeat(SpectrumHeartbeatParam heartbeat)
        {
            Application.Current.Dispatcher.Invoke(() => HeartbeatHandlerEvent?.Invoke(heartbeat));
        }

        public bool Init()
        {
            MsgSend msg = new()
            {
                EventName = "Init"
            };
            PublishAsyncClient(msg);
            return true;
        }


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
                Params = new AutoIntTimeParam()
                {
                    iLimitTime = iLimitTime,
                    fTimeB = fTimeB
                }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public MsgRecord Open(SpectrumResourceParam spectrumResourceParam)
        {
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new()
            {
                EventName = "Open",
                ServiceName = Config.Code,
                Params = Params
            };
            if (spectrumResourceParam.Id == -1)
            {
                Params.Add("TemplateParam", new CVTemplateParam() { ID = spectrumResourceParam.Id, Name = string.Empty });
            }
            else
            {
                Params.Add("TemplateParam", new CVTemplateParam() { ID = spectrumResourceParam.Id, Name = spectrumResourceParam.Name });
            }
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

        internal bool InitDark(float IntTime, int AveNum)
        {
            MsgSend msg = new()
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

        internal void GetDataAutoStop()
        {
            MsgSend msg = new()
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
            HeartbeatParam heartbeat = new();
            foreach (var item in nodeService.Devices)
            {
                if (Config.Code.Equals(item.Key, StringComparison.Ordinal))
                {
                    var dev = item.Value;
                    DeviceStatusType devStatus = (DeviceStatusType)Enum.Parse(typeof(DeviceStatusType), dev.Status);
                    switch (devStatus)
                    {
                        case DeviceStatusType.Unknown:
                            heartbeat.DeviceStatus = DeviceStatusType.Closed;
                            break;
                        case DeviceStatusType.Closed:
                            heartbeat.DeviceStatus = DeviceStatusType.Closed;
                            break;
                        case DeviceStatusType.Closing:
                            heartbeat.DeviceStatus = DeviceStatusType.Closing;
                            break;
                        case DeviceStatusType.Opened:
                            heartbeat.DeviceStatus = DeviceStatusType.Opened;
                            break;
                        case DeviceStatusType.Opening:
                            heartbeat.DeviceStatus = DeviceStatusType.Opening;
                            break;
                        case DeviceStatusType.Busy:
                            heartbeat.DeviceStatus = DeviceStatusType.Busy;
                            break;
                        case DeviceStatusType.Free:
                            heartbeat.DeviceStatus = DeviceStatusType.Free;
                            break;
                        default:
                            heartbeat.DeviceStatus = DeviceStatusType.Closed;
                            break;
                    }
                }
            }

            DoHeartbeat(heartbeat);
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


        public async Task<MsgRecord> UploadFileAsync(string name, string fileName, int fileType, int timeout = 50000)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start(); // 开始计时

            TaskCompletionSource<MsgRecord> tcs = new();
            string md5 = Tool.CalculateMD5(fileName);
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_Upload,
                Params = new Dictionary<string, object> { { "Name", name }, { "FileName", fileName }, { "FileExtType", FileExtType.Calibration }, { "MD5", md5 } }
            };
            MsgRecord msgRecord = PublishAsyncClient(msg);

            MsgRecordStateChangedHandler handler = (sender) =>
            {
                log.Info($"{fileName}  状态{sender}  Operation time: {stopwatch.ElapsedMilliseconds} ms");
                tcs.TrySetResult(msgRecord);
            };
            msgRecord.MsgRecordStateChanged += handler;
            var timeoutTask = Task.Delay(timeout);
            try
            {

                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    log.Info($"{fileName}  超时  Operation time: {stopwatch.ElapsedMilliseconds} ms");
                    tcs.TrySetException(new TimeoutException("The operation has timed out."));
                }
                return await tcs.Task; // 如果超时，这里将会抛出异常
            }
            catch (Exception ex)
            {
                log.Info($"{fileName}  异常 {ex.Message} Operation time: {stopwatch.ElapsedMilliseconds} ms");
                tcs.TrySetException(ex);
                return await tcs.Task; // 
            }
            finally
            {
                msgRecord.MsgRecordStateChanged -= handler;
            }
        }
    }
}
