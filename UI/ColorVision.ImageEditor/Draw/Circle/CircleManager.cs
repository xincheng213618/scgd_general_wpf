#pragma warning disable CS0414,CS8625
using Gu.Wpf.Geometry;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class CircleManager:IDisposable
    {
        private ZoomboxSub Zoombox1 { get; set; }
        private DrawCanvas DrawCanvas { get; set; }
        public ImageViewModel ImageViewModel { get; set; }

        private DVCircleText DrawCircleCache;

        public CircleManager(ImageViewModel imageEditViewMode, ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            Zoombox1 = zombox;
            DrawCanvas = drawCanvas;
            ImageViewModel = imageEditViewMode;
        }

        public bool IsEnabled { get; set; } = true;

        public bool IsShow
        {
            get => _IsShow; set
            {
                if (_IsShow == value) return;
                _IsShow = value;
                if (IsEnabled)
                {
                    if (value)
                    {
                        Load();
                    }
                    else
                    {
                        UnLoad();
                    }
                }
            }
        }

        public int CheckNo()
        {
            if (ImageViewModel.DrawingVisualLists.Count > 0 && ImageViewModel.DrawingVisualLists.Last() is DrawingVisualBase drawingVisual)
            {
                return drawingVisual.ID + 1;
            }
            else
            {
                return 1;
            }
        }

        public void Load()
        {
            DrawCanvas.MouseMove += MouseMove;
            DrawCanvas.MouseEnter += MouseEnter;
            DrawCanvas.MouseLeave += MouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp += Image_PreviewMouseUp;
        }

        public void UnLoad()
        {
            DrawCanvas.MouseMove -= MouseMove;
            DrawCanvas.MouseEnter -= MouseEnter;
            DrawCanvas.MouseLeave -= MouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp -= Image_PreviewMouseUp;
            DrawCircleCache = null;
        }

        Point MouseDownP { get; set; }
        Point MouseUpP { get; set; }

        bool IsMouseDown;
        private double DefalutRadius { get; set; } = 30;

        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;

            if (ImageViewModel.SelectEditorVisual.GetContainingRect(MouseDownP))
            {
                return;
            }
            else
            {
                ImageViewModel.SelectEditorVisual.ClearRender();
            }

            if (DrawCircleCache != null) return;

            CircleTextProperties circleTextProperties = new CircleTextProperties();
            int did = CheckNo();
            circleTextProperties.Id = did;
            circleTextProperties.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
            circleTextProperties.Center = MouseDownP;
            circleTextProperties.Radius = DefalutRadius;
            circleTextProperties.Text = "Point_" + did;
            DVCircleText dVCircle = new DVCircleText(circleTextProperties);

            DrawCircleCache = dVCircle;


            DrawCanvas.AddVisual(DrawCircleCache);
            e.Handled = true;
        }


        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.ReleaseMouseCapture();

            IsMouseDown = false;
            if (DrawCircleCache != null)
            {
                MouseUpP = e.GetPosition(DrawCanvas);

                if (DrawCircleCache.Attribute.Radius == DefalutRadius)
                    DrawCircleCache.Render();

                ImageViewModel.SelectEditorVisual.SetRender(DrawCircleCache); 

                DefalutRadius = DrawCircleCache.Radius;

                DrawCircleCache = null;
            }
            e.Handled = true;
        }



        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseDown)
            {
                if (DrawCircleCache != null)
                {
                    var point = e.GetPosition(DrawCanvas);

                    double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                    DrawCircleCache.Attribute.Radius = Radius;
                    DrawCircleCache.Render();
                }
            }
            e.Handled = true;
        }

        private void MouseEnter(object sender, MouseEventArgs e)
        {
        }

        private void MouseLeave(object sender, MouseEventArgs e)
        {
        }




        private bool _IsShow;




        public void Dispose()
        {
            UnLoad();

            GC.SuppressFinalize(this);
        }
    }
}
