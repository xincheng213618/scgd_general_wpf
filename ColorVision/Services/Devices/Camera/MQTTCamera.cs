#pragma warning disable CS8602,CA1707

using ColorVision.Services.Msg;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Windows;
using ColorVision.Extension;
using MQTTMessageLib.FileServer;
using MQTTMessageLib.Camera;
using MQTTMessageLib;
using ColorVision.Services.Devices.Camera.Configs;
using ColorVision.Services.Devices.Camera.Calibrations;
using System.Threading.Tasks;
using System.Diagnostics;
using ColorVision.Common.Utilities;

namespace ColorVision.Services.Devices.Camera
{
    public delegate void MQTTCameraMsgHandler(object sender, MsgReturn msg);

    public class MQTTCamera : MQTTDeviceService<ConfigCamera>
    {
        public event MessageRecvHandler OnMessageRecved;

        public MQTTTerminalCamera CameraService { get; set; }

        public override bool IsAlive {get =>  Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); }}

        public MQTTCamera(ConfigCamera CameraConfig, MQTTTerminalCamera cameraService) : base(CameraConfig)
        {
            CameraService = cameraService;
            CameraService.Devices.Add(this);
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatusType.OffLine;
            GetAllCameraID();
        }

        public override void Dispose()
        {
            CameraService.Devices.Remove(this);
            base.Dispose();
            GC.SuppressFinalize(this);     
        }

        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            switch (msg.EventName)
            {
                case "CM_GetAllSnID":
                    return;
            }
            //信息在这里添加一次过滤，让信息只能在对应的相机上显示,同时如果ID为空的话，就默认是服务端的信息，不进行过滤，这里后续在进行优化
            if (Config.Code!=null && msg.DeviceCode != Config.Code)
            {
                return;
            }
            if (msg.Code == 0)
            {

                switch (msg.EventName)
                {
                    case "Close":
                        break;
                    case MQTTCameraEventEnum.Event_Open:
                    case MQTTCameraEventEnum.Event_OpenLive:
                    case MQTTCameraEventEnum.Event_GetData:
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        break;
                    case "GetAutoExpTime":
                        if (msg.Data != null && msg.Data[0].result != null)
                        {
                            if (Config.IsExpThree)
                            {
                                for (int i = 0; i < 3; i++)
                                {
                                    if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_X)
                                    {
                                        Config.ExpTimeR = (int)msg.Data[i].result;
                                        Config.SaturationR = (int)msg.Data[i].resultSaturation;
                                    }
                                    if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_Y)
                                    {
                                        Config.ExpTimeG = (int)msg.Data[i].result;
                                        Config.SaturationG = (int)msg.Data[i].resultSaturation;
                                    }

                                    if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_Z)
                                    {
                                        Config.ExpTimeB = (int)msg.Data[i].result;
                                        Config.SaturationB = (int)msg.Data[i].resultSaturation;
                                    }
                                }

                                string Msg = "SaturationR:" + Config.SaturationR.ToString() + Environment.NewLine +
                                             "SaturationG:" + Config.SaturationG.ToString() + Environment.NewLine +
                                             "SaturationB:" + Config.SaturationB.ToString() + Environment.NewLine;

                                Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, Msg));
                            }
                            else
                            {
                                Config.ExpTime = (int)msg.Data[0].result;
                                Config.Saturation = (int)msg.Data[0].resultSaturation;

                                string Msg = "Saturation:" + Config.Saturation.ToString();
                                Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, Msg));
                            }
                        } 
                        break;
                    case "SaveLicense":
                        log.Debug($"SaveLicense:{msg.Data}");
                        break;
                    case "Calibration":
                        log.Debug($"Calibration:{msg.Data}");
                        break;
                    case "Move":
                        break;
                    case "MoveDiaphragm":
                        break;
                    case "AutoFocus":
                        Config.MotorConfig.Position = msg.Data.nPos;
                        break;
                    case "GetPosition":
                        Config.MotorConfig.Position = msg.Data.nPosition;
                        break;
                    case "SetCfg":
                        break;
                    default:
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        break;
                }
            }
            else
            {
                switch (msg.EventName)
                {
                    case "Close":
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    case "Open":
                        if (DeviceStatus == DeviceStatusType.Closed)
                            Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, "打开失败"));
                        DeviceStatus = DeviceStatusType.Closed;
                        break;
                    case "Init":
                        break;
                    case "UnInit":
                        break;
                    default:
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        break;
                }
            }
        }


        public CameraType CurrentCameraType { get; set; }

        public MsgRecord Init()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
            };
            var Params = new Dictionary<string, object>() { { "CameraType", (int)Config.CameraType }, { "SnID", Config.Id }, { "CodeID", Config.Code }, { "szCfgName", "" } };
            msg.Params = Params;

            ///如果配置电机，则传入电机的参数
            if (Config.IsHaveMotor)
            {
                var AutoFocus = new Dictionary<string, object>() { };
                AutoFocus.Add("eFOCUS_COMMUN", Config.MotorConfig.eFOCUSCOMMUN);
                AutoFocus.Add("szComName", Config.MotorConfig.SzComName);
                AutoFocus.Add("BaudRate", Config.MotorConfig.BaudRate);
                Params.Add("AutoFocus", AutoFocus);
            }

            if (Config.CFW.IsCOM)
            {
                var CFWPORT = new Dictionary<string, object>() { };
                CFWPORT.Add("szComName", Config.CFW.SzComName);
                CFWPORT.Add("BaudRate", Config.CFW.BaudRate);
                Params.Add("CFWPORT", CFWPORT);
            }

            ///这里设置1s超时，如果超时则认为初始化失败
            return PublishAsyncClient(msg,1000);
        }




        public MsgRecord UnInit()
        {
            IsVideoOpen = false;
            MsgSend msg = new MsgSend  {  EventName = "UnInit", };
            return PublishAsyncClient(msg);
        }

        public MsgRecord SetCfg(CameraConfigType configType)
        {
            MsgSend msg = new MsgSend { EventName = "SetCfg" };

            var Params = new Dictionary<string,object>();
            Params.Add("ConfigType", configType);
            switch (configType)
            {   
                case CameraConfigType.Camera:
                    Params.Add("jsonCfg", Config.CameraCfg.ToJsonN());
                    break;
                case CameraConfigType.ExpTime:
                    Params.Add("jsonCfg", Config.ExpTimeCfg.ToJsonN());
                    break;
                case CameraConfigType.Calibration:
                    Params.Add("jsonCfg", Config.ExpTimeCfg);
                    break;
                case CameraConfigType.Channels:
                    Params.Add("jsonCfg", Config.CFW.ChannelCfgs.ToJsonN());
                    break;
                case CameraConfigType.SYSTEM:
                    Params.Add("jsonCfg", Config.ExpTimeCfg);
                    break;
                default:
                    break;
            }
            msg.Params = Params;
            return PublishAsyncClient(msg);
        }

        public MsgRecord CfwPortSetPort(int nIndex, int nPort, int eImgChlType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                Params = new Dictionary<string, object>() { { "Func",new List<ParamFunction> (){
                    new ParamFunction() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", nIndex }, { "nPort", nPort },{ "eImgChlType" , eImgChlType } }  } } } }
            };
            return PublishAsyncClient(msg);
        }
        public MsgRecord Calibration(CalibrationParam item)
        {
            Dictionary<string, object> Params = new Dictionary<string, object>();
            MsgSend msg = new MsgSend
            {
                EventName = "Calibration",
                Params = Params
            };

            if (item.Color.Luminance.IsSelected)
            {
                Params.Add("CalibType", "Luminance");
                Params.Add("CalibTypeFileName", item.Color.Luminance.FilePath);
            }

            if (item.Color.LumOneColor.IsSelected)
            {
                Params.Add("CalibType", "LumOneColor");
                Params.Add("CalibTypeFileName", item.Color.LumOneColor.FilePath);
            }
            if (item.Color.LumFourColor.IsSelected)
            {
                Params.Add("CalibType", "LumFourColor");
                Params.Add("CalibTypeFileName", item.Color.LumFourColor.FilePath);
            }
            if (item.Color.LumMultiColor.IsSelected)
            {
                Params.Add("CalibType", "LumMultiColor");
                Params.Add("CalibTypeFileName", item.Color.LumMultiColor.FilePath);
            }

            List<Dictionary<string, object>> List = new List<Dictionary<string, object>>
            {
                item.Normal.ToDictionary(),
            };
            Params.Add("List", List);
            return PublishAsyncClient(msg);
        }
        private bool _IsVideoOpen ;
        public bool IsVideoOpen { get => _IsVideoOpen; set { _IsVideoOpen = value;NotifyPropertyChanged(); } }

        public MsgRecord OpenVideo(string host, int port,double expTime)
        {
            CurrentTakeImageMode = TakeImageMode.Live;
            IsVideoOpen = true;
            bool IsLocal = (host=="127.0.0.1");
            MsgSend msg = new MsgSend
            {
                EventName = "OpenLive",
                Params = new Dictionary<string, object>() { { "RemoteIp", host }, { "RemotePort", port }, { "ExpTime", expTime }, { "IsLocal", IsLocal } }
            };
             return PublishAsyncClient(msg);
        }
        public TakeImageMode CurrentTakeImageMode { get; set; }

        public MsgRecord Open(string CameraID, TakeImageMode TakeImageMode, int ImageBpp)
        {
            CurrentTakeImageMode = TakeImageMode;
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { { "TakeImageMode", (int)TakeImageMode }, { "CameraID", CameraID }, { "Bpp", ImageBpp },{ "remoteIp", Config.VideoConfig.Host },{ "remotePort", Config.VideoConfig.Port } }
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord SetExp()
        {
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                Params = Params
            };

            if (Config.IsExpThree&&!IsVideoOpen)
            {
                var FunParams = new Dictionary<string, object>() { };
                FunParams.Add("nIndex",0);
                FunParams.Add("dExp", Config.ExpTimeR);

                var FunParams1 = new Dictionary<string, object>() { };
                FunParams1.Add("nIndex", 1);
                FunParams1.Add("dExp", Config.ExpTimeG);

                var FunParams2 = new Dictionary<string, object>() { };
                FunParams2.Add("nIndex", 2);
                FunParams2.Add("dExp", Config.ExpTimeB);

                var FunParamss = new List<Dictionary<string, object>>();
                FunParamss.Add(FunParams);
                FunParamss.Add(FunParams1);
                FunParamss.Add(FunParams2);

                var Func = new List<ParamFunction>();
                foreach (var item in FunParamss)
                {
                    var Fun = new ParamFunction() { Name = "CM_SetExpTimeEx", Params = item };
                    Func.Add(Fun);
                }
                Params.Add("Func", Func);
            }
            else
            {

                var FunParams = new Dictionary<string, object>() { };
                FunParams.Add("dExp", Config.ExpTime);

                var Fun = new ParamFunction() { Name = "CM_SetExpTime", Params = FunParams };
                var Func = new List<ParamFunction>();
                Func.Add(Fun);
                Params.Add("Func", Func);
            }

            return PublishAsyncClient(msg);
        }

        public MsgRecord GetData(double[] expTime, CalibrationParam param)
        {
            string SerialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var model = ServiceManager.GetInstance().BatchSave(SerialNumber);
            var Params = new Dictionary<string, object>() { };
            MsgSend msg;
            msg = new MsgSend
            {
                EventName = "GetData",
                SerialNumber = SerialNumber,
                Params = Params
            };
            Params.Add("ExpTime", expTime);
            if (param.Id == -1)
            {
                Params.Add("Calibration", new CVTemplateParam() { ID = param.Id,Name = string.Empty });
            }
            else
            {
                Params.Add("Calibration", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            }

            double timeout = 0;
            for (int i = 0; i < expTime.Length; i++) timeout += expTime[i];
            return PublishAsyncClient(msg, timeout + 10000);
        }  
        public MsgRecord GetData_Old(double expTime, double gain)
        {
            string SerialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var model = ServiceManager.GetInstance().BatchSave(SerialNumber);

            MsgSend msg;

            if ((!Config.IsExpThree) && (Config.CameraType == CameraType.CV_Q || Config.CameraType == CameraType.MIL_CL))
            {
                List<Dictionary<string, object>> Param = new List<Dictionary<string, object>>();
                foreach (var item in Config.CFW.ChannelCfgs)
                {
                    Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
                    keyValuePairs.Add("eImgChlType", (int)item.Chtype);
                    keyValuePairs.Add("nPort", item.Cfwport);

                    if (item.Chtype == ImageChannelType.Gray_X)
                        keyValuePairs.Add("dExp", Config.ExpTime);
                    if (item.Chtype == ImageChannelType.Gray_Y)
                        keyValuePairs.Add("dExp", Config.ExpTime);
                    if (item.Chtype == ImageChannelType.Gray_Z)
                        keyValuePairs.Add("dExp", Config.ExpTime);

                    Param.Add(keyValuePairs);
                }

                msg = new MsgSend
                {
                    EventName = "GetData",
                    Params = new Dictionary<string, object>() { { "nBatchID", model.Id }, { "Param", Param }, { "gain", gain } }
                };

            }
            else if (Config.IsExpThree)
            {
                List<Dictionary<string,object>> Param = new List<Dictionary<string,object>>();


                foreach (var item in Config.CFW.ChannelCfgs)
                {
                    Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
                    keyValuePairs.Add("eImgChlType", (int)item.Chtype);
                    keyValuePairs.Add("nPort", item.Cfwport);

                    if (item.Chtype == ImageChannelType.Gray_X)
                        keyValuePairs.Add("dExp", Config.ExpTimeR);
                    if (item.Chtype == ImageChannelType.Gray_Y)
                        keyValuePairs.Add("dExp", Config.ExpTimeG);
                    if (item.Chtype == ImageChannelType.Gray_Z)
                        keyValuePairs.Add("dExp", Config.ExpTimeB);

                    Param.Add(keyValuePairs);
                }

                msg = new MsgSend
                {
                    EventName = "GetData",
                    Params = new Dictionary<string, object>() { { "nBatchID", model.Id }, { "Param", Param }, { "gain", gain } }
                };
            }
            else
            {

                List<Dictionary<string, object>> Param = new List<Dictionary<string, object>>();
                Dictionary<string, object> keyValuePairs = new Dictionary<string, object>();
                keyValuePairs.Add("eImgChlType", (int)ImageChannelType.Gray_Y);
                keyValuePairs.Add("nPort", -1);
                keyValuePairs.Add("dExp", expTime);
                Param.Add(keyValuePairs);

                msg = new MsgSend
                {
                    EventName = "GetData",
                    Params = new Dictionary<string, object>() { { "nBatchID", model.Id }, { "Param", Param }, { "gain", gain } }
                };
            }

            return PublishAsyncClient(msg, (Config.IsExpThree? expTime*3 : expTime) + 10000);
        }

        public MsgRecord GetAllCameraID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });

        public MsgRecord AutoFocus()
        {
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new MsgSend
            {
                EventName = "AutoFocus",
                Params = Params
            };

            var tAutoFocusCfg = new Dictionary<string, object>(){
                                { "forwardparam", Config.MotorConfig.AutoFocusConfig.Forwardparam} ,
                                { "curtailparam", Config.MotorConfig.AutoFocusConfig.Curtailparam},
                                { "curStep", Config.MotorConfig.AutoFocusConfig.CurStep},
                                { "stopStep", Config.MotorConfig.AutoFocusConfig.StopStep},
                                { "minPosition", Config.MotorConfig.AutoFocusConfig.MinPosition},
                                { "maxPosition", Config.MotorConfig.AutoFocusConfig.MaxPosition},
                                { "eEvaFunc", Config.MotorConfig.AutoFocusConfig.EvaFunc},
                                { "dMinValue", Config.MotorConfig.AutoFocusConfig.MinValue},
                                { "nTimeout",Config.MotorConfig.AutoFocusConfig.nTimeout}
                            };

            Params.Add("tAutoFocusCfg", tAutoFocusCfg);
            return PublishAsyncClient(msg, Config.MotorConfig.AutoFocusConfig.nTimeout);
        }

        public MsgRecord GetAutoExpTime()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetAutoExpTime",
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord SetLicense(string md5, string FileData)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SaveLicense",
                Params = new Dictionary<string, object>() { { "FileName", md5 }, { "FileData", FileData }, { "eType", 0 }}
            };

            return PublishAsyncClient(msg); 
        }

        public MsgRecord Close()
        {
            IsVideoOpen = false;
            MsgSend msg = new MsgSend {  EventName = "Close" };
            return PublishAsyncClient(msg);
        }


        public MsgRecord Move(int nPosition, bool IsbAbs = true, int dwTimeOut = 5000)
        {

            MsgSend msg = new MsgSend
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

            MsgSend msg = new MsgSend
            {
                EventName = "MoveDiaphragm",
                Params = new Dictionary<string, object>() { { "dPosition", dPosition }, { "dwTimeOut", Config.MotorConfig.DwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }




        public MsgRecord GoHome()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GoHome",
                Params = new Dictionary<string, object>() { { "dwTimeOut", Config.MotorConfig.DwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetPosition()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetPosition",
                Params = new Dictionary<string, object>() { { "dwTimeOut", Config.MotorConfig.DwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }

        public void DownloadFile(string fileName, FileExtType extType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_Download,
                //ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", extType } }
            };
            PublishAsyncClient(msg);
        }

        public MsgRecord UploadCalibrationFile(string name, string fileName,int fileType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_Upload,
                Params = new Dictionary<string, object> { { "Name", name }, { "FileName", fileName }, { "FileExtType", FileExtType.Calibration } }
            };
             return PublishAsyncClient(msg);
        }

        public async Task<MsgRecord> UploadCalibrationFileAsync(string name, string fileName, int fileType, int timeout = 50000)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start(); // 开始计时

            TaskCompletionSource<MsgRecord> tcs = new TaskCompletionSource<MsgRecord>();
            string md5 = Tool.CalculateMD5(fileName);
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_Upload,
                Params = new Dictionary<string, object> { { "Name", name }, { "FileName", fileName }, { "FileExtType", FileExtType.Calibration } ,{"MD5", md5 } }
            };
            MsgRecord msgRecord = PublishAsyncClient(msg);

            MsgRecordStateChangedHandler handler = (sender) =>
            {
                log.Info($"UploadCalibrationFileAsync:{fileName}  状态{sender}  Operation time: {stopwatch.ElapsedMilliseconds} ms");
                tcs.TrySetResult(msgRecord); 
            };
            msgRecord.MsgRecordStateChanged += handler;
            var timeoutTask = Task.Delay(timeout);
            try
            {

                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    log.Info($"UploadCalibrationFileAsync:{fileName}  超时  Operation time: {stopwatch.ElapsedMilliseconds} ms");
                    tcs.TrySetException(new TimeoutException("The operation has timed out."));
                }
                return await tcs.Task; // 如果超时，这里将会抛出异常
            }
            catch (Exception ex)
            {
                log.Info($"UploadCalibrationFileAsync:{fileName}  异常 {ex.Message} Operation time: {stopwatch.ElapsedMilliseconds} ms");
                tcs.TrySetException(ex);
                return await tcs.Task; // 
            }
            finally
            {
                msgRecord.MsgRecordStateChanged -= handler;
            }
        }
        public MsgRecord CacheClear()
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTCameraEventEnum.Event_Delete_Data,
                Params = new Dictionary<string, object> { }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetChannel(int recId, CVImageChannelType chType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTCameraEventEnum.Event_GetData_Channel,
                Params = new Dictionary<string, object> { { "RecID", recId }, { "ChannelType", chType } }
            };
            return PublishAsyncClient(msg);
        }
    }
}
