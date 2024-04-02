#pragma warning disable CA1711,CA2211
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    public class DefalutTextAttribute : BaseAttribute
    {
        public static DefalutTextAttribute Defalut = new DefalutTextAttribute()
        {
            Brush = Brushes.Red,
            FontFamily = new FontFamily("Arial"),
            FontStyle = FontStyles.Normal,
            FontStretch = FontStretches.Normal,
            FlowDirection = FlowDirection.LeftToRight,
            FontWeight = FontWeights.Normal,
            FontSize = 10,
        };
        public double ActualLength { get => _ActualLength; set { _ActualLength = value; NotifyPropertyChanged(); } }
        private double _ActualLength = 1;
        public string PhysicalUnit { get => _PhysicalUnit; set { _PhysicalUnit = value; NotifyPropertyChanged(); } }
        private string _PhysicalUnit = "Px";

        public string Text { get => _Text; set { _Text = value; NotifyPropertyChanged(); } }
        private string _Text;
        public double FontSize { get => _FontSize; set { _FontSize = value; NotifyPropertyChanged(); } }
        private double _FontSize;

        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }
        private Brush _Brush;

        public FontFamily FontFamily { get => _FontFamily; set { _FontFamily = value; NotifyPropertyChanged(); } }
        private FontFamily _FontFamily;


        public FontStyle FontStyle { get => _FontStyle; set { _FontStyle = value; NotifyPropertyChanged(); } }
        private FontStyle _FontStyle;

        public FontWeight FontWeight { get => _FontWeight; set { _FontWeight = value; NotifyPropertyChanged(); } }
        private FontWeight _FontWeight;

        public FontStretch FontStretch { get => _FontStretch; set { _FontStretch = value; NotifyPropertyChanged(); } }
        private FontStretch _FontStretch;

        public FlowDirection FlowDirection { get => _FlowDirection; set { _FlowDirection = value; NotifyPropertyChanged(); } }
        private FlowDirection _FlowDirection;

    }



}
