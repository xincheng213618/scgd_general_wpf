using Newtonsoft.Json;

namespace ColorVision.Engine.Messages
{
    public class MsgReturn : IMsg
    {
        public string Version { get; set; }
        public string EventName { get; set; }
        public string ServiceName { get; set; }
        public string DeviceName { get; set; }        /// <summary>
        /// 设备Code
        /// </summary>
        public string DeviceCode { get; set; }

        public string SerialNumber { get; set; }

        /// <summary>
        /// 函数执行状态
        /// </summary>
        public int Code { get; set; }

        public string MsgID { get; set; }

        [JsonProperty("data")]
        public dynamic Data { get; set; }
        
        public string Message { get; set; }

        public int ZIndex { get; set; } = -1;

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
    }
}
