namespace ColorVision.Engine.Services.Devices.SMU.Configs
{
    public class SMUScanParam
    {
        public bool IsSourceV { set; get; }
        public double BeginValue { set; get; }
        public double EndValue { set; get; }
        public double LimitValue { set; get; }
        public int Points { set; get; }
    }
}
