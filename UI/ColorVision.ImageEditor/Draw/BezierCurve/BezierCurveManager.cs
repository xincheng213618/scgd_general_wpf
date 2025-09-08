#pragma warning disable CS0414,CS8625
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class BezierCurveManager:IDisposable
    {
        private ZoomboxSub ZoomboxSub { get; set; }
        private DrawCanvas DrawCanvas { get; set; }

        public DrawingVisual BezierCurveImpCache { get; set; }

        public ImageViewModel ImageViewModel { get; set; }

        public BezierCurveManager(ImageViewModel imageViewModel, ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            ZoomboxSub = zombox;
            DrawCanvas = drawCanvas;
            ImageViewModel = imageViewModel;
        }

        private bool _IsShow;
        public bool IsShow
        {
            get => _IsShow; set
            {
                if (_IsShow == value) return;
                _IsShow = value;
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
            DrawCanvas.PreviewKeyDown -= DrawCanvas_PreviewKeyDown;

            DrawCanvas.MouseMove -= MouseMove;
            DrawCanvas.MouseEnter -= MouseEnter;
            DrawCanvas.MouseLeave -= MouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp -= Image_PreviewMouseUp;
            DVBezierCurveCache = null;

        }

        private void DrawCanvas_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key realKey = e.Key;
            if (realKey == Key.ImeProcessed)
            {
                realKey = e.ImeProcessedKey;
            }
            if (realKey == Key.End || realKey == Key.Escape || realKey == Key.Enter || realKey == Key.Tab | realKey == Key.Space)
            {
                if (DVBezierCurveCache != null)
                {
                    DVBezierCurveCache.Points.RemoveAt(DVBezierCurveCache.Points.Count - 1);
                    DVBezierCurveCache.Render();
                    DVBezierCurveCache = null;
                }
                e.Handled = true;
            }
        }


        Point MouseDownP { get; set; }
        Point MouseUpP { get; set; }

        bool IsMouseDown;

        DVBezierCurve DVBezierCurveCache { get; set; }

        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;
            DrawCanvas.Focus();

            if (DVBezierCurveCache == null)
            {
                DVBezierCurveCache = new DVBezierCurve() { AutoAttributeChanged = false };
                DVBezierCurveCache.Points.Add(MouseDownP);
                DVBezierCurveCache.Points.Add(MouseDownP);

                DVBezierCurveCache.Attribute.Pen = new Pen(Brushes.Red, 1 / ZoomboxSub.ContentMatrix.M11);
                DVBezierCurveCache.Render();
                DrawCanvas.AddVisualCommand(DVBezierCurveCache);
            }
            else
            {
                DVBezierCurveCache.Points.Add(MouseDownP);
                DVBezierCurveCache.Render();
            }
            e.Handled = true;
        }


        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            IsMouseDown = false;
            if (DVBezierCurveCache != null)
            {
                MouseUpP = e.GetPosition(DrawCanvas);
                DVBezierCurveCache.Points.RemoveAt(DVBezierCurveCache.Points.Count - 1);
                DVBezierCurveCache.Points.Add(MouseUpP);
                DVBezierCurveCache.Render();
            }
            e.Handled = true;
        }



        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (DVBezierCurveCache !=null)
            {
                var point = e.GetPosition(DrawCanvas);

                DVBezierCurveCache.Points.RemoveAt(DVBezierCurveCache.Points.Count - 1);
                DVBezierCurveCache.Points.Add(point);
                DVBezierCurveCache.Render();
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
