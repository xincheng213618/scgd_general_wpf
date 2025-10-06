using ColorVision.Common.MVVM;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class TextAttribute : ViewModelBase
    {
        public static DefalutTextAttribute DefalutTextAttribute = DefalutTextAttribute.Defalut;

        [Category("TextAttribute"), DisplayName("Text")]
        public string Text { get => _Text; set { _Text = value; OnPropertyChanged(); } }
        private string _Text = string.Empty;

        [Category("TextAttribute"), DisplayName("FontSize")]
        public double FontSize { get => _FontSize; set { _FontSize = value; OnPropertyChanged(); } }
        private double _FontSize = DefalutTextAttribute.FontSize;

        [Category("TextAttribute"), DisplayName("Brush")]
        public Brush Brush { get => _Brush; set { _Brush = value; OnPropertyChanged(); } }
        private Brush _Brush = DefalutTextAttribute.Brush;

        [Category("TextAttribute"), DisplayName("FontFamily")]
        public FontFamily FontFamily { get => _FontFamily; set { _FontFamily = value; OnPropertyChanged(); } }
        private FontFamily _FontFamily = DefalutTextAttribute.FontFamily;

        [Category("TextAttribute"), DisplayName("FontStyle")]
        public FontStyle FontStyle { get => _FontStyle; set { _FontStyle = value; OnPropertyChanged(); } }
        private FontStyle _FontStyle = DefalutTextAttribute.FontStyle;
        [Category("TextAttribute"), DisplayName("FontWeight")]
        public FontWeight FontWeight { get => _FontWeight; set { _FontWeight = value; OnPropertyChanged(); } }
        private FontWeight _FontWeight = DefalutTextAttribute.FontWeight;
        [Category("TextAttribute"), DisplayName("FontStretch")]
        public FontStretch FontStretch { get => _FontStretch; set { _FontStretch = value; OnPropertyChanged(); } }
        private FontStretch _FontStretch = DefalutTextAttribute.FontStretch;

        [Category("TextAttribute"), DisplayName("FlowDirection")]
        public FlowDirection FlowDirection { get => _FlowDirection; set { _FlowDirection = value; OnPropertyChanged(); } }
        private FlowDirection _FlowDirection = DefalutTextAttribute.FlowDirection;

    }



}
