#pragma warning disable CS4014,CS0618
using ColorVision.MVVM;
using ColorVision.Template;
using MQTTnet.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
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



    public class MQTTCamera : IDisposable
    {

        private static MQTTCamera _instance;
        private static readonly object _locker = new();
        public static MQTTCamera GetInstance() { lock (_locker) { return _instance ??= new MQTTCamera(); } }


        private MQTTControl MQTTControl;

        private string SubscribeTopic;
        private string SendTopic;


        public event MQTTCameraFileHandler FileHandler;

        public event EventHandler InitCameraSucess;

        public CameraId? CameraID { get; set; }

        private MQTTCamera()
        {
            MQTTControl = MQTTControl.GetInstance();
            Task.Run(MQTTControlInit);
        }

        private async void MQTTControlInit()
        {
            if (!MQTTControl.IsConnect)
                await MQTTControl.Connect();
            SendTopic = "Camera";
            SubscribeTopic = "CameraReturn";

            MQTTControl.SubscribeAsyncClient(SubscribeTopic);
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
                try
                {
                    MQTTMsgReturn json = JsonConvert.DeserializeObject<MQTTMsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    IsRun = false;
                    if (!json.Code)
                    {
                        if (json.EventName == "InitCamere")
                        {
                            string CameraId = json.Data.CameraId;
                            CameraID = JsonConvert.DeserializeObject<CameraId>(CameraId);
                            Application.Current.Dispatcher.Invoke(() => InitCameraSucess.Invoke(this, new EventArgs()));
                        }
                        else if (json.EventName == "AddCalibration")
                        {
                            MessageBox.Show("AddCalibration");
                        }
                        else if (json.EventName == "OpenCamere")
                        {
                            MessageBox.Show("OpenCamere");
                        }
                        else if (json.EventName == "GetData")
                        {
                            string Filepath = json.Data.FilePath;
                            Application.Current.Dispatcher.Invoke(() => FileHandler?.Invoke(Filepath));
                        }
                    }
                }
                catch 
                {
                    return Task.CompletedTask;
                }
            }
            IsRun = false;
            return Task.CompletedTask;
        }

        public bool IsRun { get; set; }

        public bool InitCamera(CameraType CameraType)
        {
            if (CheckIsRun())
                return false;
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "InitCamere",
                Params = new InitCameraParamMQTT() { CameraType = (int)CameraType }
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }
        public bool AddCalibration(CalibrationParam calibrationParam)
        {
            if (CheckIsRun())
                return false;
            IsRun = false;
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "AddCalibration",
                Params = new CalibrationParamMQTT(calibrationParam)
            };
            PublishAsyncClient(mQTTMsg);

            return true;
        }
        public bool OpenCamera(string CameraID,TakeImageMode TakeImageMode,string ImageBpp)
        {
            if (CheckIsRun())
                return false;
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "OpenCamere",
                Params = new OpenCameraParamMQTT() { TakeImageMode = (int)TakeImageMode, CameraID = CameraID, Bpp = ImageBpp }
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public bool GetData(double expTime,double gain)
        {
            if (CheckIsRun())
                return false;
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "GetData",
                Params = new CameraParamMQTT() { ExpTime = expTime.ToString(),Gain =gain.ToString()}
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }
        private void PublishAsyncClient(MQTTMsg msg)
        {
            string json = JsonConvert.SerializeObject(msg, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            MQTTControl.PublishAsyncClient(SendTopic, json, false);
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

          
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }



        private class InitCameraParamMQTT : ViewModelBase
        {
            public int CameraType { get; set; }
        }

        private class OpenCameraParamMQTT : ViewModelBase
        {
            public int TakeImageMode { get; set; }
            public string CameraID { get; set; }
            public string Bpp { get; set; }    
        }


        private class CameraParamMQTT : ViewModelBase
        {
            [JsonProperty("expTime")]
            public string? ExpTime { get; set; }

            [JsonProperty("gain")]
            public string? Gain { get; set; }
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
