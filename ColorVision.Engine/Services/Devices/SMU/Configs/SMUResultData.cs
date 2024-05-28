namespace ColorVision.Services.Devices.SMU.Configs
{
    public delegate void MQTTSMUResultHandler(SMUResultData data);

    public class SMUResultData
    {
        public double V { set; get; }
        public double I { set; get; }

        public SMUResultData(double v, double i)
        {
            V = v;
            I = i;
        }
    }
}
