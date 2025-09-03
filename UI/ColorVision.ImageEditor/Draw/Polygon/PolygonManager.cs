#pragma warning disable CS0414,CS8625
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class PolygonManager : IDisposable
    {
        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas DrawCanvas { get; set; }

        public DVPolygon? DrawingVisualPolygonCache { get; set; }

        public ImageViewModel ImageViewModel { get; set; }

        public PolygonManager(ImageViewModel imageViewModel, ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            ZoomboxSub = zombox;
            DrawCanvas = drawCanvas;
            ImageViewModel = imageViewModel;
        }
        public bool IsEnabled { get; set; } = true;

        private bool _IsShow;
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

        public void Load()
        {
            DrawCanvas.PreviewKeyDown += DrawCanvas_PreviewKeyDown;
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
            DrawingVisualPolygonCache = null;

        }

        private void DrawCanvas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key ==Key.End || e.Key==Key.Escape || e.Key == Key.Enter)
            {
                if (DrawingVisualPolygonCache != null)
                {
                    DrawingVisualPolygonCache.Points.RemoveAt(DrawingVisualPolygonCache.Points.Count - 1);
                    DrawingVisualPolygonCache.Render();
                    DrawingVisualPolygonCache = null;
                }
                e.Handled = true;
            }
        }


        Point MouseDownP { get; set; }
        Point MouseUpP { get; set; }

        bool IsMouseDown;


        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;

            if (DrawingVisualPolygonCache == null)
            {
                DrawingVisualPolygonCache = new DVPolygon();
                DrawingVisualPolygonCache.Points.Add(MouseDownP);
                DrawingVisualPolygonCache.Points.Add(MouseDownP);

                DrawingVisualPolygonCache.Attribute.Pen = new Pen(Brushes.Red, 1 / ZoomboxSub.ContentMatrix.M11);
                DrawingVisualPolygonCache.Render();
                DrawCanvas.AddVisual(DrawingVisualPolygonCache);
            }
            else
            {
                DrawingVisualPolygonCache.Points.Add(MouseDownP);
                DrawingVisualPolygonCache.Render();
            }
            e.Handled = true;
        }


        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            IsMouseDown = false;
            if (DrawingVisualPolygonCache != null)
            {
                MouseUpP = e.GetPosition(DrawCanvas);
                DrawingVisualPolygonCache.Points.RemoveAt(DrawingVisualPolygonCache.Points.Count - 1);
                DrawingVisualPolygonCache.Points.Add(MouseUpP);
                DrawingVisualPolygonCache.Render();
            }
            e.Handled = true;
        }



        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (DrawingVisualPolygonCache !=null)
            {
                var point = e.GetPosition(DrawCanvas);
                DrawingVisualPolygonCache.Points.RemoveAt(DrawingVisualPolygonCache.Points.Count - 1);
                DrawingVisualPolygonCache.Points.Add(point);
                DrawingVisualPolygonCache.Render();
            }
            e.Handled = true;
        }

        private void MouseEnter(object sender, MouseEventArgs e)
        {
        }

        private void MouseLeave(object sender, MouseEventArgs e)
        {

        }


        public void Dispose()
        {
            UnLoad();
            GC.SuppressFinalize(this);
        }
    }
}
