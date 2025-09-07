#pragma warning disable CS0414,CS8625
using Gu.Wpf.Geometry;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class EraseManager : IDisposable
    {
        private ZoomboxSub Zoombox1 { get; set; }
        private DrawCanvas DrawCanvas { get; set; }
        public ImageViewModel ImageViewModel { get; set; }

        DrawingVisual EraseVisual { get; set; }

        public EraseManager(ImageViewModel imageEditViewMode, ZoomboxSub zombox, DrawCanvas drawCanvas)
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
            EraseVisual = new DrawingVisual();
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
            EraseVisual = null;
        }

        Point MouseDownP { get; set; }
        Point MouseUpP { get; set; }

        bool IsMouseDown;
        private double DefalutRadius { get; set; } = 30;
        public void DrawSelectRect(Rect rect)
        {
            using DrawingContext dc = EraseVisual.RenderOpen();
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#77F3F3F3")), new Pen(Brushes.Blue, 1), rect);
        }
        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.CaptureMouse();
            MouseDownP = e.GetPosition(DrawCanvas);
            IsMouseDown = true;

            ImageViewModel.DrawSelectRect(EraseVisual, new Rect(MouseDownP, MouseDownP)); ;
            DrawCanvas.AddVisual(EraseVisual);

            ImageViewModel.SelectEditorVisual.ClearRender();
            e.Handled = true;
        }


        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.ReleaseMouseCapture();
            MouseUpP = e.GetPosition(DrawCanvas);

            IsMouseDown = false;

            DrawCanvas.RemoveVisual(DrawCanvas.GetVisual<Visual>(MouseDownP));
            DrawCanvas.RemoveVisual(DrawCanvas.GetVisual<Visual>(MouseUpP));

            foreach (var item in DrawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP))))
            {
                DrawCanvas.RemoveVisual(item);
            }
            DrawCanvas.RemoveVisual(EraseVisual);
            e.Handled = true;
        }



        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseDown)
            {
                if (EraseVisual != null)
                {
                    var point = e.GetPosition(DrawCanvas);
                    ImageViewModel.DrawSelectRect(EraseVisual, new Rect(MouseDownP, point));
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
