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


    public class CameraId
    {
        [JsonProperty("number")]
        public int Number { get; set; }
        [JsonProperty("ID")]
        public List<string> IDs { get; set; }
    }

    public delegate void MQTTCameraFileHandler(string? FilePath);

    public class MQTTCamera :BaseService
    {
        public event MQTTCameraFileHandler FileHandler;
        public event EventHandler InitCameraSuccess;
        public event EventHandler OpenCameraSuccess;
        public event EventHandler CloseCameraSuccess;

        public CameraId? CameraIDs { get; set; }

        public MQTTCamera(string NickName = "相机1",string SendTopic = "Camera",string SubscribeTopic = "CameraService") : base()
        {
            this.NickName = NickName;
            this.SendTopic = SendTopic;
            this.SubscribeTopic = SubscribeTopic;
            MQTTControl.SubscribeCache(SubscribeTopic);
            MsgReturnChanged += MQTTCamera_MsgReturnChanged;
            GetAllLicense();
        }
        public List<string> MD5 { get; set; } = new List<string>();


        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            IsRun = false;
            if (msg.Code == 0)
            {
                if (msg.EventName == "CM_GetAllCameraID")
                {
                    JArray CameraID = msg.Data.CameraID;
                    JArray MD5ID = msg.Data.MD5ID;
                    foreach (var item in MD5ID)
                    {
                        MD5.Add(item.ToString());
                    }
                    //var CameraIDs = JsonConvert.DeserializeObject<cameralince>(CameraMD5);
                }


                if (msg.EventName == "Init")
                {
                    string CameraId = msg.Data.CameraId;
                    ServiceID = msg.ServiceID;
                    CameraIDs = JsonConvert.DeserializeObject<CameraId>(CameraId);
                    Application.Current.Dispatcher.Invoke(() => InitCameraSuccess.Invoke(this, new EventArgs()));
                }
                else if (msg.EventName == "SetParam")
                {
                    MessageBox.Show("SetParam");
                }
                else if (msg.EventName == "Open")
                {
                    Application.Current.Dispatcher.Invoke(() => OpenCameraSuccess.Invoke(this, new EventArgs()));
                }
                else if (msg.EventName == "GatData")
                {
                    string Filepath = msg.Data.FilePath;
                    Application.Current.Dispatcher.Invoke(() => FileHandler?.Invoke(Filepath));
                }
                else if (msg.EventName == "Close")
                {
                    Application.Current.Dispatcher.Invoke(() => CloseCameraSuccess.Invoke(this, new EventArgs()));
                }
                else if (msg.EventName == "Uninit")
                {
                    MessageBox.Show("Uninit");
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
                        MessageBox.Show("关闭相机失败");
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
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
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
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
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
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }

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
