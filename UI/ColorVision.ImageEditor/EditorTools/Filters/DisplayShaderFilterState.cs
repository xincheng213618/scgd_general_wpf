using ColorVision.Common.MVVM;

namespace ColorVision.ImageEditor.EditorTools.Filters
{
    public class DisplayShaderFilterState : ViewModelBase
    {
        private bool _isEnabled;
        private DisplayShaderChannelMode _channelMode = DisplayShaderChannelMode.Rgb;
        private double _redGain = 1;
        private double _greenGain = 1;
        private double _blueGain = 1;
        private double _redOffset;
        private double _greenOffset;
        private double _blueOffset;
        private double _brightness;
        private double _contrast = 1;
        private double _gamma = 1;
        private double _saturation = 1;
        private bool _invert;
        private DisplayShaderThresholdMode _thresholdMode = DisplayShaderThresholdMode.Off;
        private double _threshold = 0.5;
        private double _thresholdLow = 0.05;
        private double _thresholdHigh = 0.95;
        private double _rangeLow = 0.4;
        private double _rangeHigh = 0.6;
        private double _highlightOpacity = 0.75;
        private DisplayShaderPseudoColorMode _pseudoColorMode = DisplayShaderPseudoColorMode.Off;
        private double _pseudoMin;
        private double _pseudoMax = 1;

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public DisplayShaderChannelMode ChannelMode
        {
            get => _channelMode;
            set => SetProperty(ref _channelMode, value);
        }

        public double RedGain
        {
            get => _redGain;
            set => SetProperty(ref _redGain, value);
        }

        public double GreenGain
        {
            get => _greenGain;
            set => SetProperty(ref _greenGain, value);
        }

        public double BlueGain
        {
            get => _blueGain;
            set => SetProperty(ref _blueGain, value);
        }

        public double RedOffset
        {
            get => _redOffset;
            set => SetProperty(ref _redOffset, value);
        }

        public double GreenOffset
        {
            get => _greenOffset;
            set => SetProperty(ref _greenOffset, value);
        }

        public double BlueOffset
        {
            get => _blueOffset;
            set => SetProperty(ref _blueOffset, value);
        }

        public double Brightness
        {
            get => _brightness;
            set => SetProperty(ref _brightness, value);
        }

        public double Contrast
        {
            get => _contrast;
            set => SetProperty(ref _contrast, value);
        }

        public double Gamma
        {
            get => _gamma;
            set => SetProperty(ref _gamma, value);
        }

        public double Saturation
        {
            get => _saturation;
            set => SetProperty(ref _saturation, value);
        }

        public bool Invert
        {
            get => _invert;
            set => SetProperty(ref _invert, value);
        }

        public DisplayShaderThresholdMode ThresholdMode
        {
            get => _thresholdMode;
            set => SetProperty(ref _thresholdMode, value);
        }

        public double Threshold
        {
            get => _threshold;
            set => SetProperty(ref _threshold, value);
        }

        public double ThresholdLow
        {
            get => _thresholdLow;
            set => SetProperty(ref _thresholdLow, value);
        }

        public double ThresholdHigh
        {
            get => _thresholdHigh;
            set => SetProperty(ref _thresholdHigh, value);
        }

        public double RangeLow
        {
            get => _rangeLow;
            set => SetProperty(ref _rangeLow, value);
        }

        public double RangeHigh
        {
            get => _rangeHigh;
            set => SetProperty(ref _rangeHigh, value);
        }

        public double HighlightOpacity
        {
            get => _highlightOpacity;
            set => SetProperty(ref _highlightOpacity, value);
        }

        public DisplayShaderPseudoColorMode PseudoColorMode
        {
            get => _pseudoColorMode;
            set => SetProperty(ref _pseudoColorMode, value);
        }

        public double PseudoMin
        {
            get => _pseudoMin;
            set => SetProperty(ref _pseudoMin, value);
        }

        public double PseudoMax
        {
            get => _pseudoMax;
            set => SetProperty(ref _pseudoMax, value);
        }

        public void Reset()
        {
            ChannelMode = DisplayShaderChannelMode.Rgb;
            RedGain = 1;
            GreenGain = 1;
            BlueGain = 1;
            RedOffset = 0;
            GreenOffset = 0;
            BlueOffset = 0;
            Brightness = 0;
            Contrast = 1;
            Gamma = 1;
            Saturation = 1;
            Invert = false;
            ThresholdMode = DisplayShaderThresholdMode.Off;
            Threshold = 0.5;
            ThresholdLow = 0.05;
            ThresholdHigh = 0.95;
            RangeLow = 0.4;
            RangeHigh = 0.6;
            HighlightOpacity = 0.75;
            PseudoColorMode = DisplayShaderPseudoColorMode.Off;
            PseudoMin = 0;
            PseudoMax = 1;
        }

        public void CopyFrom(DisplayShaderFilterState source)
        {
            if (source == null)
            {
                return;
            }

            IsEnabled = source.IsEnabled;
            ChannelMode = source.ChannelMode;
            RedGain = source.RedGain;
            GreenGain = source.GreenGain;
            BlueGain = source.BlueGain;
            RedOffset = source.RedOffset;
            GreenOffset = source.GreenOffset;
            BlueOffset = source.BlueOffset;
            Brightness = source.Brightness;
            Contrast = source.Contrast;
            Gamma = source.Gamma;
            Saturation = source.Saturation;
            Invert = source.Invert;
            ThresholdMode = source.ThresholdMode;
            Threshold = source.Threshold;
            ThresholdLow = source.ThresholdLow;
            ThresholdHigh = source.ThresholdHigh;
            RangeLow = source.RangeLow;
            RangeHigh = source.RangeHigh;
            HighlightOpacity = source.HighlightOpacity;
            PseudoColorMode = source.PseudoColorMode;
            PseudoMin = source.PseudoMin;
            PseudoMax = source.PseudoMax;
        }
    }
}
