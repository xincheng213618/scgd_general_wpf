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


    public class CameraIDList
    {
        [JsonProperty("number")]
        public int Number { get; set; }
        [JsonProperty("ID")]
        public List<string> IDs { get; set; }
    }

    /// <summary>
    /// 基础硬件配置信息
    /// </summary>
    public class BaseHardwareConfig : ViewModelBase
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

    }

    /// <summary>
    /// 相机配置
    /// </summary>
    public class CameraConfig: BaseHardwareConfig
    {
        public CameraConfig()
        { 
        }

        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        public string CameraID { get => _CameraID; set { _CameraID = value;  NotifyPropertyChanged(); } }
        private string _CameraID;
        public CameraType CameraType { get; set; }

        public TakeImageMode TakeImageMode { get; set; }

        public int ImageBpp { get; set; }

        public string MD5 { get; set; }
    }

    public delegate void MQTTCameraFileHandler(string? FilePath);



    public class MQTTCamera :BaseService
    {
        public event MQTTCameraFileHandler FileHandler;
        public event MsgReturnHandler InitCameraSuccess;
        public event MsgReturnHandler OpenCameraSuccess;
        public event MsgReturnHandler CloseCameraSuccess;
        public event MsgReturnHandler UnInitCameraSuccess;

        public CameraConfig CameraConfig { get; set; }

        public CameraIDList? CameraIDList { get; set; }
        public string DeviceID { get => CameraConfig.CameraID; }

        public MQTTCamera(string NickName = "相机1",string SendTopic = "Camera/CMD/0", string SubscribeTopic = "Camera/STATUS/0") : base()
        {
            CameraConfig = new CameraConfig();
            CameraConfig.SendTopic = SendTopic;
            CameraConfig.SubscribeTopic = SubscribeTopic;

            this.NickName = NickName;
            this.SendTopic = SendTopic;
            this.SubscribeTopic = SubscribeTopic;
            MQTTControl.SubscribeCache(SubscribeTopic);
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
        }

        public MQTTCamera(CameraConfig CameraConfig)
        {
            this.CameraConfig = CameraConfig;
            this.SendTopic = CameraConfig.SendTopic;
            this.SubscribeTopic = CameraConfig.SubscribeTopic;
            MQTTControl.SubscribeCache(SubscribeTopic);
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
        }

        public List<string> MD5 { get; set; } = new List<string>();

        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            IsRun = false;
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    case "CM_GetAllCameraID":
                        JArray CameraID = msg.Data.CameraID;
                        JArray MD5ID = msg.Data.MD5ID;
                        foreach (var item in MD5ID)
                        {
                            if (!MD5.Contains(item.ToString()))
                            {
                                MD5.Add(item.ToString());
                            }
                        }
                        //var CameraIDList = JsonConvert.DeserializeObject<cameralince>(CameraMD5);
                        break;
                    case "Init":
                        string CameraId = msg.Data.CameraId;
                        ServiceID = msg.ServiceID;
                        CameraIDList = JsonConvert.DeserializeObject<CameraIDList>(CameraId);
                        Application.Current.Dispatcher.Invoke(() => InitCameraSuccess.Invoke(msg));
                        break;
                    case "Uninit":
                        Application.Current.Dispatcher.Invoke(() => UnInitCameraSuccess.Invoke(msg));
                        break;
                    case "SetParam":
                        MessageBox.Show("SetParam");
                        break;
                    case "Close":
                        Application.Current.Dispatcher.Invoke(() => CloseCameraSuccess.Invoke(msg));
                        break;
                    case "Open":
                        Application.Current.Dispatcher.Invoke(() => OpenCameraSuccess.Invoke(msg));
                        break;
                    case "GatData":
                        string Filepath = msg.Data.FilePath;
                        Application.Current.Dispatcher.Invoke(() => FileHandler?.Invoke(Filepath));
                        break;
                    case "GetAutoExpTime":
                        break;
                    default:
                        MessageBox.Show("未定义数据");
                        break;
                }
            }
            else
            {
                switch (msg.EventName)
                {
                    case "GatData":
                        MessageBox.Show("取图失败");
                        break;
                    case "Close":
                        Application.Current.Dispatcher.Invoke(() => CloseCameraSuccess.Invoke(msg));
                        break;
                    case "Open":
                        MessageBox.Show("没有许可证");
                        break;
                    case "Uninit":
                        Application.Current.Dispatcher.Invoke(() => UnInitCameraSuccess.Invoke(msg));
                        break;
                    default:
                        MessageBox.Show("相机操作失败");
                        break;
                }
            }
        }

        public bool IsRun { get; set; }
        public CameraType CurrentCameraType { get; set; }

        public bool Init(CameraType CameraType)
        {
            CurrentCameraType = CameraType;
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
                Params = new Dictionary<string, object>() { { "CameraType", (int)CameraType } }
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
            this.CameraID = CameraID;
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

        public bool GetAllLicense()
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
            public CalibrationParamMQTT(CalibrationParam calibrationParam)
            {
                this.Luminance = SetPath(calibrationParam.SelectedLuminance, calibrationParam.FileNameLuminance);
                this.LumOneColor = SetPath(calibrationParam.SelectedColorOne, calibrationParam.FileNameColorOne);
                this.LumFourColor = SetPath(calibrationParam.SelectedColorFour, calibrationParam.FileNameColorFour);
                this.LumMultiColor = SetPath(calibrationParam.SelectedColorMulti, calibrationParam.FileNameColorMulti);
                this.DarkNoise = SetPath(calibrationParam.SelectedDarkNoise, calibrationParam.FileNameDarkNoise);
                this.DSNU = SetPath(calibrationParam.SelectedDSNU, calibrationParam.FileNameDSNU);
                this.Distortion = SetPath(calibrationParam.SelectedDistortion, calibrationParam.FileNameDistortion);
                this.DefectWPoint = SetPath(calibrationParam.SelectedDefectWPoint, calibrationParam.FileNameDefectWPoint);
                this.DefectBPoint = SetPath(calibrationParam.SelectedDefectBPoint, calibrationParam.FileNameDefectBPoint);
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
