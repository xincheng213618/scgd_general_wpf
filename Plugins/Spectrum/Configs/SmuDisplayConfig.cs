using ColorVision.Common.MVVM;

namespace Spectrum.Configs
{
    public class SmuDisplayConfig : ViewModelBase
    {
        private bool _isSourceV = true;
        private bool _isChannelA = true;
        private bool _suppressModeSwap;
        private double _measureVal = 5.0;
        private double _limitVal = 100.0;
        private double? _voltage;
        private double? _current;

        public bool IsSourceV
        {
            get => _isSourceV;
            set
            {
                if (_isSourceV == value) return;
                _isSourceV = value;
                OnPropertyChanged();
                SwapMeasureAndLimitValues();
            }
        }

        public bool IsChannelA
        {
            get => _isChannelA;
            set
            {
                if (_isChannelA == value) return;

                SaveCurrentChannelState();
                _isChannelA = value;
                OnPropertyChanged();
                LoadCurrentChannelState();
            }
        }

        public double MeasureVal
        {
            get => _measureVal;
            set
            {
                if (_measureVal == value) return;
                _measureVal = value;
                OnPropertyChanged();

                if (IsChannelA)
                {
                    AMeasureVal = value;
                }
                else
                {
                    BMeasureVal = value;
                }
            }
        }

        public double LimitVal
        {
            get => _limitVal;
            set
            {
                if (_limitVal == value) return;
                _limitVal = value;
                OnPropertyChanged();

                if (IsChannelA)
                {
                    ALimitVal = value;
                }
                else
                {
                    BLimitVal = value;
                }
            }
        }

        public double? V
        {
            get => _voltage;
            set
            {
                if (_voltage == value) return;
                _voltage = value;
                OnPropertyChanged();

                if (IsChannelA)
                {
                    AV = value;
                }
                else
                {
                    BV = value;
                }
            }
        }

        public double? I
        {
            get => _current;
            set
            {
                if (_current == value) return;
                _current = value;
                OnPropertyChanged();

                if (IsChannelA)
                {
                    AI = value;
                }
                else
                {
                    BI = value;
                }
            }
        }

        public double AMeasureVal { get => _aMeasureVal; set { if (_aMeasureVal == value) return; _aMeasureVal = value; OnPropertyChanged(); } }
        private double _aMeasureVal = 5.0;

        public double ALimitVal { get => _aLimitVal; set { if (_aLimitVal == value) return; _aLimitVal = value; OnPropertyChanged(); } }
        private double _aLimitVal = 100.0;

        public double BMeasureVal { get => _bMeasureVal; set { if (_bMeasureVal == value) return; _bMeasureVal = value; OnPropertyChanged(); } }
        private double _bMeasureVal = 5.0;

        public double BLimitVal { get => _bLimitVal; set { if (_bLimitVal == value) return; _bLimitVal = value; OnPropertyChanged(); } }
        private double _bLimitVal = 100.0;

        public double? AV { get => _aVoltage; set { if (_aVoltage == value) return; _aVoltage = value; OnPropertyChanged(); } }
        private double? _aVoltage;

        public double? AI { get => _aCurrent; set { if (_aCurrent == value) return; _aCurrent = value; OnPropertyChanged(); } }
        private double? _aCurrent;

        public double? BV { get => _bVoltage; set { if (_bVoltage == value) return; _bVoltage = value; OnPropertyChanged(); } }
        private double? _bVoltage;

        public double? BI { get => _bCurrent; set { if (_bCurrent == value) return; _bCurrent = value; OnPropertyChanged(); } }
        private double? _bCurrent;

        public void ClearOutput()
        {
            V = null;
            I = null;
        }

        private void SwapMeasureAndLimitValues()
        {
            if (_suppressModeSwap) return;

            _suppressModeSwap = true;
            try
            {
                double oldMeasure = MeasureVal;
                MeasureVal = LimitVal;
                LimitVal = oldMeasure;
            }
            finally
            {
                _suppressModeSwap = false;
            }
        }

        private void SaveCurrentChannelState()
        {
            if (IsChannelA)
            {
                AMeasureVal = _measureVal;
                ALimitVal = _limitVal;
                AV = _voltage;
                AI = _current;
            }
            else
            {
                BMeasureVal = _measureVal;
                BLimitVal = _limitVal;
                BV = _voltage;
                BI = _current;
            }
        }

        private void LoadCurrentChannelState()
        {
            _measureVal = IsChannelA ? AMeasureVal : BMeasureVal;
            _limitVal = IsChannelA ? ALimitVal : BLimitVal;
            _voltage = IsChannelA ? AV : BV;
            _current = IsChannelA ? AI : BI;

            OnPropertyChanged(nameof(MeasureVal));
            OnPropertyChanged(nameof(LimitVal));
            OnPropertyChanged(nameof(V));
            OnPropertyChanged(nameof(I));
        }
    }
}