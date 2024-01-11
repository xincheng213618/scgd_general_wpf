using cvColorVision;
using ColorVision.MVVM;

namespace ColorVision.Services.Device.Camera.Configs
{
    public class AutoFocusConfig : ViewModelBase
    {
        public double Forwardparam { get => _forwardpara; set { _forwardpara = value; NotifyPropertyChanged(); } }
        private double _forwardpara = 2000;

        public int CurStep { get => _curStep; set { _curStep = value; NotifyPropertyChanged(); } }
        private int _curStep = 5000;
        public double Curtailparam { get => _curtailparam; set { _curtailparam = value; NotifyPropertyChanged(); } }
        private double _curtailparam = 0.3;

        public int StopStep { get => _stopStep; set { _stopStep = value; NotifyPropertyChanged(); } }
        private int _stopStep = 200;

        public int MinPosition { get => _minPosition; set { _minPosition = value; NotifyPropertyChanged(); } }
        private int _minPosition = 80000;

        public int MaxPosition { get => _maxPosition; set { _maxPosition = value; NotifyPropertyChanged(); } }
        private int _maxPosition = 180000;
        public EvaFunc EvaFunc { get => _eEvaFunc; set { _eEvaFunc = value; NotifyPropertyChanged(); } }
        private EvaFunc _eEvaFunc = EvaFunc.Tenengrad;
        public double MinValue { get => _dMinValue; set { _dMinValue = value; NotifyPropertyChanged(); } }
        private double _dMinValue;

        public uint nTimeout { get => _nTimeout; set { _nTimeout = value; NotifyPropertyChanged(); } }
        private uint _nTimeout = 30000;


    }
}