#pragma warning disable CS0414,CS8625
using ColorVision.Common.MVVM;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class RectangleManagerConfig : ViewModelBase
    {
        public bool IsLocked { get => _IsLocked; set { _IsLocked = value; OnPropertyChanged(); } }
        private bool _IsLocked;

        public double DefalutWidth { get => _DefalutWidth; set { _DefalutWidth = value; OnPropertyChanged(); } }
        private double _DefalutWidth = 30;

        public double DefalutHeight { get => _DefalutHeight; set { _DefalutHeight = value; OnPropertyChanged(); } }
        private double _DefalutHeight = 30;
    }

    public class RectangleManager : IDisposable
    {
        public RectangleManagerConfig Config { get; set; } = new RectangleManagerConfig();
        private ZoomboxSub Zoombox1 { get; set; }
        private DrawCanvas DrawCanvas { get; set; }
        public ImageViewModel ImageViewModel { get; set; }

        private DVRectangleText DrawingRectangleCache;

        public RectangleManager(ImageViewModel imageEditViewMode, ZoomboxSub zombox, DrawCanvas drawCanvas)
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
            DrawingRectangleCache = null;
        }



        Point MouseDownP { get; set; }
        Point MouseUpP { get; set; }

        bool IsMouseDown;



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
            int did = CheckNo();

            RectangleTextProperties rectangleTextProperties = new RectangleTextProperties();
            rectangleTextProperties.Id = did;
            rectangleTextProperties.Rect = new Rect(MouseDownP, new Point(MouseDownP.X + Config.DefalutWidth, MouseDownP.Y + Config.DefalutHeight));
            rectangleTextProperties.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
            rectangleTextProperties.Text = "Point_" + did;
            DrawingRectangleCache = new DVRectangleText(rectangleTextProperties);
            DrawCanvas.AddVisualCommand(DrawingRectangleCache);

            e.Handled = true;
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

        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.ReleaseMouseCapture();
            IsMouseDown = false;

            if (DrawingRectangleCache != null)
            {
                MouseUpP = e.GetPosition(DrawCanvas);

                if (DrawingRectangleCache.Attribute.Rect.Width == Config.DefalutWidth && DrawingRectangleCache.Attribute.Rect.Height == Config.DefalutHeight)
                    DrawingRectangleCache.Render();

                ImageViewModel.SelectEditorVisual.SetRender(DrawingRectangleCache);

                if (!Config.IsLocked)
                {
                    Config.DefalutWidth = DrawingRectangleCache.Attribute.Rect.Width;
                    Config.DefalutHeight = DrawingRectangleCache.Attribute.Rect.Height;
                }

                DrawingRectangleCache = null;
            }

            e.Handled = true;
        }



        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseDown)
            {
                if (DrawingRectangleCache != null)
                {
                    var point = e.GetPosition(DrawCanvas);

                    DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, point);
                    DrawingRectangleCache.Render();
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
