namespace ColorVision.Services.Devices.SMU.Configs
{
    public class SMUScanResultData
    {
        public double[] VList { set; get; }
        public double[] IList { set; get; }
        public double[] ScanList { set; get; }

        public SMUScanResultData(double[] scan, double[] v, double[] i)
        {
            VList = v;
            IList = i;
            ScanList = scan;
        }
    }
}
