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
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.MQTT
{
    /// <summary>
    /// 基础硬件配置信息
    /// </summary>
    public class BaseDeviceConfig : ViewModelBase
    {
        /// <summary>
        /// 序号
        /// </summary>
        public int No { get => _No; set { _No = value; NotifyPropertyChanged(); } }
        private int _No;

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        /// <summary>
        /// 是否存活
        /// </summary>
        public bool IsAlive { get => _IsAlive; set { _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;


        /// <summary>
        /// 设备序号
        /// </summary>
        public string ID { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }
        private string _ID;

        public string MD5 { get => _MD5; set { _MD5 = value; NotifyPropertyChanged(); } }
        private string _MD5;

        public bool IsRegister { get => _IsRegister; set { _IsRegister = value; NotifyPropertyChanged(); } }
        private bool _IsRegister;

    }

    public class ServiceConfig: ViewModelBase,IMQTTServiceConfig,IHeartbeat
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 服务类型
        /// </summary>
        public string Type { get; set; }

        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        public bool IsAlive { get => _IsAlive; set { if (value == _IsAlive) return; _IsAlive = value; NotifyPropertyChanged(); } }
        private bool _IsAlive;
        public  DateTime LastAliveTime { get => _LastAliveTime; set { _LastAliveTime = value; NotifyPropertyChanged(); } }
        private DateTime _LastAliveTime = DateTime.MinValue;
    }


    public class SpectrumConfig :BaseDeviceConfig, IMQTTServiceConfig
    {
        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }
    }


    /// <summary>
    /// 相机配置
    /// </summary>
    public class CameraConfig: BaseDeviceConfig, IMQTTServiceConfig
    {

        public CameraConfig()
        {
        }

        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        public CameraType CameraType { get => _CameraType; set { _CameraType = value; NotifyPropertyChanged(); } }
        private CameraType _CameraType;


        public TakeImageMode TakeImageMode { get => _TakeImageMode; set { _TakeImageMode = value; NotifyPropertyChanged(); } }
        private TakeImageMode _TakeImageMode;

        public int ImageBpp { get => _ImageBpp; set { _ImageBpp = value; NotifyPropertyChanged(); } }
        private int _ImageBpp;
        public int Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); } }
        private int _Channel;


    }





    public delegate void MQTTCameraFileHandler(object sender,string? FilePath);
    public delegate void MQTTCameraMsgHandler(object sender, MsgReturn msg);


    public class MQTTCamera :BaseService
    {
        public event MQTTCameraFileHandler FileHandler;
        public event MQTTCameraMsgHandler InitCameraSuccess;
        public event MQTTCameraMsgHandler OpenCameraSuccess;
        public event MQTTCameraMsgHandler CloseCameraSuccess;
        public event MQTTCameraMsgHandler UnInitCameraSuccess;

        public static List<string> MD5 { get; set; } = new List<string>();
        public static List<string> CameraIDs { get; set; } = new List<string>();

        public CameraConfig Config { get; set; }
        public string DeviceID { get => Config.ID; }

        public MQTTCamera(string SendTopic = "Camera/CMD/0", string SubscribeTopic = "Camera/STATUS/0") : base()
        {
            Config = new CameraConfig();
            Config.SendTopic = SendTopic;
            Config.SubscribeTopic = SubscribeTopic;
            this.SendTopic = SendTopic;
            this.SubscribeTopic = SubscribeTopic;
            MQTTControl.SubscribeCache(SubscribeTopic);
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;

            this.Connected += (s, e) =>
            {
                GetAllCameraID();
            };
        }

        public MQTTCamera(CameraConfig CameraConfig)
        {
            this.Config = CameraConfig;
            this.SendTopic = CameraConfig.SendTopic;
            this.SubscribeTopic = CameraConfig.SubscribeTopic;
            MQTTControl.SubscribeCache(SubscribeTopic);
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
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
                case "CM_GetAllCameraID":

                    JArray CameraIDs = msg.Data.CameraID;
                    JArray MD5IDs = msg.Data.MD5ID;
                    for (int i = 0; i < CameraIDs.Count; i++)
                    {
                        if (CameraIDs[i].ToString() ==CameraID)
                            Config.MD5 = MD5IDs[i].ToString();

                        if (!MQTTCamera.CameraIDs.Contains(CameraIDs[i].ToString()))
                                MQTTCamera.CameraIDs.Add(CameraIDs[i].ToString());
                        if (!MD5.Contains(MD5IDs[i].ToString()))
                            MD5.Add(MD5IDs[i].ToString());


                    }
                    return;
            }


            //信息在这里添加一次过滤，让信息只能在对应的相机上显示,同时如果ID为空的话，就默认是服务端的信息，不进行过滤，这里后续在进行优化
            if (Config.ID!=null&&msg.DeviceID != Config.ID)
            {
                return;
            }

            if (msg.Code == 0)
            {

                switch (msg.EventName)
                {
                    case "Init":
                        ServiceID = msg.ServiceID;
                        Application.Current.Dispatcher.Invoke(() => InitCameraSuccess?.Invoke(this,msg));
                        break;
                    case "UnInit":
                        Application.Current.Dispatcher.Invoke(() => UnInitCameraSuccess?.Invoke(this, msg));
                        break;
                    case "SetParam":
                        MessageBox.Show("SetParam");
                        break;
                    case "Close":
                        Application.Current.Dispatcher.Invoke(() => CloseCameraSuccess?.Invoke(this, msg));
                        break;
                    case "Open":
                        Application.Current.Dispatcher.Invoke(() => OpenCameraSuccess?.Invoke(this, msg));
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
                        Application.Current.Dispatcher.Invoke(() => CloseCameraSuccess?.Invoke(this,msg));
                        break;
                    case "Open":
                        MessageBox.Show("Open失败，没有许可证");
                        break;
                    case "Uninit":
                        Application.Current.Dispatcher.Invoke(() => UnInitCameraSuccess?.Invoke(this,msg));
                        break;
                    default:
                        MessageBox.Show($"未定义{msg.EventName}");
                        break;
                }
            }
        }

        public bool IsRun { get; set; }
        public CameraType CurrentCameraType { get; set; }

        public bool Init(CameraType CameraType,string CameraID)
        {
            CurrentCameraType = CameraType;
            this.CameraID = CameraID;
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
                Params = new Dictionary<string, object>() { { "CameraType", (int)CameraType },{ "CameraID", CameraID } }
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

        public bool Calibration(CalibrationParam calibrationParam)
        {

            if (CheckIsRun())
                return false;
            IsRun = false;
            MsgSend msg = new MsgSend
            {
                EventName = "SetParam",
                Params =  new CalibrationParamMQTT(calibrationParam)
            };
            PublishAsyncClient(msg);

            return true;
        }
        public bool Open(string CameraID,TakeImageMode TakeImageMode,int ImageBpp)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Open",
                Params = new Dictionary<string, object>() { { "TakeImageMode", (int)TakeImageMode }, { "CameraID", CameraID }, { "Bpp", ImageBpp } }
            };
            PublishAsyncClient(msg);
            return true;
        }
         
        public bool GetData(double expTime,double gain,string saveFileName = "1.tif")
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
                EventName = "CM_GetAllCameraID",
                Params = new Dictionary<string, object>() { { "CameraID", "" }, { "eType", 0 } }
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

        public bool SetLicense(string md5,string FileData)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SaveLicense",
                Params = new Dictionary<string, object>() { { "eType", 0}, { "FileName", md5 }, { "FileData", FileData } }
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
                this.Luminance = SetPath(item.SelectedLuminance, item.FileNameLuminance);
                this.LumOneColor = SetPath(item.SelectedColorOne, item.FileNameColorOne);
                this.LumFourColor = SetPath(item.SelectedColorFour, item.FileNameColorFour);
                this.LumMultiColor = SetPath(item.SelectedColorMulti, item.FileNameColorMulti);
                this.DarkNoise = SetPath(item.SelectedDarkNoise, item.FileNameDarkNoise);
                this.DSNU = SetPath(item.SelectedDSNU, item.FileNameDSNU);
                this.Distortion = SetPath(item.SelectedDistortion, item.FileNameDistortion);
                this.DefectWPoint = SetPath(item.SelectedDefectWPoint, item.FileNameDefectWPoint);
                this.DefectBPoint = SetPath(item.SelectedDefectBPoint, item.FileNameDefectBPoint);
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


    public enum CameraType
    {
        [Description("CV_Q")]
        CVQ,
        [Description("LV_Q")]
        LVQ,
        [Description("BV_Q")]
        BVQ,
        [Description("MIL_CL")]
        MILCL,
        [Description("MIL_CXP")]
        MILCXP,
        [Description("BV_H")]
        BVH,
        [Description("LV_H")]
        LVH,
        [Description("HK_CXP")]
        HKCXP,
        [Description("LV_MIL_CL")]
        LVMILCL,
        [Description("MIL_CXP_VIDEO")]
        MILCXPVIDEO,
        [Description("CameraType_Total")]
        CameraTypeTotal,
    };

    public enum TakeImageMode
    {
        [Description("Measure_Normal")]
        Normal = 0,
        [Description("Live")]
        Live,
        [Description("Measure_Fast")]
        Fast,
        [Description("Measure_FastEx")]
        FastExt
    };
}
