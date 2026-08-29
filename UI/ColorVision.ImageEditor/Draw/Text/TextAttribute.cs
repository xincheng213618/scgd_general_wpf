#pragma warning disable CA1711
using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class TextAttribute : ViewModelBase
    {
        public TextAttribute()
        {
            DefaultTextStyleConfig defaults = DefaultTextStyleConfig.Current;
            _FontSize = defaults.FontSize;
            _Brush = defaults.Brush;
            _FontFamily = defaults.FontFamily;
            _FontStyle = defaults.FontStyle;
            _FontWeight = defaults.FontWeight;
            _FontStretch = defaults.FontStretch;
            _FlowDirection = defaults.FlowDirection;
        }

        [Category("TextAttribute"), DisplayName("Text")]
        public string Text
        {
            get => _Text;
            set
            {
                string next = value ?? string.Empty;
                if (string.Equals(_Text, next, StringComparison.Ordinal))
                    return;

                _Text = next;
                OnPropertyChanged();
            }
        }
        private string _Text = string.Empty;

        [Category("TextAttribute"), DisplayName("FontSize")]
        public double FontSize
        {
            get => _FontSize;
            set
            {
                if (_FontSize.Equals(value))
                    return;

                _FontSize = value;
                OnPropertyChanged();
            }
        }
        private double _FontSize;

        [Category("TextAttribute"), DisplayName("Brush"),JsonIgnore]
        public Brush Brush
        {
            get => _Brush;
            set
            {
                if (ReferenceEquals(_Brush, value))
                    return;

                _Brush = value;
                OnPropertyChanged();
            }
        }
        private Brush _Brush;

        [Browsable(false)]
        [JsonProperty(nameof(Brush))]
        public string SerializedBrush
        {
            get => TextStyleSerialization.SerializeBrush(Brush);
            set => Brush = TextStyleSerialization.DeserializeBrush(value, DefaultTextStyleConfig.Current.Brush);
        }

        [Category("TextAttribute"), DisplayName("FontFamily"), JsonIgnore]
        public FontFamily FontFamily
        {
            get => _FontFamily;
            set
            {
                FontFamily next = value ?? DefaultTextStyleConfig.Current.FontFamily;
                if (Equals(_FontFamily, next))
                    return;

                _FontFamily = next;
                OnPropertyChanged();
            }
        }
        private FontFamily _FontFamily;

        [Browsable(false)]
        [JsonProperty(nameof(FontFamily))]
        public string SerializedFontFamily
        {
            get => TextStyleSerialization.SerializeFontFamily(FontFamily);
            set => FontFamily = TextStyleSerialization.DeserializeFontFamily(value, DefaultTextStyleConfig.Current.FontFamily);
        }

        [Category("TextAttribute"), DisplayName("FontStyle"), JsonIgnore]
        public FontStyle FontStyle
        {
            get => _FontStyle;
            set
            {
                if (_FontStyle.Equals(value))
                    return;

                _FontStyle = value;
                OnPropertyChanged();
            }
        }
        private FontStyle _FontStyle;

        [Browsable(false)]
        [JsonProperty(nameof(FontStyle))]
        public string SerializedFontStyle
        {
            get => TextStyleSerialization.SerializeFontStyle(FontStyle);
            set => FontStyle = TextStyleSerialization.DeserializeFontStyle(value, DefaultTextStyleConfig.Current.FontStyle);
        }

        [Category("TextAttribute"), DisplayName("FontWeight"), JsonIgnore]
        public FontWeight FontWeight
        {
            get => _FontWeight;
            set
            {
                if (_FontWeight.Equals(value))
                    return;

                _FontWeight = value;
                OnPropertyChanged();
            }
        }
        private FontWeight _FontWeight;

        [Browsable(false)]
        [JsonProperty(nameof(FontWeight))]
        public int SerializedFontWeight
        {
            get => TextStyleSerialization.SerializeFontWeight(FontWeight);
            set => FontWeight = TextStyleSerialization.DeserializeFontWeight(value, DefaultTextStyleConfig.Current.FontWeight);
        }

        [Category("TextAttribute"), DisplayName("FontStretch"), JsonIgnore]
        public FontStretch FontStretch
        {
            get => _FontStretch;
            set
            {
                if (_FontStretch.Equals(value))
                    return;

                _FontStretch = value;
                OnPropertyChanged();
            }
        }
        private FontStretch _FontStretch;

        [Browsable(false)]
        [JsonProperty(nameof(FontStretch))]
        public string SerializedFontStretch
        {
            get => TextStyleSerialization.SerializeFontStretch(FontStretch);
            set => FontStretch = TextStyleSerialization.DeserializeFontStretch(value, DefaultTextStyleConfig.Current.FontStretch);
        }

        [Category("TextAttribute"), DisplayName("FlowDirection"), JsonIgnore]
        public FlowDirection FlowDirection
        {
            get => _FlowDirection;
            set
            {
                if (_FlowDirection == value)
                    return;

                _FlowDirection = value;
                OnPropertyChanged();
            }
        }
        private FlowDirection _FlowDirection;

        [Browsable(false)]
        [JsonProperty(nameof(FlowDirection))]
        public string SerializedFlowDirection
        {
            get => TextStyleSerialization.SerializeFlowDirection(FlowDirection);
            set => FlowDirection = TextStyleSerialization.DeserializeFlowDirection(value, DefaultTextStyleConfig.Current.FlowDirection);
        }

    }



}
