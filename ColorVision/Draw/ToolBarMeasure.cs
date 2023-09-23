using System.Windows;
using System.Windows.Input;
using ColorVision.Draw;

namespace ColorVision
{
    public class ToolBarMeasure
    {
        private ZoomboxSub Zoombox1 { get; set; }
        private DrawCanvas drawCanvas { get; set; }

        private FrameworkElement Parent { get; set; }

        public ToolBarMeasure(FrameworkElement Parent, ZoomboxSub zombox, DrawCanvas drawCanvas)
        {
            this.Parent = Parent;
            Zoombox1 = zombox;
            this.drawCanvas = drawCanvas;
        }
        private DrawingVisualRuler? DrawingVisualRulerCache;


        public bool Measure
        {
            get => _Measure;
            set
            {
                if (_Measure == value) return;
                _Measure = value;
                if (value)
                {
                    Parent.PreviewKeyDown += PreviewKeyDown;
                    drawCanvas.PreviewMouseLeftButtonDown += MouseDown;
                    drawCanvas.MouseMove += MouseMove;
                    drawCanvas.PreviewMouseLeftButtonUp += MouseUp;
                    drawCanvas.PreviewMouseRightButtonDown += PreviewMouseRightButtonDown;
                }
                else
                {
                    Parent.PreviewKeyDown -= PreviewKeyDown;
                    drawCanvas.PreviewMouseLeftButtonDown -= MouseDown;
                    drawCanvas.MouseMove -= MouseMove;
                    drawCanvas.PreviewMouseLeftButtonUp -= MouseUp;
                    drawCanvas.PreviewMouseRightButtonDown -= PreviewMouseRightButtonDown;

                }

            }
        }


        private bool _Measure;


        private bool IsMouseDown;
        private Point MouseDownP;


        private void MouseDown(object sender, MouseButtonEventArgs e)
        {
            IsMouseDown = true;
            if (DrawingVisualRulerCache == null)
            {
                DrawingVisualRulerCache = new DrawingVisualRuler();
                DrawingVisualRulerCache.Pen.Thickness = 1 / Zoombox1.ContentMatrix.M11;
                drawCanvas.AddVisual(DrawingVisualRulerCache);
            }
        }
        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && (Zoombox1.ActivateOn == ModifierKeys.None || !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn)))
            {
                var point = e.GetPosition(drawCanvas);

                if (DrawingVisualRulerCache != null)
                {
                    DrawingVisualRulerCache.MovePoints = point;
                    DrawingVisualRulerCache.Render();
                }
            }
        }
        private void MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                IsMouseDown = false;
                var MouseUpP = e.GetPosition(drawCanvas);
                if (DrawingVisualRulerCache != null)
                {
                    DrawingVisualRulerCache.Points.Add(MouseUpP);
                    DrawingVisualRulerCache.MovePoints = null;
                    DrawingVisualRulerCache.Render();
                }
            }
        }
        private void PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DrawingVisualRulerCache != null)
            {
                DrawingVisualRulerCache.MovePoints = null;
                DrawingVisualRulerCache.Render();
                DrawingVisualRulerCache = null;
            }
        }

        private void PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DrawingVisualRulerCache != null)
                {
                    drawCanvas.RemoveVisual(DrawingVisualRulerCache);
                    DrawingVisualRulerCache = null;
                }
            }
            if (e.Key == Key.Enter)
            {
                if (DrawingVisualRulerCache != null)
                {
                    DrawingVisualRulerCache.MovePoints = null;
                    DrawingVisualRulerCache.Render();
                    DrawingVisualRulerCache = null;
                }
            }
        }




    }
}
