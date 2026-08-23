using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DefaultTextStyleConfig : ViewModelBase, IConfig
    {
        private static readonly object SyncLock = new();
        private static DefaultTextStyleConfig? _current;

        public static DefaultTextStyleConfig Current
        {
            get
            {
                if (ConfigService.Instance != null)
                {
                    try
                    {
                        var configBacked = ConfigService.Instance.GetRequiredService<DefaultTextStyleConfig>();
                        lock (SyncLock)
                        {
                            _current = configBacked;
                            return _current;
                        }
                    }
                    catch
                    {
                    }
                }

                lock (SyncLock)
                {
                    _current ??= new DefaultTextStyleConfig();
                    return _current;
                }
            }
        }

        public static void SaveCurrent()
        {
            try
            {
                ConfigService.Instance?.Save<DefaultTextStyleConfig>();
            }
            catch
            {
            }
        }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                double next = double.IsFinite(value) && value > 0 ? TextRenderCore.NormalizeFontSize(value) : 10;
                if (_fontSize.Equals(next))
                    return;

                _fontSize = next;
                OnPropertyChanged();
            }
        }
        private double _fontSize = 10;

        [JsonIgnore]
        public Brush Brush
        {
            get => TextStyleSerialization.DeserializeBrush(SerializedBrush, Brushes.SaddleBrown);
            set => SerializedBrush = TextStyleSerialization.SerializeBrush(value);
        }

        [Browsable(false)]
        [JsonProperty(nameof(Brush))]
        public string SerializedBrush
        {
            get => _serializedBrush;
            set
            {
                string next = string.IsNullOrWhiteSpace(value) ? TextStyleSerialization.SerializeBrush(Brushes.SaddleBrown) : value;
                if (string.Equals(_serializedBrush, next, System.StringComparison.Ordinal))
                    return;

                _serializedBrush = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Brush));
            }
        }
        private string _serializedBrush = TextStyleSerialization.SerializeBrush(Brushes.SaddleBrown);

        [JsonIgnore]
        public FontFamily FontFamily
        {
            get => TextStyleSerialization.DeserializeFontFamily(SerializedFontFamily, new FontFamily("Arial"));
            set => SerializedFontFamily = TextStyleSerialization.SerializeFontFamily(value);
        }

        [Browsable(false)]
        [JsonProperty(nameof(FontFamily))]
        public string SerializedFontFamily
        {
            get => _serializedFontFamily;
            set
            {
                string next = string.IsNullOrWhiteSpace(value) ? "Arial" : value;
                if (string.Equals(_serializedFontFamily, next, System.StringComparison.Ordinal))
                    return;

                _serializedFontFamily = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FontFamily));
            }
        }
        private string _serializedFontFamily = "Arial";

        [JsonIgnore]
        public FontStyle FontStyle
        {
            get => TextStyleSerialization.DeserializeFontStyle(SerializedFontStyle, FontStyles.Normal);
            set => SerializedFontStyle = TextStyleSerialization.SerializeFontStyle(value);
        }

        [Browsable(false)]
        [JsonProperty(nameof(FontStyle))]
        public string SerializedFontStyle
        {
            get => _serializedFontStyle;
            set
            {
                string next = string.IsNullOrWhiteSpace(value) ? FontStyles.Normal.ToString() : value;
                if (string.Equals(_serializedFontStyle, next, System.StringComparison.Ordinal))
                    return;

                _serializedFontStyle = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FontStyle));
            }
        }
        private string _serializedFontStyle = FontStyles.Normal.ToString();

        [JsonIgnore]
        public FontWeight FontWeight
        {
            get => TextStyleSerialization.DeserializeFontWeight(SerializedFontWeight, FontWeights.Normal);
            set => SerializedFontWeight = TextStyleSerialization.SerializeFontWeight(value);
        }

        [Browsable(false)]
        [JsonProperty(nameof(FontWeight))]
        public int SerializedFontWeight
        {
            get => _serializedFontWeight;
            set
            {
                int next = value is > 0 and < 1000 ? value : FontWeights.Normal.ToOpenTypeWeight();
                if (_serializedFontWeight == next)
                    return;

                _serializedFontWeight = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FontWeight));
            }
        }
        private int _serializedFontWeight = FontWeights.Normal.ToOpenTypeWeight();

        [JsonIgnore]
        public FontStretch FontStretch
        {
            get => TextStyleSerialization.DeserializeFontStretch(SerializedFontStretch, FontStretches.Normal);
            set => SerializedFontStretch = TextStyleSerialization.SerializeFontStretch(value);
        }

        [Browsable(false)]
        [JsonProperty(nameof(FontStretch))]
        public string SerializedFontStretch
        {
            get => _serializedFontStretch;
            set
            {
                string next = string.IsNullOrWhiteSpace(value) ? FontStretches.Normal.ToString() : value;
                if (string.Equals(_serializedFontStretch, next, System.StringComparison.Ordinal))
                    return;

                _serializedFontStretch = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FontStretch));
            }
        }
        private string _serializedFontStretch = FontStretches.Normal.ToString();

        [JsonIgnore]
        public FlowDirection FlowDirection
        {
            get => TextStyleSerialization.DeserializeFlowDirection(SerializedFlowDirection, FlowDirection.LeftToRight);
            set => SerializedFlowDirection = TextStyleSerialization.SerializeFlowDirection(value);
        }

        [Browsable(false)]
        [JsonProperty(nameof(FlowDirection))]
        public string SerializedFlowDirection
        {
            get => _serializedFlowDirection;
            set
            {
                string next = string.IsNullOrWhiteSpace(value) ? FlowDirection.LeftToRight.ToString() : value;
                if (string.Equals(_serializedFlowDirection, next, System.StringComparison.Ordinal))
                    return;

                _serializedFlowDirection = next;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FlowDirection));
            }
        }
        private string _serializedFlowDirection = FlowDirection.LeftToRight.ToString();
    }
}
