using Newtonsoft.Json;

namespace ColorVision.Engine.Messages
{
    public class MsgSend : IMsg
    {
        public string Version { get; set; } = "1.0";
        /// <summary>
        /// 发送的函数名
        /// </summary>
        public string EventName { get; set; }

        /// <summary>
        /// 服务名称
        /// </summary>
        public string ServiceName { get; set; }
        /// <summary>
        /// 设备Code
        /// </summary>
        public string DeviceCode { get; set; }
        /// <summary>
        /// RC的结果，用来做认证的
        /// </summary>
        public string Token { get; set; }
        //服务ID,这里用的指针转换后的常量，所以是用ulong,本地不保存，会直接发送过去，
        /// <summary>
        /// 批次号
        /// </summary>
        public string SerialNumber { get; set; }
        //MsgID,用来做消息同步确认的，如果是单方向发送的话，可以传空
        public string MsgID { get; set; }

        /// <summary>
        /// 参数
        /// </summary>
        [JsonProperty("params")]
        public dynamic Params { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
    }
}
