namespace ColorVision.Engine.Services.Msg
{
    public delegate void MsgReturnHandler(MsgReturn msg);

    public delegate void MsgHandler(MsgSend msgSend, MsgReturn msgReturn);

    public interface IMsg
    {
        public string Version { get; set; }
        public string ServiceName { get; set; }
        public string EventName { get; set; }
        public string MsgID { get; set; }
    }
}
