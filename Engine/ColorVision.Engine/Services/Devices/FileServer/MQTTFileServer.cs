
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Messages;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MQTTnet;

namespace ColorVision.Engine.Services.Devices.FileServer
{
    public class FileServerEventName
    {
        public const string Heartbeat = "Heartbeat";
    }
    public class FileServerDataEvent
    {
        public string EventName { get; set; }
        public dynamic Data { get; set; }

        public FileServerDataEvent(string EventName, dynamic Data)
        {
            this.EventName = EventName;
            this.Data = Data;
        }
    }
    public delegate void MQTTImageDataHandler(object sender, FileServerDataEvent arg);
    public class MQTTFileServer : MQTTDeviceService<ConfigFileServer>
    {
        public event MQTTImageDataHandler OnImageData;
        public MQTTFileServer(ConfigFileServer config) : base(config)
        {
            Config = config;

            SendTopic = config.SendTopic;
            SubscribeTopic = config.SubscribeTopic;


            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.SubscribeCache(SubscribeTopic);

            MQTTControl.ApplicationMessageReceivedAsync += MqttClient_ApplicationMessageReceivedAsync;
        }

        private Task MqttClient_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            if (arg.ApplicationMessage.Topic == SubscribeTopic)
            {
                string Msg = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
                try
                {
                    MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(Msg);
                    if (json == null)
                        return Task.CompletedTask;
                    if (json.Code == 0 && json.DeviceCode.Equals(Config.Code, StringComparison.Ordinal))
                    {
                        if (json.EventName.Equals(FileServerEventName.Heartbeat, StringComparison.Ordinal))
                        {
                        }
                        else
                        {
                            OnImageData?.Invoke(this, new FileServerDataEvent(json.EventName, json.Data));
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

        public void Open(string fileName)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_Download,
                Params = new Dictionary<string,object> { { "FileName", fileName }, { "FileExtType", FileExtType.Src } }
            };
            PublishAsyncClient(msg);
        }

        public void GetAllFiles()
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_List_All,
                Params = new Dictionary<string, object> { { "FileExtType", FileExtType.Src } }
            };
            PublishAsyncClient(msg);
        }

        public void UploadFile(string fileName)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_Upload,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", FileExtType.Src } }
            };
            PublishAsyncClient(msg);
        }

    }
}
