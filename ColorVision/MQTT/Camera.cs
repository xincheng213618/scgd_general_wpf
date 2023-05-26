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

    public class MQTTCamera :BaseService
    {

        public event MQTTCameraFileHandler FileHandler;

        public event EventHandler InitCameraSuccess;

        public CameraId? CameraID { get; set; }

        public MQTTCamera()
        {
            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.Connected += (s, e) => MQTTControlInit();
            Task.Run(() => MQTTControl.Connect());
        }

        private void MQTTControlInit()
        {
            SendTopic = "Camera";
            SubscribeTopic = "CameraService";
            MQTTControl.SubscribeAsyncClient(SubscribeTopic);
            //如果之前绑定了，先移除在添加
            MQTTControl.ApplicationMessageReceivedAsync -= MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
            MQTTControl.Connected -= (s, e) => MQTTControlInit();
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment);
                try
                {
                    MQTTMsgReturn json = JsonConvert.DeserializeObject<MQTTMsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    IsRun = false;
                    if (json.Code==0)
                    {
                        if (json.EventName == "Init")
                        {
                            string CameraId = json.Data.CameraId;
                            ServiceID = json.ServiceID;
                            CameraID = JsonConvert.DeserializeObject<CameraId>(CameraId);
                            Application.Current.Dispatcher.Invoke(() => InitCameraSuccess.Invoke(this, new EventArgs()));
                        }
                        else if (json.EventName == "SetParam")
                        {
                            MessageBox.Show("SetParam");
                        }
                        else if (json.EventName == "Open")
                        {
                            MessageBox.Show("OpenCamera");
                        }
                        else if (json.EventName == "GatData")
                        {
                            string Filepath = json.Data.FilePath;
                            Application.Current.Dispatcher.Invoke(() => FileHandler?.Invoke(Filepath));
                        }
                        else if (json.EventName == "Close")
                        {
                            MessageBox.Show("CloseCamera");
                        }
                        else if (json.EventName == "Uninit")
                        {
                            MessageBox.Show("Uninit");
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

        public bool Init(CameraType CameraType)
        {
            if (CheckIsRun())
                return false;
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Init",
                Params = new InitCameraParamMQTT() { CameraType = (int)CameraType }
            };
            PublishAsyncClient(mQTTMsg);
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
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "SetParam",
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public void FilterWheelSetPort(int port)
        {
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "SetParam",
                Params = new SetParamSetPort() {FunctionName = "CM_SetPort", Port = port }
            };
            PublishAsyncClient(mQTTMsg);
        }

        private class SetParamSetPort : SetParamFunctionMQTT
        {
            [JsonProperty("port")]
            public int Port { get; set; }
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
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "SetParam",
                Params = new CalibrationParamMQTT(calibrationParam)
            };
            PublishAsyncClient(mQTTMsg);

            return true;
        }
        public bool Open(string CameraID,TakeImageMode TakeImageMode,int ImageBpp)
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            if (CheckIsRun())
                return false;
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Open",
                Params = new OpenCameraParamMQTT() { TakeImageMode = (int)TakeImageMode, CameraID = CameraID, Bpp = ImageBpp }
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }
         
        public bool GetData(double expTime,double gain)
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            if (CheckIsRun())
                return false;
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "GetData",
                Params = new CameraParamMQTT() { ExpTime = expTime,Gain = gain}
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public bool Close()
        {
            if (ServiceID == 0)
            {
                MessageBox.Show("请先初始化");
                return false;
            }
            if (CheckIsRun())
                return false;
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Close"
            };
            PublishAsyncClient(mQTTMsg);
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

          
        private class InitCameraParamMQTT : ViewModelBase
        {
            public int CameraType { get; set; }
        }

        private class OpenCameraParamMQTT : ViewModelBase
        {
            public int TakeImageMode { get; set; }
            public string CameraID { get; set; }
            public int Bpp { get; set; }    
        }


        private class CameraParamMQTT : ViewModelBase
        {
            [JsonProperty("expTime")]
            public double ExpTime { get; set; }

            [JsonProperty("gain")]
            public double Gain { get; set; }
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
