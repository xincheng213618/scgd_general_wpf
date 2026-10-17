namespace ColorVision.Engine.Messages
{
    public delegate void MsgReturnHandler(MsgReturn msg);

    public interface IMsg
    {
        public string Version { get; set; }
        public string ServiceName { get; set; }
        public string EventName { get; set; }
        public string MsgID { get; set; }
    }
}
