using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

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
        public int Code
        {
            get => _code;
            set { _code = value; HasCode = true; }
        }
        private int _code;

        [JsonIgnore]
        internal bool HasCode { get; private set; }

        public string MsgID { get; set; }

        [JsonProperty("data")]
        public dynamic Data { get; set; }
        
        public string Message { get; set; }

        public int ZIndex { get; set; } = -1;

        internal static bool TryReadInteger(JToken? token, out int value)
        {
            value = 0;
            return token is { Type: JTokenType.Integer or JTokenType.String }
                && int.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
    }
}
