namespace ColorVision.MQTT
{
    public delegate void MQTTMsgHandler(MQMsg resultDataMQTT);
    public class MQMsg
    {
        public MQMsg()
        {

        }
        public MQMsg(int ResultCode, string ResultMsg)
        {
            this.ResultCode = ResultCode;
            this.ResultMsg = ResultMsg;
        }

        public MQMsg(int ResultCode, string ResultMsg, object Topic, object Payload)
        {
            this.ResultCode = ResultCode;
            this.ResultMsg = ResultMsg;
            this.Topic = Topic;
            this.Payload = Payload;
        }

        /// <summary>
        /// 结果Code
        /// 正常1，其他为异常；0不作为回复结果
        /// </summary>
        public int ResultCode { get; set; }

        /// <summary>
        /// 结果信息
        /// </summary>
        public string ResultMsg { get; set; } = string.Empty;

        /// <summary>
        /// 扩展1
        /// </summary>
        public object Topic { get; set; } = string.Empty;

        /// <summary>
        /// 扩展2
        /// </summary>
        public object Payload { get; set; } = string.Empty;
    }

}
