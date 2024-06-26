﻿#pragma warning disable CS8602,CA1707
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Msg;
using ColorVision.Engine.Services.PhyCameras.Group;
using cvColorVision;
using CVCommCore;
using CVCommCore.CVImage;
using MQTTMessageLib;
using MQTTMessageLib.Camera;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera
{

    public class MQTTCamera : MQTTDeviceService<ConfigCamera>
    {
        public override bool IsAlive {get =>  Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); }}

        public MQTTCamera(ConfigCamera CameraConfig) : base(CameraConfig)
        {
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatusType.UnInit;
        }

        public override void Dispose()
        {
            base.Dispose();
            GC.SuppressFinalize(this);     
        }

        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            //信息在这里添加一次过滤，让信息只能在对应的相机上显示,同时如果ID为空的话，就默认是服务端的信息，不进行过滤，这里后续在进行优化
            if (Config.Code != null && msg.DeviceCode != Config.Code) return;
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    case "Close":
                        break;
                    case MQTTCameraEventEnum.Event_Open:
                    case MQTTCameraEventEnum.Event_OpenLive:
                    case MQTTCameraEventEnum.Event_GetData:
                        break;
                    case "GetAutoExpTime":
                        if (msg.Data != null && msg.Data[0].result != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
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
                                    MessageBox.Show(Application.Current.GetActiveWindow(), Msg);
                                }
                                else
                                {
                                    Config.ExpTime = (int)msg.Data[0].result;
                                    Config.Saturation = (int)msg.Data[0].resultSaturation;

                                    string Msg = "Saturation:" + Config.Saturation.ToString();
                                    MessageBox.Show(Application.Current.GetActiveWindow(), Msg);
                                }
                            } );
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
                        break;
                }
            }
            else if(msg.Code == -401)
            {
                switch (msg.EventName)
                {
                    case "Open":
                        DeviceStatus = DeviceStatusType.Closed;
                        string SN = msg.Data.SN;
                        Common.NativeMethods.Clipboard.SetText(SN);
                        Application.Current.Dispatcher.BeginInvoke(() => 
                        {
                            MessageBox.Show(WindowHelpers.GetActiveWindow(), $"相机打开失败，找不到激活文件,设备码{msg.DeviceCode} {Environment.NewLine} 请粘贴到SN到指定位置:{SN} ","ColorVision");
                        });
                        break;
                    default:
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
                        DeviceStatus = DeviceStatusType.Closed;
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, "打开失败", "ColorVision"));
                        break;
                    case "Init":
                        break;
                    case "UnInit":
                        break;
                    default:
                        break;
                }
            }
        }


        public CameraType CurrentCameraType { get; set; }


        public MsgRecord CfwPortSetPort(int nIndex, int nPort, int eImgChlType)
        {
            MsgSend msg = new()
            {
                EventName = "SetParam",
                Params = new Dictionary<string, object>() { { "Func",new List<ParamFunction> (){
                    new() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", nIndex }, { "nPort", nPort },{ "eImgChlType" , eImgChlType } }  } } } }
            };
            return PublishAsyncClient(msg);
        }
        private bool _IsVideoOpen ;
        public bool IsVideoOpen { get => _IsVideoOpen; set { _IsVideoOpen = value;NotifyPropertyChanged(); } }

        public MsgRecord OpenVideo(string host, int port)
        {
            CurrentTakeImageMode = TakeImageMode.Live;
            IsVideoOpen = true;
            bool IsLocal = (host=="127.0.0.1");
            MsgSend msg = new()
            {
                EventName = "OpenLive",
                Params = new Dictionary<string, object>() { { "RemoteIp", host }, { "RemotePort", port }, { "Gain", Config.Gain }, { "ExpTime", Config.ExpTime }, { "IsLocal", IsLocal } }
            };
             return PublishAsyncClient(msg);
        }
        public TakeImageMode CurrentTakeImageMode { get; set; }

        public MsgRecord Open(string CameraID, TakeImageMode TakeImageMode, int ImageBpp)
        {
            CurrentTakeImageMode = TakeImageMode;
            MsgSend msg = new()
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { { "TakeImageMode", (int)TakeImageMode }, { "CameraID", CameraID }, { "Bpp", ImageBpp },{ "remoteIp", Config.VideoConfig.Host },{ "remotePort", Config.VideoConfig.Port } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord SetExp()
        {
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new()
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
                FunParams.Add("Gain", Config.Gain);
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
            Params.Add("ScaleFactor", Config.ScaleFactor);
            double timeout = 0;
            for (int i = 0; i < expTime.Length; i++) timeout += expTime[i];
            return PublishAsyncClient(msg, timeout + 10000);
        }  
        public MsgRecord GetAllCameraID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });

        public MsgRecord AutoFocus()
        {
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new()
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
            MsgSend msg = new()
            {
                EventName = "GetAutoExpTime",
            };
            return PublishAsyncClient(msg);
        }


        public MsgRecord Close()
        {
            MsgSend msg = new() {  EventName = "Close" };
            return PublishAsyncClient(msg);
        }


        public MsgRecord Move(int nPosition, bool IsbAbs = true, int dwTimeOut = 5000)
        {

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

            MsgSend msg = new()
            {
                EventName = "MoveDiaphragm",
                Params = new Dictionary<string, object>() { { "dPosition", dPosition }, { "dwTimeOut", Config.MotorConfig.DwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }




        public MsgRecord GoHome()
        {
            MsgSend msg = new()
            {
                EventName = "GoHome",
                Params = new Dictionary<string, object>() { { "dwTimeOut", Config.MotorConfig.DwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetPosition()
        {
            MsgSend msg = new()
            {
                EventName = "GetPosition",
                Params = new Dictionary<string, object>() { { "dwTimeOut", Config.MotorConfig.DwTimeOut } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord DownloadFile(string fileName, FileExtType extType)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_Download,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", extType } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord CacheClear()
        {
            MsgSend msg = new()
            {
                EventName = MQTTCameraEventEnum.Event_Delete_Data,
                Params = new Dictionary<string, object> { }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetChannel(int recId, CVImageChannelType chType)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_GetChannel,
                Params = new Dictionary<string, object> { { "RecID", recId }, { "ChannelType", chType } }
            };
            return PublishAsyncClient(msg);
        }
    }
}
