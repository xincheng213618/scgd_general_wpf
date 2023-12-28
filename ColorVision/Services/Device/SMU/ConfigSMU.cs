using ColorVision.Services.Device;

namespace ColorVision.Services.Device.SMU
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

    public class ConfigSMU : BaseDeviceConfig
    {
        private bool _IsNet;
        public bool IsNet { get => _IsNet; set { _IsNet = value; NotifyPropertyChanged(); } }


        public bool IsSourceV { get => _IsSourceV; set { _IsSourceV = value; NotifyPropertyChanged(); } }
        private bool _IsSourceV = true;

        public string DevName { get => ID; set { ID = value; NotifyPropertyChanged(); } }


        public double StartMeasureVal { get => _startMeasureVal; set { _startMeasureVal = value; NotifyPropertyChanged(); } }
        private double _startMeasureVal;
        public double StopMeasureVal { get => _stopMeasureVal; set { _stopMeasureVal = value; NotifyPropertyChanged(); } }
        private double _stopMeasureVal;
        public int Number { get => _number; set { _number = value; NotifyPropertyChanged(); } }
        private int _number;

        public double LimitVal { get => _limitVal; set { _limitVal = value; NotifyPropertyChanged(); } }
        private double _limitVal;

        public double MeasureVal { get => _MeasureVal; set { _MeasureVal = value; NotifyPropertyChanged(); } }
        private double _MeasureVal;

        public double LmtVal { get => _lmtVal; set { _lmtVal = value; NotifyPropertyChanged(); } }
        private double _lmtVal;

        public double? V { get => _V; set { _V = value; NotifyPropertyChanged(); } }
        private double? _V;
        public double? I { get => _I; set { _I = value; NotifyPropertyChanged(); } }
        private double? _I;

    }
}
