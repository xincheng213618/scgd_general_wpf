using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{

    public class DrawingVisualRuler:DrawingVisualBase<RulerTextAttribute>,IDrawingVisual
    {
        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public DrawingVisualRuler()
        {
            Attribute = new RulerTextAttribute();
            Attribute.Pen = new Pen(Brushes.Red,10);
        }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public DrawBaseAttribute GetAttribute() => Attribute;
        public bool AutoAttributeChanged { get; set; }

        public double ActualLength { get; set; } = 1;
        public string PhysicalUnit { get; set; } = "Px";

        public List<Point> Points { get => Attribute.Points;}

        public Point? MovePoints { get; set; }

        public override void Render()
        {
            Brush brush = Brushes.Red;
            Brush brush1 = Brushes.Pink;

            FontFamily fontFamily = new FontFamily("Arial");
            double fontSize = Attribute.Pen.Thickness*10;

            using DrawingContext dc = RenderOpen();
            if (Points.Count >= 1)
            {
                for (int i = 1; i < Points.Count; i++)
                {
                    dc.DrawLine(new Pen(brush, Attribute.Pen.Thickness),Points[i-1], Points[i]);
                }
                if (MovePoints != null)
                {
                    dc.DrawLine(new Pen(brush1, Attribute.Pen.Thickness), Points[^1], (Point)MovePoints);
                }
            }

            if (Points.Count > 0)
            {
                FormattedText formattedText1 = new FormattedText("起点", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                formattedText1.TextAlignment = TextAlignment.Center;
                dc.DrawText(formattedText1, Points[0]);

                double lenAll = 0;
                for (int i = 1; i < Points.Count-1; i++)
                {
                    double len = GetDistance(Points[i], Points[i - 1]);
                    lenAll += len;
                    FormattedText formattedText2 = new FormattedText(len.ToString("F2") + "Px", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(formattedText2, Points[i]);
                }

                if (Points.Count > 1)
                {
                    double Lastlen = GetDistance(Points[^1], Points[^2]);

                    if (MovePoints == null)
                    {
                        lenAll += Lastlen;

                        FormattedText formattedText2 = new FormattedText("总长" + lenAll.ToString("F2") + "Px", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        dc.DrawText(formattedText2, Points[^1]);
                    }
                    else
                    {
                        FormattedText formattedText2 = new FormattedText(GetDistance(Points[^1], Points[^2]).ToString("F2") + "Px", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        dc.DrawText(formattedText2, Points[^1]);
                    }
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
