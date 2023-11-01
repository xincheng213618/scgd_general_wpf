using Newtonsoft.Json;

namespace ColorVision.Services.Msg
{
    public class MsgReturn : IMsg
    {
        public string Version { get; set; }
        public string EventName { get; set; }
        public string SerialNumber { get; set; }
        public string ServiceName { get; set; }
        public string DeviceName { get; set; }
        public ulong ServiceID { get; set; }
        /// <summary>
        /// 设备Code
        /// </summary>
        [JsonProperty("CodeID")]
        public string DeviceCode { get; set; }
        /// <summary>
        /// 函数执行状态
        /// </summary>
        public int Code { get; set; }
        public string MsgID { get; set; }

        [JsonProperty("data")]
        public dynamic Data { get; set; }
        public override string ToString() => JsonConvert.SerializeObject(this);
    }
}
