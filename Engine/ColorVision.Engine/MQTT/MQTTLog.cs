namespace ColorVision.Engine.MQTT
{
    public delegate void MQTTLogHandler(MQTTLog msg);
    public class MQTTLog
    {
        public MQTTLog()
        {

        }
        public MQTTLog(int ResultCode, string ResultMsg)
        {
            this.ResultCode = ResultCode;
            this.ResultMsg = ResultMsg;
        }

        public MQTTLog(int ResultCode, string ResultMsg, object Topic, object Payload)
        {
            this.ResultCode = ResultCode;
            this.ResultMsg = ResultMsg;
            this.Topic = Topic;
            this.Payload = Payload;
        }
        public int ResultCode { get; set; }

        public string ResultMsg { get; set; } = string.Empty;

        public object Topic { get; set; } = string.Empty;
        public object Payload { get; set; } = string.Empty;
    }

}
