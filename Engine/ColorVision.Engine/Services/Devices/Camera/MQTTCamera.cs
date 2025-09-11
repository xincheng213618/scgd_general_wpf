﻿#pragma warning disable CS8602,CA1707
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoExpTimeParam;
using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Themes.Controls;
using cvColorVision;
using CVCommCore;
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
            //string Msg = "{\"Data\":{\"Position\":1800,\"ImageTmpFile\":\"D:\\\\CVTest\\\\DEV.Camera.Default\\\\Data\\\\2025-03-26\\\\AutoFocus_.4425280117077446656_4.cvraw\"},\"Code\":102,\"Message\":\"Pending\",\"Version\":\"1.0\",\"ServiceName\":\"RC_local/Camera/SVR.Camera.Default/CMD\",\"DeviceCode\":\"DEV.Camera.Default\",\"EventName\":\"AutoFocus\",\"SerialNumber\":\"\",\"MsgID\":\"5ee93a80-af4c-4621-9dc6-2fb0584c523b\",\"ZIndex\":-1}";
            //msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);
            //信息在这里添加一次过滤，让信息只能在对应的相机上显示,同时如果ID为空的话，就默认是服务端的信息，不进行过滤，这里后续在进行优化

            //string Msg = "{\"Data\":{\"nPosition\":2311,\"VidPos\":-1127.522865999267},\"Code\":0,\"Message\":\"ok\",\"Version\":\"1.0\",\"ServiceName\":\"RC_local/Camera/SVR.Camera.Default/CMD\",\"DeviceCode\":\"DEV.Camera.Default\",\"EventName\":\"GetPosition\",\"SerialNumber\":\"\",\"MsgID\":\"1c364974-45c4-4e2f-8071-10b3d898e8af\",\"ZIndex\":-1}";
            //msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);

            //string Msg = "{\"Version\":\"1.0\",\"EventName\":\"AutoFocus\",\"ServiceName\":\"RC_local/Camera/SVR.Camera.Default/CMD\",\"DeviceName\":null,\"DeviceCode\":\"DEV.Camera.Default1\",\"SerialNumber\":\"\",\"Code\":0,\"MsgID\":\"ac152c4d-9501-49fe-b082-03110dc26479\",\"data\":{\"Position\":157909,\"VidPosition\":111.15504121214114,\"TotalTime\":23759,\"MasterId\":54,\"MasterResultType\":108,\"MasterValue\":null}}";
            //msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);

            //string Msg = "{\"Version\":\"1.0\",\"EventName\":\"GetAutoExpTime\",\"ServiceName\":\"RC_local/Camera/SVR.Camera.Default/CMD\",\"DeviceName\":null,\"DeviceCode\":\"DEV.Camera.Default\",\"SerialNumber\":null,\"Code\":0,\"MsgID\":\"21ea4913-3f22-42d7-9946-d85e5b05f2d8\",\"data\":{\"ExpTime\":[{\"result\":28.9528713,\"resultSaturation\":0.0},{\"result\":216.322388,\"resultSaturation\":0.0},{\"result\":2382.22241,\"resultSaturation\":0.0}],\"NDPort\":2}}";
            //msg = JsonConvert.DeserializeObject<MsgReturn>(Msg);

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
                        if (msg.Data == null) break;
                        try
                        {
                            if (msg.Data[0].result != null)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    if (Config.IsExpThree)
                                    {
                                        for (int i = 0; i < 3; i++)
                                        {
                                            if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_X)
                                            {
                                                Config.ExpTimeR = msg.Data[i].result;
                                                Config.SaturationR = msg.Data[i].resultSaturation;
                                            }
                                            if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_Y)
                                            {
                                                Config.ExpTimeG = msg.Data[i].result;
                                                Config.SaturationG = msg.Data[i].resultSaturation;
                                            }

                                            if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_Z)
                                            {
                                                Config.ExpTimeB = msg.Data[i].result;
                                                Config.SaturationB = msg.Data[i].resultSaturation;
                                            }
                                        }

                                        string Msg = $"SaturationR:{Config.SaturationR}  ExpTime:{Config.ExpTimeR}" + Environment.NewLine +
                                                     $"SaturationG:{Config.SaturationG}  ExpTime:{Config.ExpTimeG}" + Environment.NewLine +
                                                     $"SaturationB:{Config.SaturationB}  ExpTime:{Config.ExpTimeB}" + Environment.NewLine;
                                        MessageBox1.Show(Application.Current.GetActiveWindow(), Msg);
                                    }
                                    else
                                    {
                                        Config.ExpTime = msg.Data[0].result;
                                        Config.Saturation = msg.Data[0].resultSaturation;

                                        string Msg = $"Saturation:{Config.Saturation}  ExpTime:{Config.ExpTime}";
                                        MessageBox1.Show(Application.Current.GetActiveWindow(), Msg);
                                    }
                                });
                            }
                        }
                        catch(Exception ex)
                        {
                            log.Info("ND高版本支持");
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (Config.IsExpThree)
                                {
                                    for (int i = 0; i < 3; i++)
                                    {
                                        if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_X)
                                        {
                                            Config.ExpTimeR = msg.Data.ExpTime[i].result;
                                            Config.SaturationR = msg.Data.ExpTime[i].resultSaturation;
                                        }
                                        if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_Y)
                                        {
                                            Config.ExpTimeG = msg.Data.ExpTime[i].result;
                                            Config.SaturationG = msg.Data.ExpTime[i].resultSaturation;
                                        }

                                        if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_Z)
                                        {
                                            Config.ExpTimeB = msg.Data.ExpTime[i].result;
                                            Config.SaturationB = msg.Data.ExpTime[i].resultSaturation;
                                        }
                                    }

                                    string Msg = $"SaturationR:{Config.SaturationR}  ExpTime:{Config.ExpTimeR}" + Environment.NewLine +
                                                 $"SaturationG:{Config.SaturationG}  ExpTime:{Config.ExpTimeG}" + Environment.NewLine +
                                                 $"SaturationB:{Config.SaturationB}  ExpTime:{Config.ExpTimeB}" + Environment.NewLine;
                                    MessageBox1.Show(Application.Current.GetActiveWindow(), Msg);
                                }
                                else
                                {
                                    Config.ExpTime = msg.Data.ExpTime[0].result;
                                    Config.Saturation = msg.Data.ExpTime[0].resultSaturation;

                                    string Msg = $"Saturation:{Config.Saturation}  ExpTime:{Config.ExpTime}";
                                    MessageBox1.Show(Application.Current.GetActiveWindow(), Msg);
                                }
                                if (msg.Data.NDPort != null)
                                {
                                    Config.NDPort = msg.Data.NDPort;
                                }
                            });
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
                            MessageBox1.Show(WindowHelpers.GetActiveWindow(), $"相机打开失败，找不到激活文件,设备码{msg.DeviceCode} {Environment.NewLine} 请粘贴到SN到指定位置:{SN} ","ColorVision");
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
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox1.Show(Application.Current.MainWindow, "打开失败", "ColorVision"));
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
        public bool IsVideoOpen { get => _IsVideoOpen; set { _IsVideoOpen = value;OnPropertyChanged();
                if (value)
                {
                    Application.Current.MainWindow.Closing += (s, e) =>
                    {
                        if (DeviceStatus == DeviceStatusType.LiveOpened)
                        {
                            Close();
                        }
                    };
                }
            } }
        private bool _IsVideoOpen;




        public MsgRecord OpenVideo(string host, int port)
        {
            CurrentTakeImageMode = TakeImageMode.Live;
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
            var Func = new List<ParamFunction>();
            if (Config.IsExpThree && !IsVideoOpen)
            {
                var FunParams = new Dictionary<string, object>() { };
                FunParams.Add("nIndex", 0);
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

                foreach (var item in FunParamss)
                {
                    var Fun = new ParamFunction() { Name = "CM_SetExpTimeEx", Params = item };
                    Func.Add(Fun);
                }
                Func.Add(new ParamFunction() { Name = "CM_SetGain", Params = new Dictionary<string, object>() { { "Gain", Config.Gain } } });
            }
            else
            {
                var FunParams = new Dictionary<string, object>() { };
                FunParams.Add("dExp", Config.ExpTime);
                var Fun = new ParamFunction() { Name = "CM_SetExpTime", Params = FunParams };
                Func.Add(Fun);
                Func.Add(new ParamFunction() { Name = "CM_SetGain", Params = new Dictionary<string, object>() { { "Gain", Config.Gain } } });
            }


            if (Config.CFW.IsUseCFW && Config.CFW.IsNDPort)
            {
                Func.Add(new ParamFunction() { Name = "CM_SetNDPort", Params = new Dictionary<string, object>(){ { "NDPort",Config.NDPort } } });
            }
            Params.Add("Func", Func);
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



        public MsgRecord GetData(double[] expTime, CalibrationParam param, AutoExpTimeParam autoExpTimeParam,ParamBase HDRparamBase)
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
            Params.Add("AvgCount", Config.AvgCount);
            Params.Add("ExpTime", expTime);
            Params.Add("FlipMode", Config.FlipMode);

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
                    Params.Add("AutoExpTimeTemplate", new CVTemplateParam() { ID = autoExpTimeParam.Id, Name = param.Name });
                }
            }

            if (HDRparamBase.Id == -1)
            {
                Params.Add("IsHDR", false);
            }
            else
            {
                Params.Add("IsHDR", true);
                Params.Add("CamParamTemplate", new CVTemplateParam() { ID = HDRparamBase.Id, Name = HDRparamBase.Name });
            }


            Params.Add("ScaleFactor", Config.ScaleFactor);
            Params.Add("Gain", Config.Gain);
            double timeout = 0;
            for (int i = 0; i < expTime.Length; i++) timeout += expTime[i];
            return PublishAsyncClient(msg, timeout + 40000);
        }  


        public MsgRecord GetAllCameraID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });
        public MsgRecord GetCameraID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetSnID" });

        public MsgRecord AutoFocus(AutoFocusParam param)
        {
            var Params = new Dictionary<string, object>() { };

            MsgSend msg = new()
            {
                EventName = "AutoFocus",
                Params = Params
            };
            Params.Add("AutoFocusTemplate", new CVTemplateParam() { ID = param.Id, Name = param.Name });
            return PublishAsyncClient(msg, param.nTimeout);
        }

        public MsgRecord GetAutoExpTime(AutoExpTimeParam autoExpTimeParam)
        {
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

        public MsgRecord ClearDataCache()
        {
            MsgSend msg = new()
            {
                EventName = MQTTCameraEventEnum.Event_Delete_Data,
                Params = new Dictionary<string, object> { }
            };
            return PublishAsyncClient(msg);
        }

    }
}
