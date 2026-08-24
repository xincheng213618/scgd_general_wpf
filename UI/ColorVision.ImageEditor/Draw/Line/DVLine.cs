using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVLine : DrawingVisualBase<LineProperties>, IDrawingVisual, ILayoutScaleDrawingVisual
    {

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVLine()
        {
            Attribute = new LineProperties();
            Attribute.PropertyChanged += Attribute_PropertyChanged;
        }

        public DVLine(LineProperties attribute)
        {
            Attribute = attribute;
            Attribute.PropertyChanged += Attribute_PropertyChanged;
        }

        private void Attribute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LineProperties.Pen) || e.PropertyName == nameof(LineProperties.StrokeThickness))
            {
                LayoutBasePenThickness = null;
            }

            Render();
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            ApplyLayoutScaleCore(context, Pen, value => Pen = value);
        }


        public List<Point> Points { get => Attribute.Points; }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();

            if (Points.Count >= 2)
            {
                Pen pen = Attribute.Pen;
                for (int i = 1; i < Points.Count; i++)
                    dc.DrawLine(pen, Points[i - 1], Points[i]);
            }
        }

        public override Rect GetRect()
        {
            Rect rect = PointCollectionGeometry.GetBounds(Points);
            if (rect.IsEmpty)
                return Rect.Empty;

            // 考虑画笔粗细，向外扩展矩形
            // 这确保了即使是水平或垂直的直线，其矩形也具有厚度
            double halfPenThickness = Pen.Thickness / 2.0;
            rect.Inflate(halfPenThickness, halfPenThickness);

            return rect;
        }
        public override void SetRect(Rect rect)
        {
            if (Points.Count == 0)
            {
                return;
            }

            Rect currentRect = GetRect();
            if (currentRect.IsEmpty || currentRect.Width == 0 || currentRect.Height == 0)
            {
                // 如果当前矩形无效，无法进行缩放，可以选择将所有点移动到新矩形的中心
                for (int i = 0; i < Points.Count; i++)
                {
                    Points[i] = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
                }
                Render();
                return;
            }

            // 计算缩放比例
            double scaleX = rect.Width / currentRect.Width;
            double scaleY = rect.Height / currentRect.Height;

            List<Point> newPoints = new(Points.Count);
            for (int i = 0; i < Points.Count; i++)
            {
                newPoints.Add(new Point(
                    (Points[i].X - currentRect.X) * scaleX + rect.X,
                    (Points[i].Y - currentRect.Y) * scaleY + rect.Y));
            }

            Attribute.Points = newPoints;
            Render();
        }


    }



}
