using MySqlX.XDevAPI.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{

    public class DrawingVisualRuler:DrawingVisualBase<RulerTextAttribute>
    {
        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public DrawingVisualRuler()
        {

        }

        public double ActualLength { get; set; } = 1;
        public string PhysicalUnit { get; set; } = "Px";


        public List<Point> Points { get; set; } = new List<Point>();

        public Point? MovePoints { get; set; }

        public bool DrawOver { get; set; }

        public override void Render()
        {
            Brush brush = Brushes.Red;
            Brush brush1 = Brushes.Pink;

            FontFamily fontFamily = new FontFamily("Arial");
            double fontSize = 10;

            using DrawingContext dc = RenderOpen();
            if (Points.Count >= 1)
            {
                for (int i = 1; i < Points.Count; i++)
                {
                    dc.DrawLine(new Pen(brush,2),Points[i-1], Points[i]);
                }
                if (MovePoints != null)
                {
                    dc.DrawLine(new Pen(brush1, 2), Points[^1], (Point)MovePoints);
                }
            }

            if (Points.Count > 0)
            {
                FormattedText formattedText1 = new FormattedText("起点", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                formattedText1.TextAlignment = TextAlignment.Center;
                dc.DrawText(formattedText1, Points[0]);


                for (int i = 1; i < Points.Count; i++)
                {
                    FormattedText formattedText2 = new FormattedText(GetDistance(Points[i], Points[i - 1]).ToString("F2") + "Px", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    formattedText2.TextAlignment = TextAlignment.Center;
                    formattedText2.MaxTextWidth = 60;
                    dc.DrawText(formattedText2, Points[i]);
                }
            }


        }
        public static double GetDistance(Point startPoint, Point endPoint)
        {
            double x = System.Math.Abs(endPoint.X - startPoint.X);
            double y = System.Math.Abs(endPoint.Y - startPoint.Y);
            return Math.Sqrt(x * x + y * y);
        }


    }
}
