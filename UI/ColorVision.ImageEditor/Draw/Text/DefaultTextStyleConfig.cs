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

        public double FontSize { get => _fontSize; set { _fontSize = value <= 0 ? 10 : value; OnPropertyChanged(); } }
        private double _fontSize = 10;

        [JsonIgnore]
        public Brush Brush
        {
            get => TextStyleSerialization.DeserializeBrush(SerializedBrush, Brushes.SaddleBrown);
            set
            {
                SerializedBrush = TextStyleSerialization.SerializeBrush(value);
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        [JsonProperty(nameof(Brush))]
        public string SerializedBrush { get => _serializedBrush; set { _serializedBrush = string.IsNullOrWhiteSpace(value) ? TextStyleSerialization.SerializeBrush(Brushes.SaddleBrown) : value; OnPropertyChanged(); OnPropertyChanged(nameof(Brush)); } }
        private string _serializedBrush = TextStyleSerialization.SerializeBrush(Brushes.SaddleBrown);

        [JsonIgnore]
        public FontFamily FontFamily
        {
            get => TextStyleSerialization.DeserializeFontFamily(SerializedFontFamily, new FontFamily("Arial"));
            set
            {
                SerializedFontFamily = TextStyleSerialization.SerializeFontFamily(value);
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        [JsonProperty(nameof(FontFamily))]
        public string SerializedFontFamily { get => _serializedFontFamily; set { _serializedFontFamily = string.IsNullOrWhiteSpace(value) ? "Arial" : value; OnPropertyChanged(); OnPropertyChanged(nameof(FontFamily)); } }
        private string _serializedFontFamily = "Arial";

        [JsonIgnore]
        public FontStyle FontStyle
        {
            get => TextStyleSerialization.DeserializeFontStyle(SerializedFontStyle, FontStyles.Normal);
            set
            {
                SerializedFontStyle = TextStyleSerialization.SerializeFontStyle(value);
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        [JsonProperty(nameof(FontStyle))]
        public string SerializedFontStyle { get => _serializedFontStyle; set { _serializedFontStyle = string.IsNullOrWhiteSpace(value) ? FontStyles.Normal.ToString() : value; OnPropertyChanged(); OnPropertyChanged(nameof(FontStyle)); } }
        private string _serializedFontStyle = FontStyles.Normal.ToString();

        [JsonIgnore]
        public FontWeight FontWeight
        {
            get => TextStyleSerialization.DeserializeFontWeight(SerializedFontWeight, FontWeights.Normal);
            set
            {
                SerializedFontWeight = TextStyleSerialization.SerializeFontWeight(value);
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        [JsonProperty(nameof(FontWeight))]
        public int SerializedFontWeight { get => _serializedFontWeight; set { _serializedFontWeight = value is > 0 and < 1000 ? value : FontWeights.Normal.ToOpenTypeWeight(); OnPropertyChanged(); OnPropertyChanged(nameof(FontWeight)); } }
        private int _serializedFontWeight = FontWeights.Normal.ToOpenTypeWeight();

        [JsonIgnore]
        public FontStretch FontStretch
        {
            get => TextStyleSerialization.DeserializeFontStretch(SerializedFontStretch, FontStretches.Normal);
            set
            {
                SerializedFontStretch = TextStyleSerialization.SerializeFontStretch(value);
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        [JsonProperty(nameof(FontStretch))]
        public string SerializedFontStretch { get => _serializedFontStretch; set { _serializedFontStretch = string.IsNullOrWhiteSpace(value) ? FontStretches.Normal.ToString() : value; OnPropertyChanged(); OnPropertyChanged(nameof(FontStretch)); } }
        private string _serializedFontStretch = FontStretches.Normal.ToString();

        [JsonIgnore]
        public FlowDirection FlowDirection
        {
            get => TextStyleSerialization.DeserializeFlowDirection(SerializedFlowDirection, FlowDirection.LeftToRight);
            set
            {
                SerializedFlowDirection = TextStyleSerialization.SerializeFlowDirection(value);
                OnPropertyChanged();
            }
        }

        [Browsable(false)]
        [JsonProperty(nameof(FlowDirection))]
        public string SerializedFlowDirection { get => _serializedFlowDirection; set { _serializedFlowDirection = string.IsNullOrWhiteSpace(value) ? FlowDirection.LeftToRight.ToString() : value; OnPropertyChanged(); OnPropertyChanged(nameof(FlowDirection)); } }
        private string _serializedFlowDirection = FlowDirection.LeftToRight.ToString();
    }
}