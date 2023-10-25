using System.Windows.Media;
using System.Windows;
using System.Globalization;

namespace ColorVision.Draw.Ruler
{
    public enum ScaleLocation
    {
        upperleft,
        upperright,
        lowerleft,
        lowerright
    }



    public class DrawingVisualScaleHost : FrameworkElement
    {
        private readonly DrawingVisual visual;

        public DrawingVisualScaleHost()
        {
            visual = new DrawingVisual();
        }

        public double ParentWidth { get; set; }
        public double ParentHeight { get; set; }


        public ScaleLocation ScaleLocation { get; set; } = ScaleLocation.upperleft;

        public double ActualLength { get; set; } = 1;
        public string PhysicalUnit { get; set; } = "Px";

        private double Lastlength = 1;
        public void Render() => Render(Lastlength);
        public void Render(double length)
        {
            Lastlength = length;
            Brush brush = Brushes.Red;
            FontFamily fontFamily = new FontFamily("Arial");
            double fontSize = 10;
            using (var dc = visual.RenderOpen())
            {
                if (ParentWidth > 200 && ParentHeight>200)
                {
                    double result = length < 10 ? 5 : length < 20 ? 10 : length < 50 ? 20 : length < 100 ? 50 : length < 200 ? 100 : length < 500 ? 200 : length < 1000 ? 500 : length < 2000 ? 1000 : length < 4000 ? 2000 : length < 8000 ? 4000 : 8000;


                    double X = 60;
                    double Y = 50;
                    if (ScaleLocation == ScaleLocation.lowerright)
                    {
                        X = ParentWidth - 120;
                        Y = ParentHeight - 30;
                    }

                    dc.DrawLine(new Pen(Brushes.White, 4), new Point(X, Y), new Point(X + 2 + 100 * result / length, Y));
                    dc.DrawLine(new Pen(Brushes.White, 4), new Point(X, Y + 2), new Point(X, Y - 8 - 1));
                    dc.DrawLine(new Pen(Brushes.White, 4), new Point(X + 100 * result / length, Y), new Point(X + 100 * result / length, Y - 8 - 1));

                    dc.DrawLine(new Pen(Brushes.Black, 2), new Point(X, Y + 1), new Point(X, Y - 8));
                    dc.DrawLine(new Pen(Brushes.Black, 2), new Point(X + 100 * result / length, Y + 1), new Point(X + 100 * result / length, Y - 8));
                    dc.DrawLine(new Pen(Brushes.Black, 2), new Point(X, Y), new Point(X + 100 * result / length, Y));

                    FormattedText formattedText1 = new FormattedText((result * ActualLength).ToString("F0") + " " + PhysicalUnit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    formattedText1.TextAlignment = TextAlignment.Center;
                    formattedText1.MaxTextWidth = 100 * result / length;
                    dc.DrawText(formattedText1, new Point(X, Y - 20));
                }

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
