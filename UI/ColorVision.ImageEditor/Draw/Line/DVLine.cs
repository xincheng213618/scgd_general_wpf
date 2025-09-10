using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVLine : DrawingVisualBase<LineProperties>, IDrawingVisual
    {

        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVLine()
        {
            Attribute = new LineProperties();
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public DVLine(LineProperties attribute)
        {
            Attribute = attribute;
            Attribute.PropertyChanged += (s, e) => Render();
        }


        public List<Point> Points { get => Attribute.Points; }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();

            if (Points.Count >= 1)
            {
                for (int i = 1; i < Points.Count; i++)
                {
                    dc.DrawLine(new Pen(Attribute.Pen.Brush, Attribute.Pen.Thickness), Points[i - 1], Points[i]);
                }
            }
        }

        public override Rect GetRect()
        {
            if (Points.Count == 0)
            {
                return Rect.Empty;
            }

            // 计算所有点的边界
            double minX = Points.Min(p => p.X);
            double minY = Points.Min(p => p.Y);
            double maxX = Points.Max(p => p.X);
            double maxY = Points.Max(p => p.Y);

            var rect = new Rect(new Point(minX, minY), new Point(maxX, maxY));

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

            var newPoints = new List<Point>();
            for (int i = 0; i < Points.Count; i++)
            {
                // 1. 将点平移到原点坐标系
                double translatedX = Points[i].X - currentRect.X;
                double translatedY = Points[i].Y - currentRect.Y;

                // 2. 进行缩放
                double scaledX = translatedX * scaleX;
                double scaledY = translatedY * scaleY;

                // 3. 平移到新矩形的位置
                double finalX = scaledX + rect.X;
                double finalY = scaledY + rect.Y;

                newPoints.Add(new Point(finalX, finalY));
            }

            Attribute.Points = newPoints;
            Render();
        }


    }



}
