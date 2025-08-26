namespace ColorVision.Engine
{
    /// <summary>
    /// 服务接口
    /// </summary>
    public interface IServiceConfig
    {
        /// <summary>
        /// 发送信道
        /// </summary>
        public string SendTopic { get; set; }

        /// <summary>
        /// 监听信道
        /// </summary>
        public string SubscribeTopic { get; set; }
    }



}
