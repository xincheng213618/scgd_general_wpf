#pragma warning disable CS8625,CS8602,CS8607,CS0103,CS0067
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class SelectEditorVisual : DrawingVisual,IDisposable
    {
        public DrawCanvas DrawCanvas { get; set; }

        public ZoomboxSub ZoomboxSub { get; set; }

        public SelectEditorVisual(DrawCanvas drawCanvas, ZoomboxSub zoomboxSub)
        {
            DrawCanvas = drawCanvas;
            ZoomboxSub = zoomboxSub;
            DrawCanvas.AddVisual(this, false);
        }

        private void ZoomboxSub_LayoutUpdated(object? sender, System.EventArgs e)
        {
            Render();
        }

        public Rect Rect { get => _Rect; set {  _Rect = value; }  }
        private Rect _Rect;

        public ISelectVisual SelectVisual 
        { get => _SelectVisual;
            set
            { 
                _SelectVisual = value;
                if (value != null)
                {
                    ZoomboxSub.LayoutUpdated += ZoomboxSub_LayoutUpdated;
                }
                else
                {
                    ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;
                }
            } 
        }
        private ISelectVisual _SelectVisual;

        public void SetRect()
        {
            SelectVisual.SetRect(Rect);
            Render();
        }

        public Rect OldRect { get; set; }

        public Point FixedPoint{ get; set; }
        public Point FixedPoint1 { get; set; }

        public bool GetContainingRect(Point point)
        {
            double thickness = 1 / ZoomboxSub.ContentMatrix.M11;
            double smallRectSize = 10 * thickness;
            double halfSmallRectSize = smallRectSize / 2;
            OldRect = new Rect(Rect.X, Rect.Y, Rect.Width, Rect.Height);
            // 计算每个角落的小矩形，使其中心在角落
            Rect topLeft = new Rect(Rect.Left - halfSmallRectSize, Rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect topRight = new Rect(Rect.Right - halfSmallRectSize, Rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect bottomLeft = new Rect(Rect.Left - halfSmallRectSize, Rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect bottomRight = new Rect(Rect.Right - halfSmallRectSize, Rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);

            // 计算每条边中间的小矩形，使其中心在边的中点
            Rect middleTop = new Rect(Rect.Left + (Rect.Width / 2) - halfSmallRectSize, Rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect middleBottom = new Rect(Rect.Left + (Rect.Width / 2) - halfSmallRectSize, Rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect middleLeft = new Rect(Rect.Left - halfSmallRectSize, Rect.Top + (Rect.Height / 2) - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect middleRight = new Rect(Rect.Right - halfSmallRectSize, Rect.Top + (Rect.Height / 2) - halfSmallRectSize, smallRectSize, smallRectSize);


            // 检查点在哪个小矩形内
            if (topLeft.Contains(point))
            {
                ZoomboxSub.Cursor = Cursors.SizeNWSE;
                FixedPoint = OldRect.BottomRight;
                return true;
            }
            else if (topRight.Contains(point))
            {
                FixedPoint = OldRect.BottomLeft;
                ZoomboxSub.Cursor = Cursors.SizeNESW;
                return true;
            }
            else if (bottomLeft.Contains(point))
            {
                FixedPoint = OldRect.TopRight;
                ZoomboxSub.Cursor = Cursors.SizeNESW;
                return true;
            }
            else if (bottomRight.Contains(point))
            {
                FixedPoint = OldRect.TopLeft;
                ZoomboxSub.Cursor = Cursors.SizeNWSE;
                return true;
            }
            else if (middleTop.Contains(point))
            {
                FixedPoint = OldRect.BottomLeft;
                FixedPoint1 = OldRect.BottomRight;
                ZoomboxSub.Cursor = Cursors.SizeNS;

                return true;
            }
            else if (middleBottom.Contains(point))
            {
                FixedPoint = OldRect.TopLeft;
                FixedPoint1 = OldRect.TopRight;
                ZoomboxSub.Cursor = Cursors.SizeNS;
                return true;
            }
            else if (middleLeft.Contains(point))
            {
                FixedPoint = OldRect.TopRight;
                FixedPoint1 = OldRect.BottomRight;

                ZoomboxSub.Cursor = Cursors.SizeWE;
                return true;
            }
            else if (middleRight.Contains(point))
            {
                FixedPoint = OldRect.TopLeft;
                FixedPoint1 = OldRect.BottomLeft;
                ZoomboxSub.Cursor = Cursors.SizeWE;
                return true;
            }
            else if (Rect.Contains(point))
            {
                ZoomboxSub.Cursor = Cursors.SizeAll;
                return true;
            }

            return false;
        }

        public void SetRender(ISelectVisual selectVisual)
        {
            SelectVisual = selectVisual;
            if (selectVisual != null)
            {
                _Rect = selectVisual.GetRect();
            }
            Render();
        }

        public void Render()
        {
            DrawCanvas.AddVisual(this,false);
            using DrawingContext dc = this.RenderOpen();
            if (SelectVisual == null)
                return;
            double thickness = 1 / ZoomboxSub.ContentMatrix.M11;
            dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), Rect);
            // 小矩形的尺寸
            double smallRectSize = 10 * thickness;
            double halfSmallRectSize = smallRectSize / 2;

            // 计算每个角落的小矩形，使其中心在角落
            Rect topLeft = new Rect(Rect.Left - halfSmallRectSize, Rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect topRight = new Rect(Rect.Right - halfSmallRectSize, Rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect bottomLeft = new Rect(Rect.Left - halfSmallRectSize, Rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect bottomRight = new Rect(Rect.Right - halfSmallRectSize, Rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);

            // 计算每条边中间的小矩形，使其中心在边的中点
            Rect middleTop = new Rect(Rect.Left + (Rect.Width / 2) - halfSmallRectSize, Rect.Top - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect middleBottom = new Rect(Rect.Left + (Rect.Width / 2) - halfSmallRectSize, Rect.Bottom - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect middleLeft = new Rect(Rect.Left - halfSmallRectSize, Rect.Top + (Rect.Height / 2) - halfSmallRectSize, smallRectSize, smallRectSize);
            Rect middleRight = new Rect(Rect.Right - halfSmallRectSize, Rect.Top + (Rect.Height / 2) - halfSmallRectSize, smallRectSize, smallRectSize);
            // 绘制小矩形
            dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), topLeft);
            dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), topRight);
            dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), bottomLeft);
            dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), bottomRight);

            dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), middleTop);
            dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), middleBottom);
            dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), middleLeft);
            dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, thickness), middleRight);

            Point start = new Point(middleTop.Left + middleTop.Width / 2, middleTop.Top + middleTop.Height / 2);
            Point end = start + new Vector(0, -40 * thickness);

            // Draw line
            dc.DrawLine(new Pen(Brushes.White, thickness), start, end);

            // Draw rotation icon (simple circle for demonstration)
            double iconSize = 10 * thickness;
            Rect iconRect = new Rect(end.X - iconSize / 2, end.Y - iconSize / 2, iconSize, iconSize);
            dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.White, thickness), end, iconSize / 2, iconSize / 2);
        }

        public void Dispose()
        {
            DrawCanvas?.RemoveVisual(this, false);
            if (ZoomboxSub != null)
                ZoomboxSub.LayoutUpdated -= ZoomboxSub_LayoutUpdated;

            GC.SuppressFinalize(this);
        }
    }
}
