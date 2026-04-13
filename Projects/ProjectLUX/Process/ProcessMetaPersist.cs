namespace ProjectLUX.Process
{
    internal class ProcessMetaPersist
    {
        public string Name { get; set; }
        public string FlowTemplate { get; set; }
        public string ProcessTypeFullName { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string SocketCode { get; set; }
        public string ConfigJson { get; set; }
    }
}
