using ColorVision.Extension;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Input;
using ColorVision.Common.Extension;
using Microsoft.VisualBasic.Devices;
using ColorVision.Util.Draw.Special;

namespace ColorVision.Draw.Special
{
    public class Crosshair
    {
        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas DrawCanvas { get; set; }

        public DrawingVisual DrawVisualImage { get; set; }

        public DrawingVisual DrawingVisualImage1 { get; set; }

        public Crosshair(ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            ZoomboxSub = zombox;
            DrawCanvas = drawCanvas;
            DrawVisualImage = new DrawingVisual();
            DrawingVisualImage1 = new DrawingVisual();
        }

        public bool IsShow
        {
            get => _IsShow; set
            {
                if (_IsShow == value) return;
                _IsShow = value;
                DrawVisualImageControl(_IsShow);
                if (value)
                {
                    DrawCanvas.MouseMove += MouseMove;
                    DrawCanvas.MouseEnter += MouseEnter;
                    DrawCanvas.MouseLeave += MouseLeave;
                }
                else
                {
                    DrawCanvas.MouseMove -= MouseMove;
                    DrawCanvas.MouseEnter -= MouseEnter;
                    DrawCanvas.MouseLeave -= MouseLeave;
                }
            }
        }
        private bool _IsShow;

        public void DrawImage(Point actPoint)
        {
            if (DrawCanvas.Source is BitmapSource bitmapImage)
            {
                using DrawingContext dc = DrawVisualImage.RenderOpen();
                double mouseX = actPoint.X; // 示例坐标
                double mouseY = actPoint.Y; // 示例坐标
                double length = 1 / ZoomboxSub.ContentMatrix.M11;
                double radius = 5 / ZoomboxSub.ContentMatrix.M11; // 直径为10，半径为5
                // 绘制空心圆
                Pen circlePen = new Pen(Brushes.Black, length); // 黑色笔刷，线宽为1
                dc.DrawEllipse(null, new Pen(Brushes.White, length * 1.5), actPoint, radius, radius);
                dc.DrawEllipse(null, circlePen, actPoint, radius, radius);

                // 绘制虚线的笔刷
                Pen dashedPen = new Pen(Brushes.Black, length)
                {
                    DashStyle = new DashStyle(new double[] { 2, 2 }, 0) // 虚线样式
                };
                Pen dashedPen1 = new Pen(Brushes.White, length * 1.5)
                {
                    DashStyle = new DashStyle(new double[] { 2, 2 }, 0) // 虚线样式
                };

                // 绘制X轴虚线
                dc.DrawLine(dashedPen1, new Point(0, mouseY), new Point(mouseX - radius, mouseY)); // 左边
                dc.DrawLine(dashedPen1, new Point(mouseX + radius, mouseY), new Point(DrawCanvas.ActualWidth, mouseY));


                dc.DrawLine(dashedPen, new Point(0, mouseY), new Point(mouseX - radius, mouseY)); // 左边
                dc.DrawLine(dashedPen, new Point(mouseX + radius, mouseY), new Point(DrawCanvas.ActualWidth, mouseY));

                // 绘制Y轴虚线
                dc.DrawLine(dashedPen1, new Point(mouseX, 0), new Point(mouseX, mouseY - radius)); // 上边
                dc.DrawLine(dashedPen1, new Point(mouseX, mouseY + radius), new Point(mouseX, DrawCanvas.ActualHeight));

                dc.DrawLine(dashedPen, new Point(mouseX, 0), new Point(mouseX, mouseY - radius)); // 上边
                dc.DrawLine(dashedPen, new Point(mouseX, mouseY + radius), new Point(mouseX, DrawCanvas.ActualHeight));
            }
        }

        public double Ratio { get; set;}

        public void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsShow && sender is DrawCanvas drawCanvas && drawCanvas.Source is BitmapSource bitmap)
            {
                var point = e.GetPosition(drawCanvas);

                var controlWidth = drawCanvas.ActualWidth;
                var controlHeight = drawCanvas.ActualHeight;


                int imageWidth = bitmap.PixelWidth;
                int imageHeight = bitmap.PixelHeight;
                var actPoint = new Point(point.X, point.Y);

                Ratio = 1 / controlWidth * imageWidth;

                point.X = point.X / controlWidth * imageWidth;
                point.Y = point.Y / controlHeight * imageHeight;

                var bitPoint = new Point(point.X.ToInt32(), point.Y.ToInt32());

                if (point.X.ToInt32() >= 0 && point.X.ToInt32() < bitmap.PixelWidth && point.Y.ToInt32() >= 0 && point.Y.ToInt32() < bitmap.PixelHeight)
                {
                    var color = bitmap.GetPixelColor(point.X.ToInt32(), point.Y.ToInt32());
                    DrawImage(actPoint);
                }
            }


        }



        public void MouseEnter(object sender, MouseEventArgs e) => DrawVisualImageControl(true);

        public void MouseLeave(object sender, MouseEventArgs e) => DrawVisualImageControl(true);

        public void DrawVisualImageControl(bool Control)
        {
            if (Control)
            {
                if (!DrawCanvas.ContainsVisual(DrawVisualImage))
                    DrawCanvas.AddVisual(DrawVisualImage);
            }
            else
            {
                if (DrawCanvas.ContainsVisual(DrawVisualImage))
                    DrawCanvas.RemoveVisual(DrawVisualImage);
            }
        }
    }
}
