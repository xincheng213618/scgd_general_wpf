using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.SMU.Configs
{
    public class ConfigSMU : DeviceServiceConfig
    {
        [DisplayName("Is4Wire")]
        public bool Is4Wire { get => _is4Wire; set { _is4Wire = value; OnPropertyChanged(); } }
        private bool _is4Wire;

        [DisplayName("IsFront")]
        public bool IsFront { get => _IsFront; set { _IsFront = value; OnPropertyChanged(); } }
        private bool _IsFront;

        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); } }
        private bool _IsNet;

        public bool IsAutoStart { get => _IsAutoStart; set { _IsAutoStart = value; OnPropertyChanged(); } }
        private bool _IsAutoStart;


        public bool IsSourceV { get => _IsSourceV; set { _IsSourceV = value; OnPropertyChanged(); } }
        private bool _IsSourceV = true;

        public string DevName { get => Id; set { Id = value; OnPropertyChanged(); } }
        public string DevType { get => _DevType; set { _DevType = value; OnPropertyChanged(); } }
        private string _DevType;

        public double StartMeasureVal { get => _startMeasureVal; set { _startMeasureVal = value; OnPropertyChanged(); } }
        private double _startMeasureVal;
        public double StopMeasureVal { get => _stopMeasureVal; set { _stopMeasureVal = value; OnPropertyChanged(); } }
        private double _stopMeasureVal;
        public int Number { get => _number; set { _number = value; OnPropertyChanged(); } }
        private int _number;

        public double LimitVal { get => _limitVal; set { _limitVal = value; OnPropertyChanged(); } }
        private double _limitVal;

        public double MeasureVal { get => _MeasureVal; set { _MeasureVal = value; OnPropertyChanged(); } }
        private double _MeasureVal;

        public double LmtVal { get => _lmtVal; set { _lmtVal = value; OnPropertyChanged(); } }
        private double _lmtVal;




        public double? V { get => _V; set { _V = value; OnPropertyChanged(); } }
        private double? _V;
        public double? I { get => _I; set { _I = value; OnPropertyChanged(); } }
        private double? _I;

    }
}
