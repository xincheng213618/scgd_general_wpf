namespace ProjectARVRPro.IntegrationDemo.Contracts.Socket
{
    public class ProjectArvrSocketResponse<TData>
    {
        public string Version { get; set; } = "1.0";
        public string MsgID { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public int Code { get; set; }
        public string Msg { get; set; } = string.Empty;
        public TData Data { get; set; }
    }
}
