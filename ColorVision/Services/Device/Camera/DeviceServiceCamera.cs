#pragma warning disable CS8602  

using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.Services;
using ColorVision.Services.Device.Camera;
using ColorVision.Services.Msg;
using ColorVision.Templates;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Windows;
using ColorVision.Extension;

namespace ColorVision.Device.Camera
{

    public delegate void MQTTCameraFileHandler(object sender, string? FilePath);
    public delegate void MQTTCameraMsgHandler(object sender, MsgReturn msg);




    public class DeviceServiceCamera : BaseDevService<ConfigCamera>
    {
        public event MQTTCameraFileHandler FileHandler;

        public ServiceCamera CameraService { get; set; }

        public bool IsOnlie { get => CameraService.DevicesSN.Contains(Config.ID); }
        public override bool IsAlive { get =>
                Config.IsAlive && IsOnlie; set { 
                Config.IsAlive = (value && IsOnlie); NotifyPropertyChanged(); } }

        public DeviceServiceCamera(ConfigCamera CameraConfig, ServiceCamera cameraService) : base(CameraConfig)
        {
            CameraService = cameraService;
            CameraService.Devices.Add(this);
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatus.UnInit;
            DisConnected += (s, e) =>
            {  
                DeviceStatus = DeviceStatus.UnInit;
            };

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
                    case "Init":
                        DeviceStatus = DeviceStatus.Init;
                        SetCfg(ConfigType.Camera);
                        SetCfg(ConfigType.ExpTime);
                        break;
                    case "UnInit":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "SetParam":
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    case "Open":
                        DeviceStatus = DeviceStatus.Opened;
                        break;
                    case "GetData":
                        try
                        {
                            string SaveFileName = msg.Data.list[0].filename;
                            Application.Current.Dispatcher.Invoke(() => FileHandler?.Invoke(this, SaveFileName));
                        }
                        catch { }

                        break;
                    case "GetAutoExpTime":
                        if (msg.Data != null && msg.Data.result[0].result != null)
                        {
                            if (Config.IsExpThree)
                            {
                                for (int i = 0; i < 3; i++)
                                {
                                    if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_X)
                                    {
                                        Config.ExpTimeR = msg.Data.result[i].result;
                                        Config.SaturationR = msg.Data.result[i].resultSaturation;
                                    }
                                    if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_Y)
                                    {
                                        Config.ExpTimeG = msg.Data.result[i].result;
                                        Config.SaturationG = msg.Data.result[i].resultSaturation;
                                    }

                                    if (Config.CFW.ChannelCfgs[i].Chtype == ImageChannelType.Gray_Z)
                                    {
                                        Config.ExpTimeB = msg.Data.result[i].result;
                                        Config.SaturationB = msg.Data.result[i].resultSaturation;
                                    }
                                }

                                string Msg = "SaturationR:" + Config.SaturationR.ToString() + Environment.NewLine +
                                             "SaturationG:" + Config.SaturationG.ToString() + Environment.NewLine +
                                             "SaturationB:" + Config.SaturationB.ToString() + Environment.NewLine;

                                Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, Msg));
                            }
                            else
                            {
                                Config.ExpTime = msg.Data.result[0].result;
                                Config.Saturation = msg.Data.result[0].resultSaturation;

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
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, $"未定义{msg.EventName}"));
                        break;
                }
            }
            else
            {
                switch (msg.EventName)
                {
                    case "GetData":
                        //string SaveFileName = msg.Data.SaveFileName;
                        //Application.Current.Dispatcher.Invoke(() => FileHandler?.Invoke(this, SaveFileName));
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "Open":
                        if (DeviceStatus == DeviceStatus.Init)
                            Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, "许可证异常，请配置相机设备许可证"));
                        DeviceStatus = DeviceStatus.Init;
                        break;
                    case "Init":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "UnInit":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "Calibrations":
                        break;
                    default:
                        //DeviceStatus = DeviceStatus.UnInit;
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
            var Params = new Dictionary<string, object>() { { "CameraType", (int)Config.CameraType }, { "SnID", Config.SNID }, { "CodeID", Config.Code }, { "szCfgName", "" } };
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
            MsgSend msg = new MsgSend  {  EventName = "UnInit", };
            return PublishAsyncClient(msg);
        }

        public MsgRecord SetCfg(ConfigType configType)
        {
            MsgSend msg = new MsgSend { EventName = "SetCfg" };

            var Params = new Dictionary<string,object>();
            Params.Add("ConfigType", configType);
            switch (configType)
            {   
                case ConfigType.Camera:
                    Params.Add("jsonCfg", Config.CameraCfg.ToJsonN());
                    break;
                case ConfigType.ExpTime:
                    Params.Add("jsonCfg", Config.ExpTimeCfg.ToJsonN());
                    break;
                case ConfigType.Calibration:
                    Params.Add("jsonCfg", Config.ExpTimeCfg);
                    break;
                case ConfigType.Channels:
                    Params.Add("jsonCfg", Config.CFW.ChannelCfgs.ToJsonN());
                    break;
                case ConfigType.SYSTEM:
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
                item.NormalR.ToDictionary(),
                item.NormalG.ToDictionary(),
                item.NormalB.ToDictionary()
            };
            Params.Add("List", List);
            return PublishAsyncClient(msg);
        }

        public void OpenVideo()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "OpenVideo",
                Params = new Dictionary<string, object>() { }
            };
            PublishAsyncClient(msg);
        }

        public MsgRecord Open(string CameraID, TakeImageMode TakeImageMode, int ImageBpp)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { { "TakeImageMode", (int)TakeImageMode }, { "CameraID", CameraID }, { "Bpp", ImageBpp },{ "remoteIp", Config.VideoConfig.Host },{ "remotePort", Config.VideoConfig.Port } }
            };

            return PublishAsyncClient(msg);
        }

        public MsgRecord GetData(double expTime, double gain)
        {
            string SerialNumber  = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var model = ServiceManager.GetInstance().BatchSave(SerialNumber);

            MsgSend msg;
            if (Config.IsExpThree)
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
            MsgSend msg = new MsgSend
            {
                EventName = "AutoFocus",
                Params = new Dictionary<string, object>() {   }
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

            var Params = new Dictionary<string, object>() { };
            Params.Add("tAutoFocusCfg", tAutoFocusCfg);
            msg.Params.Add("params", Params);
            return PublishAsyncClient(msg, Config.MotorConfig.AutoFocusConfig.nTimeout);
        }

        public MsgRecord GetAutoExpTime()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetAutoExpTime",
                Params = new Dictionary<string, object>() {
                    {
                        "SetCfwport", new List<Dictionary<string, object>>()
                        {
                            new Dictionary<string, object>() {
                                { "nIndex",0},{ "nPort",Config.CFW.ChannelCfgs[0].Cfwport},{"eImgChlType",(int)Config.CFW.ChannelCfgs[0].Chtype }
                            },
                            new Dictionary<string, object>() {
                                { "nIndex",1},{ "nPort",Config.CFW.ChannelCfgs[1].Cfwport},{"eImgChlType",(int)Config.CFW.ChannelCfgs[1].Chtype }
                            },
                            new Dictionary<string, object>() {
                                { "nIndex",2},{ "nPort",Config.CFW.ChannelCfgs[2].Cfwport},{"eImgChlType",(int)Config.CFW.ChannelCfgs[2].Chtype }
                            },
                        }
                    }
                }
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

    }



}
