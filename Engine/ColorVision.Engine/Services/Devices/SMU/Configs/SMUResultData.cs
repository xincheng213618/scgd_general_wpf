namespace ColorVision.Engine.Services.Devices.SMU.Configs
{

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
