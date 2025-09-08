using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class DrawingVisualRuler:DrawingVisualBase<RulerTextProperties>,IDrawingVisual
    {
        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public DrawingVisualRuler()
        {
            Attribute = new RulerTextProperties();
            Attribute.Pen = new Pen(Brushes.Red,10);
        }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public bool AutoAttributeChanged { get; set; }

        public static double ActualLength { get => DefalutTextAttribute.Defalut.IsUsePhysicalUnit ? DefalutTextAttribute.Defalut.ActualLength :1; set { DefalutTextAttribute.Defalut.ActualLength = value;} }
        public static string PhysicalUnit { get => DefalutTextAttribute.Defalut.IsUsePhysicalUnit ? DefalutTextAttribute.Defalut.PhysicalUnit : "Px"; set { DefalutTextAttribute.Defalut.PhysicalUnit = value; } }

        public List<Point> Points { get => Attribute.Points;}

        public Point? MovePoints { get; set; }

        public override void Render()
        {
            Brush brush = Brushes.Red;
            Brush brush1 = Brushes.Pink;

            FontFamily fontFamily = new("Arial");
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
                FormattedText formattedText1 = new("起点", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                formattedText1.TextAlignment = TextAlignment.Center;
                dc.DrawText(formattedText1, Points[0]);

                double lenAll = 0;
                for (int i = 1; i < Points.Count-1; i++)
                {
                    double len = GetDistance(Points[i], Points[i - 1]);
                    len = len * ActualLength;
                    lenAll += len;
                    FormattedText formattedText2 = new(len.ToString("F2") + PhysicalUnit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(formattedText2, Points[i]);
                }

                if (Points.Count > 1)
                {
                    double Lastlen = GetDistance(Points[^1], Points[^2]);
                    Lastlen = Lastlen * ActualLength;
                    if (MovePoints == null)
                    {
                        lenAll += Lastlen;

                        FormattedText formattedText2 = new("总长" + lenAll.ToString("F2") + PhysicalUnit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        dc.DrawText(formattedText2, Points[^1]);
                    }
                    else
                    {
                        FormattedText formattedText2 = new(Lastlen.ToString("F2") + PhysicalUnit, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                        dc.DrawText(formattedText2, Points[^1]);
                    }
                }

            }
        }


        public static double GetDistance(Point startPoint, Point endPoint)
        {
            double x = Math.Abs(endPoint.X - startPoint.X);
            double y = Math.Abs(endPoint.Y - startPoint.Y);
            return Math.Sqrt(x * x + y * y);
        }


    }
}
