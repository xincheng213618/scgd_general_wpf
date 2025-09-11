using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.Draw.Ruler
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
        private DrawingVisualRuler? RulerCache;


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



        private void MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (RulerCache == null)
            {
                RulerCache = new DrawingVisualRuler();
                RulerCache.Pen.Thickness = 1 / Zoombox1.ContentMatrix.M11;
                drawCanvas.AddVisualCommand(RulerCache);
            }
        }
        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && (Zoombox1.ActivateOn == ModifierKeys.None || !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn)))
            {
                var point = e.GetPosition(drawCanvas);

                if (RulerCache != null)
                {
                    RulerCache.MovePoints = point;
                    RulerCache.Render();
                }
            }
        }
        private void MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                var MouseUpP = e.GetPosition(drawCanvas);
                if (RulerCache != null)
                {
                    RulerCache.Points.Add(MouseUpP);
                    RulerCache.MovePoints = null;
                    RulerCache.Render();
                }
            }
        }
        private void PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (RulerCache != null)
            {
                RulerCache.MovePoints = null;
                RulerCache.Render();
                RulerCache = null;
            }
        }

        private void PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (RulerCache != null)
                {
                    drawCanvas.RemoveVisualCommand(RulerCache);
                    RulerCache = null;
                }
            }
            if (e.Key == Key.Enter)
            {
                if (RulerCache != null)
                {
                    RulerCache.MovePoints = null;
                    RulerCache.Render();
                    RulerCache = null;
                }
            }
        }




    }
}
