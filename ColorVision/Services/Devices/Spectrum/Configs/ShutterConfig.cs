namespace ColorVision.Device.Spectrum.Configs
{
    public class ShutterConfig
    {
        public int BaudRate { get; set; }
        public string Addr { get; set; }
        public string OpenCmd { get; set; }
        public string CloseCmd { get; set; }
        public int DelayTime { get; set; }
    }
}
