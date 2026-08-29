using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class MultiPointDrawingToolStyleConfig : ViewModelBase
    {
        public Brush StrokeBrush
        {
            get => _strokeBrush;
            set
            {
                Brush next = value ?? Brushes.Red;
                if (Equals(_strokeBrush, next))
                {
                    return;
                }

                _strokeBrush = next;
                OnPropertyChanged();
            }
        }
        private Brush _strokeBrush = Brushes.Red;

        public double StrokeThickness
        {
            get => _strokeThickness;
            set
            {
                if (!double.IsFinite(value))
                    return;

                double next = Math.Max(1, value);
                if (_strokeThickness == next)
                {
                    return;
                }

                _strokeThickness = next;
                OnPropertyChanged();
            }
        }
        private double _strokeThickness = 1;
    }

    public abstract class MultiPointDrawingToolBase<TVisual> : DrawEditorToggleToolBase, ICompactInspectorProvider, IDisposable  where TVisual : DrawingVisual, ISelectVisual
    {
        private bool _isChecked;

        protected MultiPointDrawingToolBase(DrawEditorContext editorContext)
        {
            EditorContext = editorContext;
            ToolBarLocal = ToolBarLocal.Draw;
        }

        protected DrawEditorContext EditorContext { get; }
        protected DrawCanvas DrawCanvas => EditorContext.DrawCanvas;
        protected Zoombox Zoombox => EditorContext.Zoombox;
        protected SelectEditorVisual SelectionVisual => EditorContext.SelectionVisual;
        protected MultiPointDrawingToolStyleConfig StyleConfig { get; } = new();

        private protected double GetSafeZoomRatio()
        {
            double zoomRatio = Math.Abs(Zoombox.ContentMatrix.M11);
            if (!double.IsFinite(zoomRatio) || zoomRatio <= 0)
                return 1;

            return Math.Max(zoomRatio, 0.0001);
        }

        protected TVisual? ActiveVisual { get; private set; }
        private ActionCommand? _activeCreationCommand;
        private bool _isMouseDown;
        private bool _ownsMouseCapture;

        protected virtual bool SupportsKeyboardCompletion => false;
        protected virtual bool CompleteOnMouseUp => false;
        protected virtual bool SelectOnMouseUp => false;

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems() => BuildCompactInspectorItems();

        protected virtual IEnumerable<CompactInspectorItem> BuildCompactInspectorItems()
        {
            return new CompactInspectorItem[]
            {
                new CompactInspectorPropertyItem
                {
                    Source = StyleConfig,
                    PropertyName = nameof(StyleConfig.StrokeBrush),
                    Order = 10,
                    EditorKind = CompactInspectorEditorKind.Brush,
                    ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_LineColor,
                },
                new CompactInspectorPropertyItem
                {
                    Source = StyleConfig,
                    PropertyName = nameof(StyleConfig.StrokeThickness),
                    Icon = CompactInspectorIcons.CreateText("━"),
                    Width = 56,
                    Order = 20,
                    EditorKind = CompactInspectorEditorKind.Number,
                    ToolTip = ColorVision.ImageEditor.Properties.Resources.Draw_LineWidth,
                },
            };
        }

        public override bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                {
                    return;
                }

                _isChecked = value;
                if (value)
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(this);
                    Load();
                    OnActivated();
                }
                else
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(null);
                    UnLoad();
                    OnDeactivated();
                }

                OnPropertyChanged();
            }
        }

        protected abstract TVisual CreateVisual();
        protected abstract IList<Point> GetPoints(TVisual visual);
        protected abstract void RenderVisual(TVisual visual);

        protected virtual void InitializeVisual(TVisual visual, Point startPoint)
        {
            IList<Point> points = GetPoints(visual);
            points.Add(startPoint);
            points.Add(startPoint);
        }

        protected virtual bool IsCompletionKey(Key key)
        {
            return key == Key.End || key == Key.Space || key == Key.Enter || key == Key.Tab;
        }

        protected virtual void OnActivated()
        {
        }

        protected virtual void OnDeactivated()
        {
        }

        protected virtual void OnVisualCreated(TVisual visual)
        {
        }

        protected virtual void OnVisualMouseUp(TVisual visual, Point point)
        {
        }

        protected virtual void OnVisualCompleted(TVisual visual)
        {
            SelectionVisual.SetRender(visual);
        }

        private void Load()
        {
            DrawCanvas.PreviewKeyDown += HandlePreviewKeyDown;
            DrawCanvas.MouseMove += HandleMouseMove;
            DrawCanvas.PreviewMouseLeftButtonDown += HandlePreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp += HandlePreviewMouseUp;
            DrawCanvas.LostMouseCapture += DrawCanvas_LostMouseCapture;
            DrawCanvas.VisualsRemove += DrawCanvas_VisualsRemove;
        }

        private void UnLoad()
        {
            DrawCanvas.PreviewKeyDown -= HandlePreviewKeyDown;
            DrawCanvas.MouseMove -= HandleMouseMove;
            DrawCanvas.PreviewMouseLeftButtonDown -= HandlePreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp -= HandlePreviewMouseUp;
            DrawCanvas.LostMouseCapture -= DrawCanvas_LostMouseCapture;
            DrawCanvas.VisualsRemove -= DrawCanvas_VisualsRemove;
            _isMouseDown = false;
            if (_ownsMouseCapture)
                DrawCanvas.ReleaseMouseCapture();
            _ownsMouseCapture = false;
            CancelActiveVisual();
        }

        private void DrawCanvas_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (!_isMouseDown)
                return;

            _isMouseDown = false;
            _ownsMouseCapture = false;
            if (CompleteOnMouseUp)
            {
                CancelActiveVisual();
                IsChecked = false;
            }
        }

        private void DrawCanvas_VisualsRemove(object? sender, VisualChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Visual, ActiveVisual))
            {
                return;
            }

            ActionCommand? creationCommand = _activeCreationCommand;
            bool preserveCreationHistory = e.Visual != null && DrawCanvas.IsVisualRemovalCommandInProgress(e.Visual);
            ActiveVisual = null;
            _activeCreationCommand = null;
            _isMouseDown = false;
            if (_ownsMouseCapture)
                DrawCanvas.ReleaseMouseCapture();
            _ownsMouseCapture = false;
            if (creationCommand != null && !preserveCreationHistory)
            {
                DrawCanvas.DiscardActionCommand(creationCommand);
            }
        }

        private void HandlePreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!SupportsKeyboardCompletion || ActiveVisual == null)
            {
                return;
            }

            Key realKey = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
            if (realKey == Key.Escape)
            {
                CancelActiveVisual();
                IsChecked = false;
                e.Handled = true;
                return;
            }

            if (IsCompletionKey(realKey))
            {
                CompleteCurrentVisual(trimPreviewPoint: true);
                IsChecked = false;
                e.Handled = true;
            }
        }

        private void HandlePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _ownsMouseCapture = DrawCanvas.CaptureMouse();
            _isMouseDown = true;
            DrawCanvas.Focus();

            Point point = e.GetPosition(DrawCanvas);
            if (ActiveVisual == null)
            {
                TVisual visual = CreateVisual();
                InitializeVisual(visual, point);
                OnVisualCreated(visual);
                RenderVisual(visual);
                ActionCommand? creationCommand = DrawCanvas.AddVisualCommandCore(visual);
                if (creationCommand == null || !IsChecked || !DrawCanvas.ContainsVisual(visual))
                {
                    if (DrawCanvas.ContainsVisual(visual))
                        DrawCanvas.RemoveVisual(visual);
                    if (creationCommand != null)
                        DrawCanvas.DiscardActionCommand(creationCommand);
                    e.Handled = true;
                    return;
                }

                _activeCreationCommand = creationCommand;
                ActiveVisual = visual;
            }
            else
            {
                GetPoints(ActiveVisual).Add(point);
                RenderVisual(ActiveVisual);
            }

            e.Handled = true;
        }

        private void HandlePreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isMouseDown || e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            _isMouseDown = false;
            if (_ownsMouseCapture)
                DrawCanvas.ReleaseMouseCapture();
            _ownsMouseCapture = false;
            if (ActiveVisual != null)
            {
                Point point = e.GetPosition(DrawCanvas);
                ReplacePreviewPoint(ActiveVisual, point);
                RenderVisual(ActiveVisual);
                OnVisualMouseUp(ActiveVisual, point);

                if (CompleteOnMouseUp)
                {
                    CompleteCurrentVisual(trimPreviewPoint: false);
                    IsChecked = false;
                }
                else if (SelectOnMouseUp)
                {
                    SelectionVisual.SetRender(ActiveVisual);
                }
            }

            e.Handled = true;
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            if (ActiveVisual == null)
                return;

            ReplacePreviewPoint(ActiveVisual, e.GetPosition(DrawCanvas));
            RenderVisual(ActiveVisual);
            e.Handled = true;
        }

        private void ReplacePreviewPoint(TVisual visual, Point point)
        {
            IList<Point> points = GetPoints(visual);
            if (points.Count == 0)
            {
                points.Add(point);
                return;
            }

            points[points.Count - 1] = point;
        }

        private void CompleteCurrentVisual(bool trimPreviewPoint)
        {
            if (ActiveVisual == null)
            {
                return;
            }

            TVisual visual = ActiveVisual;
            if (trimPreviewPoint)
            {
                IList<Point> points = GetPoints(visual);
                if (points.Count > 0)
                {
                    points.RemoveAt(points.Count - 1);
                }
            }

            RenderVisual(visual);
            OnVisualCompleted(visual);
            ActiveVisual = null;
            _activeCreationCommand = null;
        }

        internal void CancelActiveVisual()
        {
            if (ActiveVisual == null)
            {
                return;
            }

            TVisual visual = ActiveVisual;
            ActionCommand? creationCommand = _activeCreationCommand;
            ActiveVisual = null;
            _activeCreationCommand = null;

            if (DrawCanvas.ContainsVisual(visual))
                DrawCanvas.RemoveVisual(visual);

            if (creationCommand != null)
                DrawCanvas.DiscardActionCommand(creationCommand);
        }

        public virtual void Dispose()
        {
            if (IsChecked)
            {
                IsChecked = false;
            }
            else
            {
                UnLoad();
                OnDeactivated();
            }

            GC.SuppressFinalize(this);
        }
    }
}
