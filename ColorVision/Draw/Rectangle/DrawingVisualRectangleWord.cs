#pragma warning disable CA1711,CA2211
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    public class DrawingVisualRectangleWord : DrawingVisualRectangle, IRectangle
    {
        public override void Render()
        {
            Brush brush = Brushes.Red;
            FontFamily fontFamily = new FontFamily("Arial");
            double fontSize = Attribute.Pen.Thickness * 10;
            using DrawingContext dc = RenderOpen();
            FormattedText formattedText = new FormattedText("Point_" + ID.ToString(), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(formattedText, new Point(Attribute.Rect.X, Attribute.Rect.Y - fontSize));
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
        }
    }



}
