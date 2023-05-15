#pragma warning disable CS4014,CS0618
using MQTTnet.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using static cvColorVision.GCSDLL;

namespace ColorVision.MQTT
{

    public interface IMQTT
    {
        bool Init();
        bool Calibration();
        bool Open();
        bool GetData();
        bool Close();
    }

    public delegate void MQTTSpectrumDataHandler(ColorParam colorPara);

    public class MQTTSpectrum
    {
        private static MQTTSpectrum _instance;
        private static readonly object _locker = new();
        public static MQTTSpectrum GetInstance() { lock (_locker) { return _instance ??= new MQTTSpectrum(); } }

        public event MQTTSpectrumDataHandler DataHandlerEvent;

        private string SubscribeTopic;
        private string SendTopic;
        private MQTTControl MQTTControl;

        public MQTTSpectrum()
        {
            MQTTControl = MQTTControl.GetInstance();
            Task.Run(MQTTControlInit);
        }

        private async void MQTTControlInit()
        {
            await Task.Delay(1000);

            SendTopic = "Spectrum";
            SubscribeTopic = "SpectrumService";

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
                    if (json.Code == 0)
                    {
                        if (json.EventName == "InitSpectrum")
                        {
                            MessageBox.Show("InitSpectrum");
                        }
                        else if (json.EventName == "CalibrationSpectrum")
                        {
                            MessageBox.Show("CalibrationSpectrum");
                        }
                        else if (json.EventName == "OpenSpectrum")
                        {
                            MessageBox.Show("OpenSpectrum");
                        }
                        else if (json.EventName == "GetDataSpectrum")
                        {
                            string data = json.Data.COLOR_PARA;
                            ColorParam colorParam = JsonConvert.DeserializeObject<ColorParam>(data);
                            Application.Current.Dispatcher.Invoke(() => DataHandlerEvent?.Invoke(colorParam));

                        }
                        else if (json.EventName == "CloseSpectrum")
                        {
                            MessageBox.Show("CloseSpectrum");
                        }
                    }
                }
                catch
                {
                    return Task.CompletedTask;
                }
            }
            return Task.CompletedTask;
        }


        private void PublishAsyncClient(MQTTMsg msg)
        {
            msg.ServiceName = SendTopic;
            string json = JsonConvert.SerializeObject(msg, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            MQTTControl.PublishAsyncClient(SendTopic, json, false);
        }

        public bool Init()
        {
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Init"
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public bool UnInit()
        {
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "UnInit",
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }


        public bool SetParam()
        {
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "SetParam"
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public bool Open()
        {
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Open"
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public bool GetData(float IntTime, int AveNum, bool bUseAutoIntTime =false, bool bUseAutoDark =false)
        {
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "GetData",
                Params = new GetDataParamMQTT()
                {
                    IntTime = IntTime,
                    AveNum = AveNum,
                    BUseAutoIntTime = bUseAutoIntTime,
                    BUseAutoDark = bUseAutoDark
                }
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public bool Close()
        {
            MQTTMsg mQTTMsg = new MQTTMsg
            {
                EventName = "Close"
            };
            PublishAsyncClient(mQTTMsg);
            return true;
        }

        public class GetDataParamMQTT
        {
            public float IntTime { get; set; }
            public int AveNum { get; set; }

            [JsonProperty("bUseAutoIntTime")]
            public bool BUseAutoIntTime { get; set; }
            [JsonProperty("bUseAutoDark")]
            public bool BUseAutoDark { get; set; }
        }



    }
}
