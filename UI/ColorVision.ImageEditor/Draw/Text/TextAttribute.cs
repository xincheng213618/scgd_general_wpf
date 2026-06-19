#pragma warning disable CA1711
using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class TextAttribute : ViewModelBase
    {
        private static DefaultTextStyleConfig DefaultSettings => DefaultTextStyleConfig.Current;

        [Category("TextAttribute"), DisplayName("Text")]
        public string Text { get => _Text; set { _Text = value; OnPropertyChanged(); } }
        private string _Text = string.Empty;

        [Category("TextAttribute"), DisplayName("FontSize")]
        public double FontSize { get => _FontSize; set { _FontSize = value; OnPropertyChanged(); } }
        private double _FontSize = DefaultSettings.FontSize;

        [Category("TextAttribute"), DisplayName("Brush"),JsonIgnore]
        public Brush Brush { get => _Brush; set { _Brush = value; OnPropertyChanged(); } }
        private Brush _Brush = DefaultSettings.Brush;

        [Browsable(false)]
        [JsonProperty(nameof(Brush))]
        public string SerializedBrush
        {
            get => TextStyleSerialization.SerializeBrush(Brush);
            set => Brush = TextStyleSerialization.DeserializeBrush(value, DefaultSettings.Brush);
        }

        [Category("TextAttribute"), DisplayName("FontFamily"), JsonIgnore]
        public FontFamily FontFamily { get => _FontFamily; set { _FontFamily = value; OnPropertyChanged(); } }
        private FontFamily _FontFamily = DefaultSettings.FontFamily;

        [Browsable(false)]
        [JsonProperty(nameof(FontFamily))]
        public string SerializedFontFamily
        {
            get => TextStyleSerialization.SerializeFontFamily(FontFamily);
            set => FontFamily = TextStyleSerialization.DeserializeFontFamily(value, DefaultSettings.FontFamily);
        }

        [Category("TextAttribute"), DisplayName("FontStyle"), JsonIgnore]
        public FontStyle FontStyle { get => _FontStyle; set { _FontStyle = value; OnPropertyChanged(); } }
        private FontStyle _FontStyle = DefaultSettings.FontStyle;

        [Browsable(false)]
        [JsonProperty(nameof(FontStyle))]
        public string SerializedFontStyle
        {
            get => TextStyleSerialization.SerializeFontStyle(FontStyle);
            set => FontStyle = TextStyleSerialization.DeserializeFontStyle(value, DefaultSettings.FontStyle);
        }

        [Category("TextAttribute"), DisplayName("FontWeight"), JsonIgnore]
        public FontWeight FontWeight { get => _FontWeight; set { _FontWeight = value; OnPropertyChanged(); } }
        private FontWeight _FontWeight = DefaultSettings.FontWeight;

        [Browsable(false)]
        [JsonProperty(nameof(FontWeight))]
        public int SerializedFontWeight
        {
            get => TextStyleSerialization.SerializeFontWeight(FontWeight);
            set => FontWeight = TextStyleSerialization.DeserializeFontWeight(value, DefaultSettings.FontWeight);
        }

        [Category("TextAttribute"), DisplayName("FontStretch"), JsonIgnore]
        public FontStretch FontStretch { get => _FontStretch; set { _FontStretch = value; OnPropertyChanged(); } }
        private FontStretch _FontStretch = DefaultSettings.FontStretch;

        [Browsable(false)]
        [JsonProperty(nameof(FontStretch))]
        public string SerializedFontStretch
        {
            get => TextStyleSerialization.SerializeFontStretch(FontStretch);
            set => FontStretch = TextStyleSerialization.DeserializeFontStretch(value, DefaultSettings.FontStretch);
        }

        [Category("TextAttribute"), DisplayName("FlowDirection"), JsonIgnore]
        public FlowDirection FlowDirection { get => _FlowDirection; set { _FlowDirection = value; OnPropertyChanged(); } }
        private FlowDirection _FlowDirection = DefaultSettings.FlowDirection;

        [Browsable(false)]
        [JsonProperty(nameof(FlowDirection))]
        public string SerializedFlowDirection
        {
            get => TextStyleSerialization.SerializeFlowDirection(FlowDirection);
            set => FlowDirection = TextStyleSerialization.DeserializeFlowDirection(value, DefaultSettings.FlowDirection);
        }

    }



}
