namespace ColorVision.Services.Device.SMU.Configs
{
    public class ConfigSMU : BaseDeviceConfig
    {
        private bool _IsNet;
        public bool IsNet { get => _IsNet; set { _IsNet = value; NotifyPropertyChanged(); } }

        private bool _IsAutoStart;
        public bool IsAutoStart { get => _IsAutoStart; set { _IsAutoStart = value; NotifyPropertyChanged(); } }


        public bool IsSourceV { get => _IsSourceV; set { _IsSourceV = value; NotifyPropertyChanged(); } }
        private bool _IsSourceV = true;

        public string DevName { get => Id; set { Id = value; NotifyPropertyChanged(); } }
        public string DevType { get => _DevType; set { _DevType = value; NotifyPropertyChanged(); } }
        private string _DevType;

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
