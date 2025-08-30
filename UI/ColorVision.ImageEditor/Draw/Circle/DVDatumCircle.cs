using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVDatumCircle : DrawingVisualBase<CircleProperties>, IDrawingVisualDatum, ICircle
    {
        public bool AutoAttributeChanged { get; set; } = true;
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }

        public DVDatumCircle()
        {
            Attribute = new CircleProperties();
            Attribute.Brush = Brushes.Red;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Center = new Point(50, 50);
            Attribute.Radius = 30;

            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged && e.PropertyName!="Id")
                    Render();
            };
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }

        public override Rect GetRect()
        {
            return new Rect(Attribute.Center.X - Attribute.Radius, Attribute.Center.Y - Attribute.Radius, Attribute.Radius * 2, Attribute.Radius * 2);
        }
        public override void SetRect(Rect rect)
        {
            Attribute.Center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            Attribute.Radius = Math.Min(rect.Width, rect.Height) / 2;
            Render();
        }

    }



}
