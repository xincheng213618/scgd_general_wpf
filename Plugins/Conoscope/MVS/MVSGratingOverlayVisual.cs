using System;
using System.Windows;
using System.Windows.Media;

namespace Conoscope.MVS
{
    internal sealed class MVSGratingOverlayVisual : DrawingVisual
    {
        private static readonly SolidColorBrush OuterStrokeBrush = CreateBrush(Colors.White);
        private static readonly SolidColorBrush InnerStrokeBrush = CreateBrush(Colors.Red);

        public void Update(Point center, double diameterPixels, double zoomScale)
        {
            using DrawingContext drawingContext = RenderOpen();

            if (diameterPixels <= 0 || double.IsNaN(diameterPixels) || double.IsInfinity(diameterPixels))
            {
                return;
            }

            if (double.IsNaN(center.X) || double.IsNaN(center.Y) || double.IsInfinity(center.X) || double.IsInfinity(center.Y))
            {
                return;
            }

            double radius = diameterPixels / 2.0;
            double normalizedZoom = Math.Max(Math.Abs(zoomScale), 0.0001);

            Pen outerPen = new Pen(OuterStrokeBrush, Math.Max(1.5, 4.0 / normalizedZoom));
            Pen innerPen = new Pen(InnerStrokeBrush, Math.Max(1.0, 2.0 / normalizedZoom));
            if (outerPen.CanFreeze)
            {
                outerPen.Freeze();
            }

            if (innerPen.CanFreeze)
            {
                innerPen.Freeze();
            }

            drawingContext.DrawEllipse(null, outerPen, center, radius, radius);
            drawingContext.DrawEllipse(null, innerPen, center, radius, radius);
        }

        public void Clear()
        {
            using DrawingContext drawingContext = RenderOpen();
        }

        private static SolidColorBrush CreateBrush(Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush;
        }
    }
}