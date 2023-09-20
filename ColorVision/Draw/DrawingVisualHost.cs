using System.Windows.Media;
using System.Windows;
using System.Globalization;
using MySqlX.XDevAPI.Common;
using System.Windows.Media.Media3D;

namespace ColorVision.Draw
{
    public class DrawingVisualHost : FrameworkElement
    {
        private readonly DrawingVisual visual;

        public DrawingVisualHost()
        {
            visual = new DrawingVisual();
        }
        public void Render(double length)
        {
            Brush brush = Brushes.Red;
            FontFamily fontFamily = new FontFamily("Arial");
            double fontSize = 10;
            using (var dc = visual.RenderOpen())
            {
                double result = length < 10 ? 5 : length < 20 ? 10 : length < 50 ? 20 : length < 100 ? 50 : (length < 200 ? 100 : (length < 500 ? 200 : (length < 1000 ? 500 : (length < 2000 ? 1000 : 2000))));


                double X = 100;
                double Y = 100;

                dc.DrawLine(new Pen(Brushes.White, 4), new Point(X, Y), new Point(X+2 + 100 * result / length, Y));
                dc.DrawLine(new Pen(Brushes.White, 4), new Point(X, Y + 2), new Point(X, Y - 8 - 1));
                dc.DrawLine(new Pen(Brushes.White, 4), new Point(X + 100 * result / length, Y), new Point(X + 100 * result / length, Y - 8 - 1));

                dc.DrawLine(new Pen(Brushes.Black, 2), new Point(X, Y+1), new Point(X, Y - 8));
                dc.DrawLine(new Pen(Brushes.Black, 2), new Point(X + 100 * result / length, Y +1), new Point(X + 100 * result / length, Y - 8));
                dc.DrawLine(new Pen(Brushes.Black, 2), new Point(X, Y), new Point(X + 100 * result / length, Y));

                FormattedText formattedText1 = new FormattedText(result.ToString("F2") + "px", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                formattedText1.TextAlignment = TextAlignment.Center;
                formattedText1.MaxTextWidth = 100 * result / length;
                dc.DrawText(formattedText1, new Point(100, 80));
            }
            InvalidateVisual();
        }



        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.DrawDrawing(visual.Drawing);
        }
    }
}
