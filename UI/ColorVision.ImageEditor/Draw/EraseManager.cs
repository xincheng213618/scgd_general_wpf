#pragma warning disable CS0414,CS8625
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class EraseManager : RegionOperationToolBase
    {
        public EraseManager(DrawEditorContext context) : base(context)
        {
            Order = 2;
            Icon = IEditorToolFactory.TryFindResource("DrawingImageeraser");
        }

        protected override Cursor ActiveCursor => Input.Cursors.Eraser;
        protected override Cursor InactiveCursor => Cursors.Cross;

        public Func<Visual, bool>? CanEraseVisual { get; set; }

        DrawingVisual EraseVisual { get; set; }

        protected override void LoadCore()
        {
            EraseVisual = new DrawingVisual();
            DrawCanvas.MouseMove += MouseMove;
            DrawCanvas.MouseEnter += MouseEnter;
            DrawCanvas.MouseLeave += MouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp += Image_PreviewMouseUp;
        }

        protected override void UnLoadCore()
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

            EditorContext.SelectionVisual.ClearRender();
            e.Handled = true;
        }


        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.ReleaseMouseCapture();
            MouseUpP = e.GetPosition(DrawCanvas);

            IsMouseDown = false;

            RemoveVisualIfAllowed(DrawCanvas.GetVisual<Visual>(MouseDownP));
            RemoveVisualIfAllowed(DrawCanvas.GetVisual<Visual>(MouseUpP));

            foreach (var item in DrawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP))))
            {
                RemoveVisualIfAllowed(item);
            }
            DrawCanvas.RemoveVisualCommand(EraseVisual);
            e.Handled = true;
        }

        private void RemoveVisualIfAllowed(Visual? visual)
        {
            if (visual == null || ReferenceEquals(visual, EraseVisual))
            {
                return;
            }

            if (CanEraseVisual?.Invoke(visual) == false)
            {
                return;
            }

            DrawCanvas.RemoveVisualCommand(visual);
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



    }
}
