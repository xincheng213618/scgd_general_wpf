using System;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVDatumCircle : DrawingVisualBase<CircleProperties>, IDrawingVisualDatum, ICircle
    {
        private bool _deferAttributeRender;

        public bool AutoAttributeChanged { get; set; } = true;
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }

        public DVDatumCircle()
        {
            Attribute = new CircleProperties();
            ObserveAttributeChanges(OnAttributePropertyChanged);
        }

        private void OnAttributePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_deferAttributeRender)
                Render();
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            if (!ShapeGeometry.TryGetEllipseBounds(Attribute.Center, Attribute.Radius, Attribute.Radius, out Rect bounds))
                return;

            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, bounds.Width / 2, bounds.Height / 2);
        }

        public override Rect GetRect()
        {
            return ShapeGeometry.TryGetEllipseBounds(Attribute.Center, Attribute.Radius, Attribute.Radius, out Rect bounds)
                ? bounds
                : Rect.Empty;
        }
        public override void SetRect(Rect rect)
        {
            if (!ShapeGeometry.IsFinite(rect))
                return;

            bool wasDeferred = _deferAttributeRender;
            _deferAttributeRender = true;
            try
            {
                Attribute.Center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
                Attribute.Radius = Math.Min(rect.Width, rect.Height) / 2;
            }
            finally
            {
                _deferAttributeRender = wasDeferred;
            }

            if (!wasDeferred)
                Render();
        }

    }



}
