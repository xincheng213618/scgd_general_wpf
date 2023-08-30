using Newtonsoft.Json;

namespace ColorVision.Device.SMU
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
    public class SMUOpenParam
    {
        public bool IsNet { set; get; }
        public string DevName { set; get; }
    }
    public class SMUGetDataParam
    {
        public bool IsSourceV { set; get; }
        public double MeasureValue { set; get; }
        public double LimitValue { set; get; }
    }

    public class SMUScanParam
    {
        public bool IsSourceV { set; get; }
        public double StartMeasureVal { set; get; }
        public double StopMeasureVal { set; get; }
        public double LimitVal { set; get; }
        public int Number { set; get; }
    }

    public class SMUConfig : BaseDeviceConfig
    {
        private bool _IsNet;
        public bool IsNet { get => _IsNet; set { _IsNet = value; NotifyPropertyChanged(); } }
    }
}
