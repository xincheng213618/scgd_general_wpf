#pragma warning disable CS0414,CS8625
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class LineManager : IDisposable
    {
        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas DrawCanvas { get; set; }

        public DVLine? DVLineCache { get; set; }

        public ImageViewModel ImageViewModel { get; set; }

        public LineManager(ImageViewModel imageViewModel, ZoomboxSub zombox, DrawCanvas drawCanvas)
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
            DVLineCache = null;

        }

        Point MouseDownP { get; set; }
        Point MouseUpP { get; set; }

        bool IsMouseDown;


        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;
            DrawCanvas.Focus();

            if (DVLineCache == null)
            {
                DVLineCache = new DVLine();
                DVLineCache.Points.Add(MouseDownP);
                DVLineCache.Points.Add(MouseDownP);

                DVLineCache.Attribute.Pen = new Pen(Brushes.Red, 1 / ZoomboxSub.ContentMatrix.M11);
                DVLineCache.Render();
                DrawCanvas.AddVisualCommand(DVLineCache);
            }
            e.Handled = true;
        }


        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            IsMouseDown = false;
            if (DVLineCache != null)
            {
                MouseUpP = e.GetPosition(DrawCanvas);
                DVLineCache.Points.RemoveAt(DVLineCache.Points.Count - 1);
                DVLineCache.Points.Add(MouseUpP);
                DVLineCache.Render();
                DVLineCache = null;
            }
            e.Handled = true;
        }



        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (DVLineCache !=null)
            {
                var point = e.GetPosition(DrawCanvas);
                DVLineCache.Points.RemoveAt(DVLineCache.Points.Count - 1);
                DVLineCache.Points.Add(point);
                DVLineCache.Render();
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
