using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using ColorVision.ImageEditor.Settings;
using ColorVision.Common.MVVM;

namespace ColorVision.ImageEditor.EditorTools.Filters
{
    public class DisplayShaderFilterState : ViewModelBase
    {
        private bool _isEnabled;
        private DisplayShaderChannelMode _channelMode = DisplayShaderChannelMode.Rgb;
        private double _temperature;
        private double _tint;
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

        [Display(Name = nameof(SettingsText.IsEnabled), GroupName = nameof(SettingsText.FilterBasics), ResourceType = typeof(SettingsText))]
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        [Display(Name = nameof(SettingsText.ChannelMode), GroupName = nameof(SettingsText.FilterBasics), ResourceType = typeof(SettingsText))]
        public DisplayShaderChannelMode ChannelMode
        {
            get => _channelMode;
            set => SetProperty(ref _channelMode, value);
        }

        [Range(-1d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.Temperature), GroupName = nameof(SettingsText.FilterWhiteBalance), ResourceType = typeof(SettingsText))]
        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, value);
        }

        [Range(-1d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.Tint), GroupName = nameof(SettingsText.FilterWhiteBalance), ResourceType = typeof(SettingsText))]
        public double Tint
        {
            get => _tint;
            set => SetProperty(ref _tint, value);
        }

        [Range(0d, 3d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.RedGain), GroupName = nameof(SettingsText.FilterWhiteBalance), ResourceType = typeof(SettingsText))]
        public double RedGain
        {
            get => _redGain;
            set => SetProperty(ref _redGain, value);
        }

        [Range(0d, 3d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.GreenGain), GroupName = nameof(SettingsText.FilterWhiteBalance), ResourceType = typeof(SettingsText))]
        public double GreenGain
        {
            get => _greenGain;
            set => SetProperty(ref _greenGain, value);
        }

        [Range(0d, 3d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.BlueGain), GroupName = nameof(SettingsText.FilterWhiteBalance), ResourceType = typeof(SettingsText))]
        public double BlueGain
        {
            get => _blueGain;
            set => SetProperty(ref _blueGain, value);
        }

        [Range(-1d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.RedOffset), GroupName = nameof(SettingsText.FilterWhiteBalance), ResourceType = typeof(SettingsText))]
        public double RedOffset
        {
            get => _redOffset;
            set => SetProperty(ref _redOffset, value);
        }

        [Range(-1d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.GreenOffset), GroupName = nameof(SettingsText.FilterWhiteBalance), ResourceType = typeof(SettingsText))]
        public double GreenOffset
        {
            get => _greenOffset;
            set => SetProperty(ref _greenOffset, value);
        }

        [Range(-1d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.BlueOffset), GroupName = nameof(SettingsText.FilterWhiteBalance), ResourceType = typeof(SettingsText))]
        public double BlueOffset
        {
            get => _blueOffset;
            set => SetProperty(ref _blueOffset, value);
        }

        [Range(-1d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.Brightness), GroupName = nameof(SettingsText.FilterTone), ResourceType = typeof(SettingsText))]
        public double Brightness
        {
            get => _brightness;
            set => SetProperty(ref _brightness, value);
        }

        [Range(0d, 3d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.Contrast), GroupName = nameof(SettingsText.FilterTone), ResourceType = typeof(SettingsText))]
        public double Contrast
        {
            get => _contrast;
            set => SetProperty(ref _contrast, value);
        }

        [Range(0.1d, 4d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.Gamma), GroupName = nameof(SettingsText.FilterTone), ResourceType = typeof(SettingsText))]
        public double Gamma
        {
            get => _gamma;
            set => SetProperty(ref _gamma, value);
        }

        [Range(0d, 3d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.Saturation), GroupName = nameof(SettingsText.FilterTone), ResourceType = typeof(SettingsText))]
        public double Saturation
        {
            get => _saturation;
            set => SetProperty(ref _saturation, value);
        }

        [Display(Name = nameof(SettingsText.Invert), GroupName = nameof(SettingsText.FilterTone), ResourceType = typeof(SettingsText))]
        public bool Invert
        {
            get => _invert;
            set => SetProperty(ref _invert, value);
        }

        [Display(Name = nameof(SettingsText.ThresholdMode), GroupName = nameof(SettingsText.FilterThreshold), ResourceType = typeof(SettingsText))]
        public DisplayShaderThresholdMode ThresholdMode
        {
            get => _thresholdMode;
            set => SetProperty(ref _thresholdMode, value);
        }

        [Range(0d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.Threshold), GroupName = nameof(SettingsText.FilterThreshold), ResourceType = typeof(SettingsText))]
        public double Threshold
        {
            get => _threshold;
            set => SetProperty(ref _threshold, value);
        }

        [Range(0d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.ThresholdLow), GroupName = nameof(SettingsText.FilterThreshold), ResourceType = typeof(SettingsText))]
        public double ThresholdLow
        {
            get => _thresholdLow;
            set => SetProperty(ref _thresholdLow, value);
        }

        [Range(0d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.ThresholdHigh), GroupName = nameof(SettingsText.FilterThreshold), ResourceType = typeof(SettingsText))]
        public double ThresholdHigh
        {
            get => _thresholdHigh;
            set => SetProperty(ref _thresholdHigh, value);
        }

        [Range(0d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.RangeLow), GroupName = nameof(SettingsText.FilterThreshold), ResourceType = typeof(SettingsText))]
        public double RangeLow
        {
            get => _rangeLow;
            set => SetProperty(ref _rangeLow, value);
        }

        [Range(0d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.RangeHigh), GroupName = nameof(SettingsText.FilterThreshold), ResourceType = typeof(SettingsText))]
        public double RangeHigh
        {
            get => _rangeHigh;
            set => SetProperty(ref _rangeHigh, value);
        }

        [Range(0d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.HighlightOpacity), GroupName = nameof(SettingsText.FilterThreshold), ResourceType = typeof(SettingsText))]
        public double HighlightOpacity
        {
            get => _highlightOpacity;
            set => SetProperty(ref _highlightOpacity, value);
        }

        [Display(Name = nameof(SettingsText.PseudoColorMode), GroupName = nameof(SettingsText.FilterPseudoColor), ResourceType = typeof(SettingsText))]
        public DisplayShaderPseudoColorMode PseudoColorMode
        {
            get => _pseudoColorMode;
            set => SetProperty(ref _pseudoColorMode, value);
        }

        [Range(0d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.PseudoMin), GroupName = nameof(SettingsText.FilterPseudoColor), ResourceType = typeof(SettingsText))]
        public double PseudoMin
        {
            get => _pseudoMin;
            set => SetProperty(ref _pseudoMin, value);
        }

        [Range(0d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        [Display(Name = nameof(SettingsText.PseudoMax), GroupName = nameof(SettingsText.FilterPseudoColor), ResourceType = typeof(SettingsText))]
        public double PseudoMax
        {
            get => _pseudoMax;
            set => SetProperty(ref _pseudoMax, value);
        }

        public void Reset()
        {
            ChannelMode = DisplayShaderChannelMode.Rgb;
            Temperature = 0;
            Tint = 0;
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
            Temperature = source.Temperature;
            Tint = source.Tint;
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
