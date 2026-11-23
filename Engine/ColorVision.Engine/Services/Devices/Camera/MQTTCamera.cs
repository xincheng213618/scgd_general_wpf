#pragma warning disable CS0168
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using cvColorVision;
using MQTTMessageLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera
{

    public class MQTTCamera : MQTTDeviceService<ConfigCamera>
    {
        private const string CaptureEvent = "GetData";
        private const int CameraImageResultType = 100;
        private readonly object _uiQueueLock = new();
        private readonly Queue<Func<Task>> _uiQueue = new();
        private readonly Action<string, string?> _showAutoExposureMessage;
        private bool _uiDrainScheduled;
        private volatile bool _retired;

        public DeviceCamera Device { get; set; }

        public MQTTCamera(DeviceCamera deviceCamera) : this(deviceCamera, MQTTControl.GetInstance(), ShowAutoExposureMessage)
        {
        }

        internal MQTTCamera(DeviceCamera deviceCamera, MQTTControl mqttControl, Action<string, string?> showAutoExposureMessage)
            : base(deviceCamera.Config, mqttControl)
        {
            Device = deviceCamera;
            _showAutoExposureMessage = showAutoExposureMessage;

            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatusType.UnInit;
        }

        public override DeviceStatusType DeviceStatus
        {
            get => Device?.CameraBackend.Status ?? base.DeviceStatus;
            set
            {
                if (Device == null) base.DeviceStatus = value;
                else { Device.CameraBackend.ObserveService(value); RefreshBackendStatus(); }
            }
        }

        internal void RefreshBackendStatus() => base.DeviceStatus = Device.CameraBackend.Status;

        internal override MsgRecord PublishAsyncClient(MsgSend msg, double timeout = 30000)
        {
            DeviceCamera owner = Device;
            lock (Local.CameraBackendState.OwnershipSync)
            {
                owner.CameraBackend.EnsureServiceAvailable();
                owner.EnsureServiceCameraAvailable();
                owner.CameraBackend.BeginServiceCommand(msg.EventName);
            }
            try
            {
                if (msg.EventName == CaptureEvent) return PublishCapture(msg, owner, timeout);

                MsgRecord record = base.PublishAsyncClient(msg, timeout);
                bool completed = false;
                void Complete(object? sender, MsgRecordState state)
                {
                    if (completed || state is not (MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout)) return;
                    completed = true;
                    record.MsgRecordStateChanged -= Complete;
                    owner.CameraBackend.EndServiceCommand();
                    if (state == MsgRecordState.Success && msg.EventName is "Open" or "Close")
                        DeviceStatus = msg.EventName == "Close" ? DeviceStatusType.Closed
                            : Config.TakeImageMode == TakeImageMode.Live ? DeviceStatusType.LiveOpened : DeviceStatusType.Opened;
                }
                record.MsgRecordStateChanged += Complete;
                Complete(record, record.MsgRecordState);
                return record;
            }
            catch { owner.CameraBackend.EndServiceCommand(); throw; }
        }

        private MsgRecord PublishCapture(MsgSend msg, DeviceCamera owner, double timeout)
        {
            var route = CaptureMqttRoute(pinConnection: true);
            var target = owner.CaptureConfigurationTarget();
            var endpoint = owner.ResultDatabaseEndpoint is { IsCurrent: true } verifiedEndpoint ? verifiedEndpoint : null;
            string? serialNumber = msg.SerialNumber;

            bool OwnsCapture(bool beforeDispatch)
            {
                var current = CaptureMqttRoute(pinConnection: true);
                return CanPresentFeedback(owner, serviceOnly: true) && target.IsCurrent(owner)
                    && (endpoint == null || owner.OwnsResultDatabase(endpoint))
                    && route.HasSameDestination(current) && IsMqttRouteUsable(route)
                    && (!beforeDispatch || string.Equals(route.ServiceToken, current.ServiceToken, StringComparison.Ordinal));
            }

            if ((!string.IsNullOrEmpty(msg.DeviceCode) && !string.Equals(msg.DeviceCode, route.DeviceCode, StringComparison.Ordinal))
                || !OwnsCapture(beforeDispatch: true))
                throw new InvalidOperationException(EngineLocalization.Get("原相机设备或 MQTT 连接已失效，请重新选择设备后重试。"));

            msg.CompleteWithoutDispatcher = true;
            return PublishOnMqttRoute(msg, route, timeout, canDispatch: () => OwnsCapture(beforeDispatch: true),
                dispatchLock: Local.CameraBackendState.OwnershipSync,
                acceptsReply: reply => AcceptsCaptureReply(reply, route.DeviceCode, serialNumber), beforeDispatch: PrepareRecord);

            void PrepareRecord(MsgRecord record)
            {
                record.BeforeWorkerCompletion(_ => owner.CameraBackend.EndServiceCommand());
                record.TerminalStateCommitted += PublishCaptureResult;

                void PublishCaptureResult(MsgRecordState state)
                {
                    record.TerminalStateCommitted -= PublishCaptureResult;
                    try
                    {
                        if (state != MsgRecordState.Success || endpoint == null || !OwnsCapture(beforeDispatch: false)
                            || record.MsgReturn is not { HasCode: true, Code: 0 } reply || reply.Data is not JObject data
                            || !MsgReturn.TryReadInteger(data["MasterId"], out int masterId) || masterId <= 0) return;

                        int resultType = MsgReturn.TryReadInteger(data["MasterResultType"], out int reportedType)
                            ? reportedType : CameraImageResultType;
                        ResultMessageBus.Default.PublishPersisted(ResultRoutes.Camera, ResultKinds.Image, route.DeviceCode,
                            CaptureEvent, string.IsNullOrWhiteSpace(serialNumber) ? reply.SerialNumber : serialNumber,
                            string.Empty, reply.ZIndex, masterId, resultType, databaseEndpoint: endpoint,
                            sourceDevice: owner, sourceService: this);
                    }
                    catch (Exception error) { log.Warn("Unable to report the camera capture result.", error); }
                }
            }
        }

        private static bool AcceptsCaptureReply(MsgReturn reply, string deviceCode, string? serialNumber)
        {
            if (!reply.HasCode || (!string.IsNullOrEmpty(reply.DeviceCode) && !string.Equals(reply.DeviceCode, deviceCode, StringComparison.Ordinal))
                || (!string.IsNullOrEmpty(reply.EventName) && !string.Equals(reply.EventName, CaptureEvent, StringComparison.Ordinal))
                || (!string.IsNullOrWhiteSpace(serialNumber) && !string.IsNullOrEmpty(reply.SerialNumber)
                    && !string.Equals(reply.SerialNumber, serialNumber, StringComparison.Ordinal))) return false;

            // Older services can acknowledge a successful capture without a persisted image reference.
            if (reply.Code == 0 && reply.Data is JObject data && data["MasterId"] is { Type: not (JTokenType.Null or JTokenType.Undefined) } id)
                return MsgReturn.TryReadInteger(id, out _);

            return true;
        }

        public override void Dispose()
        {
            lock (_uiQueueLock)
            {
                _retired = true;
                _uiQueue.Clear();
            }
            MsgReturnReceived -= MQTTCamera_MsgReturnChanged;
            base.Dispose();
            GC.SuppressFinalize(this);     
        }

        internal void TrackAutoExposureFailure(MsgRecord record, Func<bool> canShow)
        {
            TrackPanelFailure(record, serviceOnly: true, (state, reason) =>
            {
                string message = state == MsgRecordState.Timeout ? Properties.Resources.AutoExposureTimeoutCheckLog
                    : string.Format(Properties.Resources.AutoExposureFailedCheckLog, Environment.NewLine, reason);
                if (canShow()) _showAutoExposureMessage(message, "ColorVision");
                return Task.CompletedTask;
            });
        }

        internal void TrackPanelFailure(MsgRecord record, bool serviceOnly, Func<MsgRecordState, string?, Task> present)
        {
            int completed = 0;
            void Complete(object? sender, MsgRecordState state)
            {
                if (!MsgRecord.IsTerminal(state) || Interlocked.Exchange(ref completed, 1) != 0)
                    return;
                record.MsgRecordStateChanged -= Complete;
                if (state == MsgRecordState.Success)
                    return;

                string? reason = record.MsgReturn?.Message;
                QueuePanelFeedback(serviceOnly, () => present(state, reason));
            }

            record.MsgRecordStateChanged += Complete;
            Complete(record, record.MsgRecordState);
        }

        private void QueueAutoExposureResult(object? payload)
        {
            JArray? items = payload as JArray ?? (payload as JObject)?["ExpTime"] as JArray;
            bool useThreeExposures = Config.IsExpThree;
            int count = useThreeExposures ? 3 : 1;
            if (items == null || items.Count < count)
                return;

            var readings = new AutoExposureReading[count];
            int? ndPort;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    if (items[i] is not JObject reading || reading["result"] is not JValue { Value: not null } exposure
                        || reading["resultSaturation"] is not JValue { Value: not null } saturation)
                        return;
                    readings[i] = new(exposure.Value<double>(), saturation.Value<double>());
                }
                ndPort = (payload as JObject)?.Value<int?>("NDPort");
            }
            catch (Exception error)
            {
                log.Warn("Invalid camera auto-exposure response.", error);
                return;
            }

            EnqueueAutoExposureUi(camera =>
            {
                if (Config.IsExpThree != useThreeExposures)
                    return;
                var display = camera.DisplayConfig;
                if (useThreeExposures)
                {
                    var channels = Config.CFW.ChannelCfgs;
                    if (channels.Count < readings.Length)
                        return;
                    for (int i = 0; i < readings.Length; i++)
                    {
                        AutoExposureReading reading = readings[i];
                        switch (channels[i].Chtype)
                        {
                            case ImageChannelType.Gray_X:
                                display.ExpTimeR = reading.ExposureTime;
                                display.SaturationR = reading.Saturation;
                                break;
                            case ImageChannelType.Gray_Y:
                                display.ExpTimeG = reading.ExposureTime;
                                display.SaturationG = reading.Saturation;
                                break;
                            case ImageChannelType.Gray_Z:
                                display.ExpTimeB = reading.ExposureTime;
                                display.SaturationB = reading.Saturation;
                                break;
                        }
                    }
                }
                else
                {
                    display.ExpTime = readings[0].ExposureTime;
                    display.Saturation = readings[0].Saturation;
                }
                if (ndPort is { } port)
                    Config.NDPort = port;

                string message = useThreeExposures
                    ? $"SaturationR:{display.SaturationR}  ExpTime:{display.ExpTimeR}{Environment.NewLine}"
                        + $"SaturationG:{display.SaturationG}  ExpTime:{display.ExpTimeG}{Environment.NewLine}"
                        + $"SaturationB:{display.SaturationB}  ExpTime:{display.ExpTimeB}{Environment.NewLine}"
                    : $"Saturation:{display.Saturation}  ExpTime:{display.ExpTime}";
                _showAutoExposureMessage(message, null);
            });
        }

        private void EnqueueAutoExposureUi(Action<DeviceCamera> present)
        {
            DeviceCamera camera = Device;
            QueuePanelFeedback(serviceOnly: true, () =>
            {
                present(camera);
                return Task.CompletedTask;
            });
        }

        internal bool CanPresentFeedback(DeviceCamera camera, bool serviceOnly) => !_retired && !camera.IsDisposed
            && ReferenceEquals(camera, Device) && ReferenceEquals(camera.DService, this)
            && (!serviceOnly || camera.ServiceControlsEnabled);

        internal void QueuePanelFeedback(bool serviceOnly, Func<Task> present)
        {
            if (Application.Current?.Dispatcher is not { } dispatcher)
                return;
            DeviceCamera camera = Device;
            lock (_uiQueueLock)
            {
                if (_retired)
                    return;
                _uiQueue.Enqueue(() =>
                {
                    return CanPresentFeedback(camera, serviceOnly) ? present() : Task.CompletedTask;
                });
                if (_uiDrainScheduled)
                    return;
                _uiDrainScheduled = true;
            }

            dispatcher.BeginInvoke(() => { _ = DrainPanelFeedbackAsync(); });
        }

        private async Task DrainPanelFeedbackAsync()
        {
            // Keep the drain scheduled while a modal pumps Dispatcher messages so dialogs cannot nest.
            while (true)
            {
                Func<Task>? present;
                lock (_uiQueueLock)
                {
                    if (!_uiQueue.TryDequeue(out present))
                    {
                        _uiDrainScheduled = false;
                        return;
                    }
                }
                try { await present(); }
                catch (Exception error) { log.Error("Unable to present camera command feedback.", error); }
            }
        }

        private static void ShowAutoExposureMessage(string message, string? caption)
        {
            if (caption == null) MessageBox1.Show(Application.Current.GetActiveWindow(), message);
            else MessageBox1.Show(message, caption);
        }

        private readonly record struct AutoExposureReading(double ExposureTime, double Saturation);

        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            //string Msg = "{\"Data\":{\"Position\":1800,\"ImageTmpFile\":\"D:\\\\CVTest\\\\DEV.Camera.Default\\\\Data\\\\2025-03-26\\\\AutoFocus_.4425280117077446656_4.cvraw\"},\"Code\":102,\"Message\":\"Pending\",\"ServiceVersion\":\"1.0\",\"ServiceName\":\"RC_local/Camera/SVR.Camera.Default/CMD\",\"DeviceCode\":\"DEV.Camera.Default\",\"EventName\":\"AutoFocus\",\"SerialNumber\":\"\",\"MsgID\":\"5ee93a80-af4c-4621-9dc6-2fb0584c523b\",\"ZIndex\":-1}";
            //msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);
            //信息在这里添加一次过滤，让信息只能在对应的相机上显示,同时如果ID为空的话，就默认是服务端的信息，不进行过滤，这里后续在进行优化

            //string Msg = "{\"Data\":{\"nPosition\":2311,\"VidPos\":-1127.522865999267},\"Code\":0,\"Message\":\"ok\",\"ServiceVersion\":\"1.0\",\"ServiceName\":\"RC_local/Camera/SVR.Camera.Default/CMD\",\"DeviceCode\":\"DEV.Camera.Default\",\"EventName\":\"GetPosition\",\"SerialNumber\":\"\",\"MsgID\":\"1c364974-45c4-4e2f-8071-10b3d898e8af\",\"ZIndex\":-1}";
            //msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);

            //string Msg = "{\"ServiceVersion\":\"1.0\",\"EventName\":\"AutoFocus\",\"ServiceName\":\"RC_local/Camera/SVR.Camera.Default/CMD\",\"DeviceName\":null,\"DeviceCode\":\"DEV.Camera.Default1\",\"SerialNumber\":\"\",\"Code\":0,\"MsgID\":\"ac152c4d-9501-49fe-b082-03110dc26479\",\"Data\":{\"Position\":157909,\"VidPosition\":111.15504121214114,\"TotalTime\":23759,\"MasterId\":54,\"MasterResultType\":108,\"MasterValue\":null}}";
            //msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);

            //string Msg = "{\"ServiceVersion\":\"1.0\",\"EventName\":\"GetAutoExpTime\",\"ServiceName\":\"RC_local/Camera/SVR.Camera.Default/CMD\",\"DeviceName\":null,\"DeviceCode\":\"DEV.Camera.Default\",\"SerialNumber\":null,\"Code\":0,\"MsgID\":\"21ea4913-3f22-42d7-9946-d85e5b05f2d8\",\"Data\":{\"ExpTime\":[{\"result\":28.9528713,\"resultSaturation\":0.0},{\"result\":216.322388,\"resultSaturation\":0.0},{\"result\":2382.22241,\"resultSaturation\":0.0}],\"NDPort\":2}}";
            //msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);

            if (Config.Code != null && msg.DeviceCode != Config.Code) return;
            if (Device.RoutesLocally) return;

            if (msg.EventName == "AutoFocus" && msg.Code == 102 && msg.Data != null)
            {
                JObject data = msg.Data is JObject json ? json : JObject.FromObject((object)msg.Data);
                int? position = data.Value<int?>("Position");
                double? vid = data.Value<double?>("VidPosition");
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (position.HasValue) Config.MotorConfig.Position = position.Value;
                    if (vid.HasValue) Config.MotorConfig.VIDPosition = vid.Value;
                });
            }

            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    case "GetAutoExpTime":
                        QueueAutoExposureResult((object?)msg.Data);
                        break;
                    case "AutoFocus":
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                if (msg.Data != null)
                                {
                                    Config.MotorConfig.Position = msg.Data.Position;
                                    Config.MotorConfig.VIDPosition = msg.Data.VidPosition;
                                }
                            }
                            catch(Exception ex)
                            {
                                log.Error(ex);
                            }
                        });
                        break;
                    case "GetPosition":
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                if (msg.Data != null)
                                {
                                    Config.MotorConfig.Position = msg.Data.nPosition;
                                    Config.MotorConfig.VIDPosition = msg.Data.VidPos;
                                }
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);
                            }
                        });
                        break;
                    case "SetCfg":
                        break;
                    default:
                        break;
                }
            }
        }
        public MsgRecord Open(string CameraID, TakeImageMode TakeImageMode, int ImageBpp)
        {
            if (Device.CameraBackend.OpensLocally) return Device.OpenLocalCamera(CameraID, TakeImageMode, ImageBpp);
            CameraID = Device.ResolveCameraId(CameraID);
            if (!string.IsNullOrWhiteSpace(Config.CameraCode) && string.IsNullOrWhiteSpace(CameraID))
                throw new InvalidOperationException(EngineLocalization.Get("Camera_LocalBindingUnavailable"));
            if (!string.IsNullOrWhiteSpace(Config.CameraCode))
            {
                try { Device.SaveResolvedCameraId(CameraID); }
                catch (Exception ex) { log.Error("保存相机 CameraID 失败；使用已校验的物理身份连接，下次连接将重试保存。", ex); }
            }
            MsgSend msg = new()
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { { "TakeImageMode", (int)TakeImageMode }, { "CameraID", CameraID }, { "Bpp", ImageBpp } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord SetNDPort()
        {
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new()
            {
                EventName = "SetParam",
                Params = Params
            };
            var Func = new List<ParamFunction>();
            if (Config.CFW.IsUseCFW && Config.CFW.IsNDPort)
            {
                Func.Add(new ParamFunction() { Name = "CM_SetNDPort", Params = new Dictionary<string, object>() { { "NDPort", Config.NDPort } } });
            }
            Params.Add("Func", Func);
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


        public MsgRecord GetData(double[] expTime, CalibrationParam param, ParamBase autoExpTimeParam)
        {
            if (Device.RoutesLocally) return Device.CaptureLocally(expTime, param, autoExpTimeParam);
            return CaptureRemotely(expTime, param, autoExpTimeParam);
        }

        internal MsgRecord CaptureRemotely(double[] expTime, CalibrationParam param, ParamBase autoExpTimeParam)
        {
            string SerialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var Params = new Dictionary<string, object>() { };
            MsgSend msg;
            msg = new MsgSend
            {
                EventName = CaptureEvent,
                SerialNumber = SerialNumber,
                Params = Params
            };
            Params.Add("AvgCount", Device.DisplayConfig.AvgCount);
            Params.Add("ExpTime", expTime);
            Params.Add("FlipMode", Device.DisplayConfig.FlipMode);

            if (param.Id == -1)
            {
                Params.Add("Calibration", new CVTemplateParam() { ID = param.Id,Name = string.Empty });
            }
            else
            {
                Params.Add("Calibration", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            }
            if (autoExpTimeParam.Id == -1)
            {
                Params.Add("IsAutoExpTime", false);
            }
            else
            {
                Params.Add("IsAutoExpTime", true);
                Params.Add("IsAutoExpWithND", Config.IsAutoExpWithND);

                if (autoExpTimeParam.Id == -2)
                {
                    Params.Add("AutoExpTimeTemplate", new CVTemplateParam() { ID = -1, Name = string.Empty });
                }
                else
                {
                    Params.Add("AutoExpTimeTemplate", new CVTemplateParam() { ID = autoExpTimeParam.Id, Name = autoExpTimeParam.Name });
                }
            }

            Params.Add("ScaleFactor",1);
            Params.Add("Gain", Device.DisplayConfig.Gain);
            double timeout = 0;
            for (int i = 0; i < expTime.Length; i++) timeout += expTime[i];
            return PublishAsyncClient(msg, timeout + 40000);
        }  

        public MsgRecord GetCameraID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetSnID" });

        public MsgRecord AutoFocus(AutoFocusParam param)
        {
            if (Device.CameraBackend.OpensLocally) return Device.AutoFocusLocally(param);
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new()
            {
                EventName = "AutoFocus",
                Params = Params
            };
            Params.Add("AutoFocusTemplate", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            return PublishAsyncClient(msg, param.nTimeout);
        }

        public MsgRecord GetAutoExpTime(ParamBase autoExpTimeParam)
        {
            if (Device.RoutesLocally) return Device.AutoExposeLocally(autoExpTimeParam, null);
            var Params = new Dictionary<string, object>() { };
            Params.Add("AutoExpTimeTemplate", new CVTemplateParam() { ID = autoExpTimeParam.Id, Name = autoExpTimeParam.Name });
            Params.Add("IsAutoExpWithND",  Config.IsAutoExpWithND);

            
            MsgSend msg = new()
            {
                EventName = "GetAutoExpTime",
                Params = Params
            };
            return PublishAsyncClient(msg,60000);
        }


        public MsgRecord Close()
        {
            if (Device.RoutesLocally) return Device.CloseLocalCamera();
            MsgSend msg = new() {  EventName = "Close" };
            return PublishAsyncClient(msg);
        }


        public MsgRecord Move(int nPosition, bool IsbAbs = true, int dwTimeOut = 5000)
        {
            if (Device.CameraBackend.OpensLocally) return Device.RunLocalMotorCommand("Move", motor => motor.Move(nPosition, IsbAbs));

            MsgSend msg = new()
            {
                EventName = "Move",
                Params = new Dictionary<string, object>() {
                    {"nPosition",nPosition },{"dwTimeOut", Config.MotorConfig.DwTimeOut },{ "bAbs", IsbAbs}
                }
            };
            return PublishAsyncClient(msg);
        }
        public MsgRecord MoveDiaphragm(double dPosition, int dwTimeOut = 5000)
        {
            if (Device.CameraBackend.OpensLocally) return Device.RunLocalMotorCommand("MoveDiaphragm", motor => { motor.Aperture(dPosition); return null; });

            MsgSend msg = new()
            {
                EventName = "MoveDiaphragm",
                Params = new Dictionary<string, object>() { { "dPosition", dPosition }, { "dwTimeOut", Config.MotorConfig.DwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }



        public MsgRecord GoHome()
        {
            if (Device.CameraBackend.OpensLocally) return Device.RunLocalMotorCommand("GoHome", motor => motor.Home());
            MsgSend msg = new()
            {
                EventName = "GoHome",
                Params = new Dictionary<string, object>() { { "dwTimeOut", Config.MotorConfig.DwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetPosition()
        {
            if (Device.CameraBackend.OpensLocally) return Device.RunLocalMotorCommand("GetPosition", motor => motor.ReadPosition());
            MsgSend msg = new()
            {
                EventName = "GetPosition",
                Params = new Dictionary<string, object>() { { "dwTimeOut", Config.MotorConfig.DwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }



    }
}
