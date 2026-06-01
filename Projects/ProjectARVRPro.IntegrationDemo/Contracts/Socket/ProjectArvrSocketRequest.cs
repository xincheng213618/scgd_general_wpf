namespace ProjectARVRPro.IntegrationDemo.Contracts.Socket
{
    public class ProjectArvrSocketRequest
    {
        public string Version { get; set; } = "1.0";
        public string MsgID { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Params { get; set; } = string.Empty;
    }
}
