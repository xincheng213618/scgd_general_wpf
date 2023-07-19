using Newtonsoft.Json;
using System;

namespace ColorVision.MQTT
{
    public class MsgSend
    {
        public string Version { get; set; } = "1.0";
        public string EventName { get; set; }
        public string ServiceName { get; set; }
        public ulong ServiceID { get; set; }
        public Guid MsgID { get; set; }
        [JsonProperty("params")]
        public dynamic Params { get; set; }
    }

    public delegate void MsgReturnHandler(MsgReturn msg);

    public class MsgReturn
    {
        public string Version { get; set; }
        public string EventName { get; set; }
        public string ServiceName { get; set; }
        public ulong ServiceID { get; set; }
        public int Code { get; set; }
        public string Msg { get; set; }
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
