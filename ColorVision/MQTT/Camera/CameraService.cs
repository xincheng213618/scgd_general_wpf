using ColorVision.MVVM;
using ColorVision.Template;
using HslCommunication.MQTT;
using MQTTnet.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using ScottPlot.Drawing.Colormaps;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

namespace ColorVision.MQTT.Camera
{

    public delegate void MQTTCameraFileHandler(object sender, string? FilePath);
    public delegate void MQTTCameraMsgHandler(object sender, MsgReturn msg);


    public class CameraService : BaseService<CameraConfig>
    {
        public event MQTTCameraFileHandler FileHandler;

        public event DeviceStatusChangedHandler DeviceStatusChanged;

        public DeviceStatus DeviceStatus { get => _DeviceStatus; set { _DeviceStatus = value;  Application.Current.Dispatcher.Invoke(() => DeviceStatusChanged?.Invoke(value));  NotifyPropertyChanged(); } } 
        private DeviceStatus _DeviceStatus;

        public static List<string> MD5 { get; set; } = new List<string>();
        public static List<string> CameraIDs { get; set; } = new List<string>();

        public static Dictionary<string, ObservableCollection<string>> ServicesDevices { get; set; } = new Dictionary<string, ObservableCollection<string>>();

        public string DeviceID { get => Config.ID; }

        public CameraService(CameraConfig CameraConfig) : base(CameraConfig)
        {
            SendTopic = CameraConfig.SendTopic;
            SubscribeTopic = CameraConfig.SubscribeTopic;
            MQTTControl.SubscribeCache(SubscribeTopic);
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatus.UnInit;

            this.Connected += (s, e) =>
            {
                GetAllCameraID();
            };
        }

       
        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            IsRun = false;
            switch (msg.EventName)
            {
                case "CM_GetAllSnID":
                    try
                    {
                        JArray SnIDs = msg.Data.SnID;
                        JArray MD5IDs = msg.Data.MD5ID;


                        if (SnIDs == null || MD5IDs == null)
                        {
                            return;
                        }

                        for (int i = 0; i < SnIDs.Count; i++)
                        {
                            if (SnIDs[i].ToString() == SnID)
                                Config.MD5 = MD5IDs[i].ToString();

                            if (!CameraIDs.Contains(SnIDs[i].ToString()))
                                CameraIDs.Add(SnIDs[i].ToString());

                            if (!MD5.Contains(MD5IDs[i].ToString()))
                                MD5.Add(MD5IDs[i].ToString());


                            if (ServicesDevices.TryGetValue(SubscribeTopic, out ObservableCollection<string> list) && !list.Contains(SnIDs[i].ToString()))
                            {
                                list.Add(SnIDs[i].ToString());
                            }
                            else
                            {
                                ServicesDevices.Add(SubscribeTopic, new ObservableCollection<string>() { SnIDs[i].ToString() });
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        if (log.IsErrorEnabled)
                            log.Error(ex);
                    }

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
                        DeviceStatus= DeviceStatus.Init;
                        break;
                    case "UnInit":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "SetParam":
                        MessageBox.Show("SetParam");
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    case "Open":
                        DeviceStatus = DeviceStatus.Opened;
                        break;
                    case "GetData":
                        string SaveFileName = msg.Data.SaveFileName;
                        Application.Current.Dispatcher.Invoke(() => FileHandler?.Invoke(this, SaveFileName));
                        break;
                    case "GetAutoExpTime":
                        break;
                    case "SaveLicense":
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
                        string SaveFileName = msg.Data.SaveFileName;
                        Application.Current.Dispatcher.Invoke(() => FileHandler?.Invoke(this, SaveFileName));
                        break;
                    case "Close":
                        break;
                    case "Open":
                        MessageBox.Show("Open失败，没有许可证");
                        break;
                    case "Init":
                        MessageBox.Show("初始化失败，找不到相机"+ Environment.NewLine +"请连接相机或重新初始化服务");
                        break;
                    case "Uninit":
                        MessageBox.Show("关闭相机失败" + Environment.NewLine + "请连接相机或重新初始化服务");
                        break;
                    default:
                        MessageBox.Show($"{msg.EventName}失败");
                        break;
                }
            }
        }

        public bool IsRun { get; set; }
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

        public bool UnInit()
        {
            if (CheckIsRun())
                return false;
            MsgSend msg = new MsgSend
            {
                EventName = "UnInit",
            };
            PublishAsyncClient(msg);
            return true;
        }

        public void FilterWheelSetPort(int nIndex, int nPort, int eImgChlType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                Params = new Dictionary<string, object>() { { "Func",new List<ParamFunction> (){
                    new ParamFunction() { Name = "CM_SetCfwport", Params = new Dictionary<string, object>() { { "nIndex", nIndex }, { "nPort", nPort },{ "eImgChlType" , eImgChlType } }  } } } }
            };
            PublishAsyncClient(msg);
        }

        public bool Calibration()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                Params = new Dictionary<string, object>() {
                {
                    "NameFuc", new List<ParamFunction>()
                    {
                        new ParamFunction(){Name ="CM_InitCalibration" },
                        new ParamFunction(){Name ="CM_UnInitCalibration" },
                        new ParamFunction(){Name ="CM_UnInitCalibration" },
                        new ParamFunction(){Name ="CM_UnInitCalibration" },
                        new ParamFunction(){Name ="CM_UnInitCalibration" },
                        new ParamFunction(){Name ="CM_UnInitCalibration" },
                        new ParamFunction(){Name ="CM_UnInitCalibration" },
                    }
                }
                }
            };

            PublishAsyncClient(msg);
            return true;
        }
        public bool Open(string CameraID, TakeImageMode TakeImageMode, int ImageBpp)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { { "TakeImageMode", (int)TakeImageMode }, { "CameraID", CameraID }, { "Bpp", ImageBpp } }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool GetData(double expTime, double gain, string saveFileName = "1.tif")
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetData",
                Params = new Dictionary<string, object>() { { "expTime", expTime }, { "gain", gain }, { "savefilename", saveFileName } }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool GetAllCameraID()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "CM_GetAllSnID",
            };
            PublishAsyncClient(msg);
            return true;
        }



        public bool SetCfwport()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetAutoExpTime",
                Params = new Dictionary<string, object>() {
                    {
                        "SetCfwport", new List<Dictionary<string, object>>()
                        {
                            new Dictionary<string, object>() {
                                { "nIndex",0},{ "nPort",2},{"eImgChlType",0 }
                            },
                            new Dictionary<string, object>() {
                                { "nIndex",1},{ "nPort",2},{"eImgChlType",0 }
                            },
                            new Dictionary<string, object>() {
                                { "nIndex",2},{ "nPort",2},{"eImgChlType",0 }
                            },
                        }
                    }
                }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool SetLicense(string md5, string FileData)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SaveLicense",
                Params = new Dictionary<string, object>() { { "eType", 0 }, { "FileName", md5 }, { "FileData", FileData } }
            };
            PublishAsyncClient(msg);
            return true;
        }

        public bool Close()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Close"
            };
            PublishAsyncClient(msg);
            return true;
        }


        private bool CheckIsRun()
        {
            if (!MQTTControl.IsConnect)
                return true;

            if (IsRun)
            {
                MessageBox.Show("正在运行中");
                return true;
            }
            IsRun = false;
            return IsRun;
        }



        public class CalibrationParamMQTT : ViewModelBase
        {
            public CalibrationParamMQTT(CalibrationParam item)
            {
                Luminance = SetPath(item.SelectedLuminance, item.FileNameLuminance);
                LumOneColor = SetPath(item.SelectedColorOne, item.FileNameColorOne);
                LumFourColor = SetPath(item.SelectedColorFour, item.FileNameColorFour);
                LumMultiColor = SetPath(item.SelectedColorMulti, item.FileNameColorMulti);
                DarkNoise = SetPath(item.SelectedDarkNoise, item.FileNameDarkNoise);
                DSNU = SetPath(item.SelectedDSNU, item.FileNameDSNU);
                Distortion = SetPath(item.SelectedDistortion, item.FileNameDistortion);
                DefectWPoint = SetPath(item.SelectedDefectWPoint, item.FileNameDefectWPoint);
                DefectBPoint = SetPath(item.SelectedDefectBPoint, item.FileNameDefectBPoint);
            }
            private static string? SetPath(bool Check, string Name)
            {
                return Check && Name != null ? Path.IsPathRooted(Name) ? Name : Environment.CurrentDirectory + "\\" + Name : null;
            }

            public string? Luminance { get; set; }
            [JsonProperty("Uniformity_X")]
            public string? UniformityX { get; set; }
            [JsonProperty("Uniformity_Y")]
            public string? UniformityY { get; set; }
            [JsonProperty("Uniformity_Z")]
            public string? UniformityZ { get; set; }
            public string? LumOneColor { get; set; }
            public string? LumFourColor { get; set; }
            public string? LumMultiColor { get; set; }
            public string? DarkNoise { get; set; }
            public string? DSNU { get; set; }
            public string? Distortion { get; set; }
            public string? DefectWPoint { get; set; }
            public string? DefectBPoint { get; set; }




        }



    }



}
