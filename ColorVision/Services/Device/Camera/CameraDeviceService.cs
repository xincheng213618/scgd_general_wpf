#pragma warning disable CS8602  

using ColorVision.MQTT;
using ColorVision.Services;
using ColorVision.Services.Device.Camera;
using ColorVision.Services.Msg;
using ColorVision.Templates;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Device.Camera
{

    public delegate void MQTTCameraFileHandler(object sender, string? FilePath);
    public delegate void MQTTCameraMsgHandler(object sender, MsgReturn msg);
    
    public class CameraDeviceService : BaseDevService<CameraConfig>
    {
        public event MQTTCameraFileHandler FileHandler;
        public event DeviceStatusChangedHandler DeviceStatusChanged;

        public DeviceStatus DeviceStatus { get => _DeviceStatus; set { _DeviceStatus = value; Application.Current.Dispatcher.Invoke(() => DeviceStatusChanged?.Invoke(value)); NotifyPropertyChanged(); } }
        private DeviceStatus _DeviceStatus;

        public CameraService CameraService { get; set; }

        public bool IsOnlie { get => CameraService.DevicesSN.Contains(Config.ID); }
        public override bool IsAlive { get =>
                Config.IsAlive && IsOnlie; set { 
                Config.IsAlive = (value && IsOnlie); NotifyPropertyChanged(); } }

        public CameraDeviceService(CameraConfig CameraConfig, CameraService cameraService) : base(CameraConfig)
        {
            CameraService = cameraService;
            CameraService.Devices.Add(this);
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatus.UnInit;
        }
        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            switch (msg.EventName)
            {
                case "CM_GetAllSnID":
                    return;
            }


            //信息在这里添加一次过滤，让信息只能在对应的相机上显示,同时如果ID为空的话，就默认是服务端的信息，不进行过滤，这里后续在进行优化
            if (Config.ID != null && msg.SnID != Config.ID)
            {
                return;
            }

            if (msg.Code == 0)
            {

                switch (msg.EventName)
                {
                    case "Init":
                        //ServiceID = msg.ServiceID;
                        DeviceStatus = DeviceStatus.Init;
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
                            if (Config. IsExpThree)
                            {

                                for (int i = 0; i < 3; i++)
                                {
                                    if (Config.ChannelConfigs[i].ChannelType == ImageChannelType.Gray_X)
                                    {
                                        Config.ExpTimeR = msg.Data.result[i].result;
                                        Config.SaturationR = msg.Data.result[i].resultSaturation;
                                    }
                                    if (Config.ChannelConfigs[i].ChannelType == ImageChannelType.Gray_Y)
                                    {
                                        Config.ExpTimeG = msg.Data.result[i].result;
                                        Config.SaturationG = msg.Data.result[i].resultSaturation;
                                    }

                                    if (Config.ChannelConfigs[i].ChannelType == ImageChannelType.Gray_Z)
                                    {
                                        Config.ExpTimeB = msg.Data.result[i].result;
                                        Config.SaturationB = msg.Data.result[i].resultSaturation;
                                    }
                                }

                                string Msg = "SaturationR:" + Config.SaturationR.ToString() + Environment.NewLine +
                                             "SaturationG:" + Config.SaturationG.ToString() + Environment.NewLine +
                                             "SaturationB:" + Config.SaturationB.ToString() + Environment.NewLine;
                                MessageBox.Show(Msg);


                            }
                            else
                            {
                                Config.ExpTime = msg.Data.result[0].result;
                                Config.Saturation = msg.Data.result[0].resultSaturation;

                                string Msg = "Saturation:" + Config.Saturation.ToString();
                                MessageBox.Show(Msg);
                            }
                        } 
                        break;
                    case "SaveLicense":
                        log.Debug($"SaveLicense:{msg.Data}");
                        break;
                    case "Calibration":
                        log.Debug($"Calibration:{msg.Data}");
                        break;
                    default:
                        MessageBox.Show($"未定义{msg.EventName}");
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
                            MessageBox.Show("许可证异常，请配置相机设备许可证");
                        DeviceStatus = DeviceStatus.UnInit;
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

        public bool Init() => Init(Config.CameraType, Config.ID);

        public bool Init(CameraType CameraType, string CameraID)
        {
            CurrentCameraType = CameraType;
            SnID = CameraID;
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
                Params = new Dictionary<string, object>() { { "CameraType", (int)CameraType }, { "SnID", CameraID }, { "szCfgName", "" } }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public MsgRecord UnInit()
        {
            MsgSend msg = new MsgSend  {  EventName = "UnInit", };
            return PublishAsyncClient(msg);
        }

        public MsgRecord FilterWheelSetPort(int nIndex, int nPort, int eImgChlType)
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

        public bool Open(string CameraID, TakeImageMode TakeImageMode, int ImageBpp)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { { "TakeImageMode", (int)TakeImageMode }, { "CameraID", CameraID }, { "Bpp", ImageBpp },{ "remoteIp", Config.VideoConfig.Host },{ "remotePort", Config.VideoConfig.Port } }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public MsgRecord GetData(double expTime, double gain, CalibrationType eCalibType = CalibrationType.Empty_Num)
        {
            string SerialNumber  = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            var model = ServiceManager.GetInstance().GetResultBatch(SerialNumber);

            MsgSend msg;
            if (Config.IsExpThree)
            {
                List<double> expTimes = new List<double>();

                foreach (var item in Config.ChannelConfigs)
                {
                    if (item.ChannelType == ImageChannelType.Gray_X)
                        expTimes.Add(Config.ExpTimeR);
                    if (item.ChannelType == ImageChannelType.Gray_Y)
                        expTimes.Add(Config.ExpTimeG);
                    if (item.ChannelType == ImageChannelType.Gray_Z)
                        expTimes.Add(Config.ExpTimeB);
                }

                msg = new MsgSend
                {
                    EventName = "GetData",
                    Params = new Dictionary<string, object>() { { "nBatchID", model.Id }, { "expTime", expTimes }, { "gain", gain }, { "eCalibType", eCalibType } }
                };
            }
            else
            {
                msg = new MsgSend
                {
                    EventName = "GetData",
                    Params = new Dictionary<string, object>() { { "nBatchID", model.Id }, { "expTime", expTime }, { "gain", gain }, { "eCalibType", eCalibType } }
                };
            }





            return PublishAsyncClient(msg, expTime + 10000);
        }

        public MsgRecord GetAllCameraID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });


        public MsgRecord AutoFocus()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "AutoFocus",
                Params = new Dictionary<string, object>() { {
                    "Func", new List<ParamFunction>() {
                        new ParamFunction(){ 
                            Name ="CM_InitCOM" ,
                            Params = new Dictionary<string,object>(){
                                { "eFOCUS_COMMUN", Config.MotorConfig.eFOCUSCOMMUN} ,
                                { "szComName", Config.MotorConfig.szComName},
                                { "BaudRate", Config.MotorConfig.BaudRate}
                            }
                        },
                        new ParamFunction()  {
                            Name ="CM_CalcAutoFocus",
                            Params = new Dictionary<string,object>(){
                                { "forwardparam", Config.MotorConfig.AutoFocusConfig.forwardparam} ,
                                { "curtailparam", Config.MotorConfig.AutoFocusConfig.curtailparam},
                                { "curStep", Config.MotorConfig.AutoFocusConfig.curStep},
                                { "stopStep", Config.MotorConfig.AutoFocusConfig.stopStep},
                                { "minPosition", Config.MotorConfig.AutoFocusConfig.minPosition},
                                { "maxPosition", Config.MotorConfig.AutoFocusConfig.maxPosition},
                                { "eEvaFunc", Config.MotorConfig.AutoFocusConfig.eEvaFunc},
                                { "dMinValue", Config.MotorConfig.AutoFocusConfig.dMinValue}
                            }
                        }
                    }
                  }
                }
            };
            return PublishAsyncClient(msg);
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
                                { "nIndex",0},{ "nPort",Config.ChannelConfigs[0].Port},{"eImgChlType",(int)Config.ChannelConfigs[0].ChannelType }
                            },
                            new Dictionary<string, object>() {
                                { "nIndex",1},{ "nPort",Config.ChannelConfigs[1].Port},{"eImgChlType",(int)Config.ChannelConfigs[1].ChannelType }
                            },
                            new Dictionary<string, object>() {
                                { "nIndex",2},{ "nPort",Config.ChannelConfigs[2].Port},{"eImgChlType",(int)Config.ChannelConfigs[2].ChannelType }
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

    }



}
