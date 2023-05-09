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
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.MQTT
{

    public class MQTTMsgReturn
    {
        public string Version { get; set; }
        public string EventName { get; set; }
        public bool Code { get; set; }
        public string Msg { get; set; }

        public dynamic data { get; set; }
    }

    public class MQTTMsg
    {
        public string Version { get; set; } = "1.0";
        public string EventName { get; set; }
        [JsonProperty("params")]
        public dynamic Params { get; set; }
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

        public event EventHandler InitCamereSucess;

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
                            string CameraId = json.data.CameraId;
                            CameraID = JsonConvert.DeserializeObject<CameraId>(CameraId);
                            Application.Current.Dispatcher.Invoke(() => InitCamereSucess.Invoke(this, new EventArgs()));
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
                            string Filepath = json.data.FilePath;
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
            if (!MQTTControl.IsConnect)
                return false;

            if (IsRun)
            {
                MessageBox.Show("正在运行中");
                return false;
            }

            IsRun = false;
            string json = "{\"Version\":\"1.0\",\"EventName\":\"InitCamere\",\"params\":{\"CameraType\":"+ ((int)CameraType).ToString()+ "}}";
            MQTTControl.PublishAsyncClient(SendTopic, json, false);
            return true;
        }
        public bool AddCalibration(CalibrationParam calibrationParam)
        {
            if (!MQTTControl.IsConnect)
                return false;

            if (!MQTTControl.IsConnect)
                return false;

            if (IsRun)
            {
                MessageBox.Show("正在运行中");
                return false;
            }
            IsRun = false;
            MQTTMsg mQTTMsg = new MQTTMsg();
            mQTTMsg.EventName = "AddCalibration";
            mQTTMsg.Params = new CalibrationParamMQTT(calibrationParam);
            var jsonSetting = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            string json = JsonConvert.SerializeObject(mQTTMsg, Formatting.Indented, jsonSetting);
            MQTTControl.PublishAsyncClient(SendTopic, json, false);
            return true;
        }
        public bool OpenCamera(string CameraID,TakeImageMode TakeImageMode,string ImageBpp)
        {
            if (!MQTTControl.IsConnect)
                return false;

            if (IsRun)
            {
                MessageBox.Show("正在运行中");
                return false;
            }
            IsRun = false;
            string json = "{\"Version\":\"1.0\",\"EventName\":\"OpenCamere\",\"params\":{\"TakeImageMode\":"+ (int)TakeImageMode + ",\"CameraID\":\""+ CameraID + "\",\"Bpp\":"+ ImageBpp + "}}";
            MQTTControl.PublishAsyncClient(SendTopic, json, false);
            return true;
        }

        public bool GetData(double expTime,double gain)
        {
            if (!MQTTControl.IsConnect)
                return false;

            if (IsRun)
            {
                MessageBox.Show("正在运行中");
                return false;
            }
            IsRun = false;
            string json = "{\"Version\":\"1.0\",\"EventName\":\"GetData\",\"params\":{\"expTime\":"+ expTime+",\"gain\":"+ gain + "}}";
            MQTTControl.PublishAsyncClient(SendTopic, json, false);
            return true;
        }
          
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
