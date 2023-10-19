#pragma warning disable CS8602  

using ColorVision.Device.SMU;
using ColorVision.Lincense;
using ColorVision.MQTT;
using ColorVision.Services;
using ColorVision.Services.Msg;
using ColorVision.Template;
using cvColorVision;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Device.Camera
{

    public delegate void MQTTCameraFileHandler(object sender, string? FilePath);
    public delegate void MQTTCameraMsgHandler(object sender, MsgReturn msg);
    
    public class CameraServiceConfig : BaseServiceConfig
    {

    }


    public class BaseService<T>: BaseService where T : BaseServiceConfig
    {
        public T Config { get; set; }

        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; } }
        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; } }

        public override int HeartbeatTime { get => Config.HeartbeatTime; set { Config.HeartbeatTime = value; NotifyPropertyChanged(); } }

        public override bool IsAlive { get => Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); } }

        public override DateTime LastAliveTime { get => Config.LastAliveTime; set => Config.LastAliveTime = value; }


        public void UpdateServiceConfig(IServiceConfig config)
        {
            Task.Run(() => MQTTControl.UnsubscribeAsyncClientAsync(Config.SubscribeTopic));

            Config.SendTopic = config.SendTopic;
            Config.SubscribeTopic = config.SubscribeTopic;
            MQTTControl.SubscribeCache(Config.SubscribeTopic);
        }

        public BaseService(T Config) : base()
        {
            this.Config = Config;
            MQTTControl.SubscribeCache(Config.SubscribeTopic);
        }
    }

    public class CameraService : BaseService<CameraServiceConfig>
    {
        public CameraService(CameraServiceConfig Config) :base(Config)
        {
            Devices = new List<CameraDeviceService>();
            GetAllDevice();
            Connected += (s, e) =>
            {
                GetAllDevice();
            };
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
        }
        public List<CameraDeviceService> Devices { get; set; }
        public List<string> DevicesSN { get; set; } = new List<string>();
        public Dictionary<string, string> DevicesSNMD5 { get; set; } = new Dictionary<string, string>();

        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            switch (msg.EventName)
            {
                case "CM_GetAllSnID":
                    try
                    {
                        JArray SnIDs = msg.Data.SnID;
                        if (SnIDs != null)
                        {
                            DevicesSN.Clear();
                            for (int i = 0; i < SnIDs.Count; i++)
                            {
                                DevicesSN.Add(SnIDs[i].ToString());
                            }
                        }

                        JArray MD5IDs = msg.Data.MD5ID;

                        if (SnIDs == null || MD5IDs == null)
                        {
                            return;
                        }

                        for (int i = 0; i < MD5IDs.Count; i++)
                        {
                            DevicesSNMD5.Add(SnIDs[i].ToString(), MD5IDs[i].ToString());
                            LicenseManager.GetInstance().AddLicense(new LicenseConfig() { Name = SnIDs[i].ToString(), Sn = MD5IDs[i].ToString(), IsCanImport = true });
                        }
                    }
                    catch (Exception ex)
                    {
                        if (log.IsErrorEnabled)
                            log.Error(ex);
                    }

                    return;
            }
        }
        public void GetAllDevice()
        {
            PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });
        }
    }


    public class CameraDeviceService : BaseDevService<CameraConfig>
    {
        public event MQTTCameraFileHandler FileHandler;
        public event DeviceStatusChangedHandler DeviceStatusChanged;

        public DeviceStatus DeviceStatus { get => _DeviceStatus; set { _DeviceStatus = value; Application.Current.Dispatcher.Invoke(() => DeviceStatusChanged?.Invoke(value)); NotifyPropertyChanged(); } }
        private DeviceStatus _DeviceStatus;

        public CameraDeviceService(CameraConfig CameraConfig) : base(CameraConfig)
        {
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatus.UnInit;

            Connected += (s, e) =>
            {
                GetAllCameraID();
            };
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
                        ServiceID = msg.ServiceID;
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
                            if (Config.IsExpThree)
                            {
                                Config.ExpTimeR = msg.Data.result[0].result;
                                Config.ExpTimeG = msg.Data.result[1].result;
                                Config.ExpTimeB = msg.Data.result[2].result;

                                Config.SaturationR = msg.Data.result[0].resultSaturation;
                                Config.SaturationG = msg.Data.result[1].resultSaturation;
                                Config.SaturationB = msg.Data.result[2].resultSaturation;

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


        private static List<ParamFunction> Calibrations(CalibrationParam item)
        {
            var param = new List<ParamFunction>() { };

            ImageChannelType eImgChlType = ImageChannelType.Gray_Y;
            param.Add( new ParamFunction(){  Name="CM_InitCalibration" });
            param.Add(new ParamFunction() { Name = "CM_AddChannel",Params=new Dictionary<string, object>() { { "eImgChlType", eImgChlType } } });
            param.Add(SetPath("DarkNoise", item.SelectedDarkNoise, item.FileNameDarkNoise));
            param.Add(SetPath("Luminance", item.SelectedLuminance, item.FileNameLuminance));
            param.Add(SetPath("LumOneColor", item.SelectedColorOne, item.FileNameColorOne));
            param.Add(SetPath("LumFourColor", item.SelectedColorFour, item.FileNameColorFour));
            param.Add(SetPath("LumMultiColor", item.SelectedColorMulti, item.FileNameColorMulti));
            param.Add(SetPath("DSNU", item.SelectedDSNU, item.FileNameDSNU));
            param.Add(SetPath("Distortion", item.SelectedDistortion, item.FileNameDistortion));
            param.Add(SetPath("DefectWPoint", item.SelectedDefectWPoint, item.FileNameDefectWPoint));
            param.Add(SetPath("DefectBPoint", item.SelectedDefectBPoint, item.FileNameDefectBPoint));
            param.Add(SetPath("FileNameUniformityX",item.SelectedUniformityX, item.FileNameUniformityX));
            param.Add(SetPath("FileNameUniformityY",item.SelectedUniformityY, item.FileNameUniformityY));
            param.Add(SetPath("FileNameUniformityZ", item.SelectedUniformityZ, item.FileNameUniformityZ));

            ParamFunction SetPath(string typeName, bool bEnabled, string fileName)
            {
                CalibrationType eCaliType =0;
                switch (typeName)
                {
                    case "DarkNoise":
                        eCaliType = CalibrationType.DarkNoise;
                        break;
                    case "DefectWPoint":
                        eCaliType = CalibrationType.DefectWPoint;
                        break;
                    case "DefectBPoint":
                        eCaliType = CalibrationType.DefectBPoint;
                        break; 
                    case "Luminance":
                        eCaliType = CalibrationType.Luminance;
                        break;
                    case "LumOneColor":
                        eCaliType = CalibrationType.LumOneColor;
                        break;
                    case "LumFourColor":
                        eCaliType = CalibrationType.LumFourColor;
                        break;
                    case "LumMultiColor":
                        eCaliType = CalibrationType.LumMultiColor;
                        break;
                    case "Distortion":
                        eCaliType = CalibrationType.Distortion;
                        break;
                    case "DSNU":
                        eCaliType = CalibrationType.DSNU;
                        break;
                    case "FileNameUniformityX":
                        eCaliType = CalibrationType.Uniformity;
                        break;
                    case "FileNameUniformityY":
                        eCaliType = CalibrationType.Uniformity;
                        break;
                    case "FileNameUniformityZ":
                        eCaliType = CalibrationType.Uniformity;
                        break;
                };
                param.Add(new ParamFunction() { Name = "CM_InsertItem", Params = new Dictionary<string, object>() { { "eImgChlType", eImgChlType }, { "eCaliType", eCaliType } } });
                return new ParamFunction()
                {
                    Name = "CM_SetItemFile",
                    Params = new Dictionary<string, object>() {
                            { "eImgChlType",eImgChlType } ,
                            { "eCaliType", eCaliType } ,
                            { "bEnabled", bEnabled} ,
                            { "filename", fileName }
                        }
                };
            }

            return param;
        }
        public MsgRecord Calibration(CalibrationParam item)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Calibration",
                Params = new Dictionary<string, object>() {
                {
                    "Func", Calibrations(item)
                }   }
            };

            //new ParamFunction() { Name = "CM_AddChannel" },
            //new ParamFunction() { Name = "CM_InitCalibration" },
            //new ParamFunction() { Name = "CM_UnInitCalibration" },
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
            MsgSend msg = new MsgSend
            {
                EventName = "GetData",
                Params = new Dictionary<string, object>() { { "nBatchID", model.Id }, { "expTime", expTime }, { "gain", gain }, { "eCalibType", eCalibType } }
            };
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
