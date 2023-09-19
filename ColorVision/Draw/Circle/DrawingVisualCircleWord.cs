#pragma warning disable CA1711,CA2211
using System.Globalization;
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


    public class TextAttribute : BaseAttribute
    {
        public static DefalutTextAttribute DefalutTextAttribute = DefalutTextAttribute.Defalut;


        public string Text { get => _Text; set { _Text = value; NotifyPropertyChanged(); } }
        private string _Text;
        public double FontSize { get => _FontSize; set { _FontSize = value; NotifyPropertyChanged(); } }
        private double _FontSize = DefalutTextAttribute.FontSize;

        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }
        private Brush _Brush = DefalutTextAttribute.Brush;

        public FontFamily FontFamily { get => _FontFamily; set { _FontFamily = value; NotifyPropertyChanged(); } }
        private FontFamily _FontFamily = DefalutTextAttribute.FontFamily;


        public FontStyle FontStyle { get => _FontStyle; set { _FontStyle = value; NotifyPropertyChanged(); } }
        private FontStyle _FontStyle = DefalutTextAttribute.FontStyle;

        public FontWeight FontWeight { get => _FontWeight; set { _FontWeight = value; NotifyPropertyChanged(); } }
        private FontWeight _FontWeight = DefalutTextAttribute.FontWeight;

        public FontStretch FontStretch { get => _FontStretch; set { _FontStretch = value; NotifyPropertyChanged(); } }
        private FontStretch _FontStretch = DefalutTextAttribute.FontStretch;

        public FlowDirection FlowDirection { get => _FlowDirection; set { _FlowDirection = value; NotifyPropertyChanged(); } }
        private FlowDirection _FlowDirection = DefalutTextAttribute.FlowDirection;

    }





    public class DrawingVisualCircleWord : DrawingVisualCircle
    {
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();

        public override void Render()
        {
            TextAttribute.Text = "Point_" + ID.ToString();
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            using DrawingContext dc = RenderOpen();

            FormattedText formattedText = new FormattedText(TextAttribute.Text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(formattedText, new Point(Attribute.Center.X - Attribute.Radius, Attribute.Center.Y - TextAttribute.FontSize));
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }
    }



}
