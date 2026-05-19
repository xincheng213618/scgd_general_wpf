using ColorVision.Common.MVVM;
using ColorVision.Core;
using System.ComponentModel;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.PseudoColor
{
    public class PseudoColorToolState : ViewModelBase
    {
        [DisplayName("启用伪彩色")]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }
        private bool _isEnabled;

        [DisplayName("自动范围")]
        public bool IsAutoSetRange
        {
            get => _isAutoSetRange;
            set
            {
                _isAutoSetRange = value;
                OnPropertyChanged();
            }
        }
        private bool _isAutoSetRange;

        [DisplayName("伪彩色类型")]
        public ColormapTypes ColormapTypes
        {
            get => _colormapTypes;
            set
            {
                _colormapTypes = value;
                OnPropertyChanged();
            }
        }
        private ColormapTypes _colormapTypes = ColormapTypes.COLORMAP_JET;

        [Browsable(false)]
        public uint DataMin
        {
            get => _dataMin;
            set
            {
                _dataMin = value;
                OnPropertyChanged();
            }
        }
        private uint _dataMin;

        [Browsable(false)]
        public uint DataMax
        {
            get => _dataMax;
            set
            {
                _dataMax = value;
                OnPropertyChanged();
            }
        }
        private uint _dataMax;

        [Browsable(false)]
        public double SliderMinimum
        {
            get => _sliderMinimum;
            set
            {
                _sliderMinimum = value;
                OnPropertyChanged();
            }
        }
        private double _sliderMinimum;

        [Browsable(false)]
        public double SliderMaximum
        {
            get => _sliderMaximum;
            set
            {
                _sliderMaximum = value;
                OnPropertyChanged();
            }
        }
        private double _sliderMaximum = 255;

        [DisplayName("最小值")]
        public double SliderValueStart
        {
            get => _sliderValueStart;
            set
            {
                _sliderValueStart = value;
                OnPropertyChanged();
            }
        }
        private double _sliderValueStart;

        [DisplayName("最大值")]
        public double SliderValueEnd
        {
            get => _sliderValueEnd;
            set
            {
                _sliderValueEnd = value;
                OnPropertyChanged();
            }
        }
        private double _sliderValueEnd = 255;

        [Browsable(false)]
        public double SliderSmallChange
        {
            get => _sliderSmallChange;
            set
            {
                _sliderSmallChange = value;
                OnPropertyChanged();
            }
        }
        private double _sliderSmallChange = 1;

        [Browsable(false)]
        public double SliderLargeChange
        {
            get => _sliderLargeChange;
            set
            {
                _sliderLargeChange = value;
                OnPropertyChanged();
            }
        }
        private double _sliderLargeChange = 10;

        [Browsable(false)]
        public ImageSource? ColormapPreviewImage
        {
            get => _colormapPreviewImage;
            set
            {
                _colormapPreviewImage = value;
                OnPropertyChanged();
            }
        }
        private ImageSource? _colormapPreviewImage;

        public void ApplyDefaults(PseudoColorDefaultConfig defaults)
        {
            ColormapTypes = defaults.DefaultColormapTypes;
            IsAutoSetRange = defaults.IsAutoSetRangeByDefault;
        }

        public void ResetForNewImage(PseudoColorDefaultConfig defaults)
        {
            IsEnabled = false;
            ApplyDefaults(defaults);
            DataMin = 0;
            DataMax = 0;
            SliderMinimum = 0;
            SliderMaximum = 255;
            SliderValueStart = 0;
            SliderValueEnd = 255;
        }
    }
}
