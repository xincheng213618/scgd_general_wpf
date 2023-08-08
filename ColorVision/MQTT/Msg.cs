using Newtonsoft.Json;
using System;

namespace ColorVision.MQTT
{
    public interface IMsg
    {
        public string Version { get; set; }
        public string ServiceName { get; set; }
        public ulong ServiceID { get; set; }
        public string EventName { get; set; }
    }

    public class MsgSend : IMsg
    {
        public string Version { get; set; }
        public string EventName { get; set; }
        public string ServiceName { get; set; }
        public ulong ServiceID { get; set; }
        public string CameraID { get; set; }
        public Guid MsgID { get; set; }
        [JsonProperty("params")]
        public dynamic Params { get; set; }
    }

    public delegate void MsgReturnHandler(MsgReturn msg);

    public class MsgReturn: IMsg
    {
        public string Version { get; set; }
        public string EventName { get; set; }
        public string ServiceName { get; set; }
        public ulong ServiceID { get; set; }

        //这里的设计有问题,应该是DeviceID，然后服务的ID是服务的，设备的ID是设备的，我这里是混在一起了，后续C++这里设计的有问题需要修改（不统一）
        [JsonProperty("CameraID")]
        public string DeviceID { get; set; }
        public int Code { get; set; }
        public string MsgID { get; set; }
        [JsonProperty("data")]
        public dynamic Data { get; set; }
    }

    public class ParamFunction
    {
        public string Name { get; set; }
        [JsonProperty("params")]
        public dynamic Params { get; set; }
    }
}
