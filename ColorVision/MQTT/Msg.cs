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
        //服务ID,这里用的指针转换后的常量，所以是用ulong,本地不保存，会直接发送过去，
        public ulong ServiceID { get; set; }
        public string SerialNumber { get; set; }
        //设备ID,目前是用的CamerID,后期每个设备都有自己的ID
        public string SnID { get; set; }
        //MsgID,用来做消息同步确认的，如果是单方向发送的话，可以不做确认。
        public Guid MsgID { get; set; }
        [JsonProperty("params")]
        public dynamic Params { get; set; }
    }

    public delegate void MsgReturnHandler(MsgReturn msg);

    public delegate void MsgHandler(MsgSend msgSend, MsgReturn msgReturn);

    public class MsgReturn: IMsg
    {
        public string Version { get; set; }
        public string EventName { get; set; }
        public string ServiceName { get; set; }
        public ulong ServiceID { get; set; }
        public string SnID { get; set; }
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
