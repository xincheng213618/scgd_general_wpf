using System.Windows.Input;

namespace ColorVision.ImageEditor.Draw.Ruler
{
    public class MeasureManager: IEditorToggleToolBase
    {
        private Zoombox Zoombox1 => EditorContext.Zoombox;
        private DrawCanvas DrawCanvas => EditorContext.DrawCanvas;

        public EditorContext EditorContext { get; set; }

        public MeasureManager(EditorContext context)
        {
            EditorContext = context;
            Order = 1;
            ToolBarLocal = ToolBarLocal.Draw;
            Icon = IEditorToolFactory.TryFindResource("DrawingImageruler");
        }

        private DrawingVisualRuler? RulerCache;


        private bool _IsChecked;
        public override bool IsChecked
        {
            get => _IsChecked;
            set
            {
                if (_IsChecked == value) return;
                _IsChecked = value;
                if (value)
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(this);

                    DrawCanvas.PreviewKeyDown += PreviewKeyDown;
                    DrawCanvas.PreviewMouseLeftButtonDown += MouseDown;
                    DrawCanvas.MouseMove += MouseMove;
                    DrawCanvas.PreviewMouseLeftButtonUp += MouseUp;
                    DrawCanvas.PreviewMouseRightButtonDown += PreviewMouseRightButtonDown;
                }
                else
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(null);

                    DrawCanvas.PreviewKeyDown -= PreviewKeyDown;
                    DrawCanvas.PreviewMouseLeftButtonDown -= MouseDown;
                    DrawCanvas.MouseMove -= MouseMove;
                    DrawCanvas.PreviewMouseLeftButtonUp -= MouseUp;
                    DrawCanvas.PreviewMouseRightButtonDown -= PreviewMouseRightButtonDown;

                }
                OnPropertyChanged();
            }
        }



        private void MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (RulerCache == null)
            {
                RulerCache = new DrawingVisualRuler();
                RulerCache.Pen.Thickness = 1 / Zoombox1.ContentMatrix.M11;
                DrawCanvas.AddVisualCommand(RulerCache);
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
            Key realKey = e.Key;
            if (realKey == Key.ImeProcessed)
            {
                realKey = e.ImeProcessedKey;
            }
            if (realKey == Key.Escape)
            {
                if (RulerCache != null)
                {
                    DrawCanvas.RemoveVisualCommand(RulerCache);
                    RulerCache = null;
                    IsChecked = false;
                }
            }
            else if (realKey == Key.End || realKey == Key.Space || realKey == Key.Enter || realKey == Key.Tab)
            {
                if (RulerCache != null)
                {
                    RulerCache.Render();
                    RulerCache = null;
                    IsChecked = false;
                }
                e.Handled = true;
            }
        }
    }
}
