#pragma warning disable CS0414,CS8625
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class EraseManager : IEditorToggleToolBase, IDisposable
    {
        private Zoombox Zoombox1 => EditorContext.Zoombox;
        private DrawCanvas DrawCanvas => EditorContext.DrawCanvas;
        public ImageViewModel ImageViewModel => EditorContext.ImageViewModel;

        public EditorContext EditorContext { get; set; }

        public EraseManager(EditorContext context)
        {
            EditorContext = context;
            Order = 2;
            ToolBarLocal = ToolBarLocal.Draw;
            Icon = IEditorToolFactory.TryFindResource("DrawingImageeraser");
        }
        DrawingVisual EraseVisual { get; set; }

        public override bool IsChecked
        {
            get => _IsChecked; set
            {
                if (_IsChecked == value) return;
                _IsChecked = value;
                if (value)
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(this);
                    Zoombox1.Cursor = Input.Cursors.Eraser;
                    Load();
                }
                else
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(null);
                    Zoombox1.Cursor = Cursors.Cross;
                    UnLoad();
                }
                OnPropertyChanged();
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


            using DrawingContext dc = EraseVisual.RenderOpen();
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#77F3F3F3")), new Pen(Brushes.Blue, 1), new Rect(MouseDownP, MouseDownP));
            DrawCanvas.AddVisualCommand(EraseVisual);

            ImageViewModel.SelectEditorVisual.ClearRender();
            e.Handled = true;
        }


        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.ReleaseMouseCapture();
            MouseUpP = e.GetPosition(DrawCanvas);

            IsMouseDown = false;

            DrawCanvas.RemoveVisualCommand(DrawCanvas.GetVisual<Visual>(MouseDownP));
            DrawCanvas.RemoveVisualCommand(DrawCanvas.GetVisual<Visual>(MouseUpP));

            foreach (var item in DrawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP))))
            {
                DrawCanvas.RemoveVisualCommand(item);
            }
            DrawCanvas.RemoveVisualCommand(EraseVisual);
            e.Handled = true;
        }



        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseDown)
            {
                if (EraseVisual != null)
                {
                    var point = e.GetPosition(DrawCanvas);

                    using DrawingContext dc = EraseVisual.RenderOpen();
                    dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#77F3F3F3")), new Pen(Brushes.Blue, 1), new Rect(MouseDownP, point));
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




        private bool _IsChecked;


        public void Dispose()
        {
            UnLoad();

            GC.SuppressFinalize(this);
        }
    }
}
