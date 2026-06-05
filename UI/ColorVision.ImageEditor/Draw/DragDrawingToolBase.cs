using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.Draw
{
    public abstract class DragDrawingToolBase : DrawEditorToggleToolBase, ICompactInspectorProvider, IDisposable
    {
        private bool _isChecked;
        protected DragDrawingToolBase(DrawEditorContext editorContext)
        {
            EditorContext = editorContext;
            ToolBarLocal = ToolBarLocal.Draw;
        }

        protected DrawEditorContext EditorContext { get; }
        protected DrawCanvas DrawCanvas => EditorContext.DrawCanvas;
        protected Zoombox Zoombox => EditorContext.Zoombox;

        protected Point MouseDownPoint { get; private set; }
        protected Point MouseUpPoint { get; private set; }
        protected bool IsMouseDown { get; private set; }

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
                    AttachDragHandlers();
                    OnActivated();
                }
                else
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(null);
                    DetachDragHandlers();
                    OnDeactivated();
                }

                OnPropertyChanged();
            }
        }

        protected virtual bool IgnoreWhenCtrlPressed => false;

        public IEnumerable<CompactInspectorItem> GetCompactInspectorItems() => BuildCompactInspectorItems();

        protected virtual IEnumerable<CompactInspectorItem> BuildCompactInspectorItems() => Array.Empty<CompactInspectorItem>();

        protected virtual bool TryHandleExistingSelection(Point point)
        {
            return EditorContext.SelectionVisual.GetContainingRect(point);
        }

        protected void ClearCurrentSelection()
        {
            EditorContext.SelectionVisual.ClearRender();
        }

        protected void SelectVisual<TVisual>(TVisual visual) where TVisual : ISelectVisual
        {
            EditorContext.SelectionVisual.SetRender(visual);
        }

        protected int GetNextDrawingVisualId()
        {
            if (EditorContext.DrawingVisualLists.Count > 0 && EditorContext.DrawingVisualLists.Last() is DrawingVisualBase drawingVisual)
            {
                return drawingVisual.ID + 1;
            }

            return 1;
        }

        protected virtual void OnActivated()
        {
        }

        protected virtual void OnDeactivated()
        {
        }

        protected virtual void OnMouseEnter(MouseEventArgs e)
        {
        }

        protected virtual void OnMouseLeave(MouseEventArgs e)
        {
        }

        protected abstract void OnBeginDraw(Point startPoint, MouseButtonEventArgs e);
        protected abstract void OnUpdateDraw(Point currentPoint, MouseEventArgs e);
        protected abstract void OnEndDraw(Point endPoint, MouseButtonEventArgs e);

        private void AttachDragHandlers()
        {
            DrawCanvas.MouseMove += HandleMouseMove;
            DrawCanvas.MouseEnter += HandleMouseEnter;
            DrawCanvas.MouseLeave += HandleMouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown += HandlePreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp += HandlePreviewMouseUp;
        }

        private void DetachDragHandlers()
        {
            DrawCanvas.MouseMove -= HandleMouseMove;
            DrawCanvas.MouseEnter -= HandleMouseEnter;
            DrawCanvas.MouseLeave -= HandleMouseLeave;
            DrawCanvas.PreviewMouseLeftButtonDown -= HandlePreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp -= HandlePreviewMouseUp;
            IsMouseDown = false;
        }

        private void HandlePreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IgnoreWhenCtrlPressed && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                return;
            }

            DrawCanvas.CaptureMouse();
            MouseDownPoint = e.GetPosition(DrawCanvas);
            IsMouseDown = true;

            if (TryHandleExistingSelection(MouseDownPoint))
            {
                return;
            }

            OnBeginDraw(MouseDownPoint, e);
        }

        private void HandlePreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.ReleaseMouseCapture();
            MouseUpPoint = e.GetPosition(DrawCanvas);
            IsMouseDown = false;
            OnEndDraw(MouseUpPoint, e);
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            if (IsMouseDown)
            {
                OnUpdateDraw(e.GetPosition(DrawCanvas), e);
            }

            e.Handled = true;
        }

        private void HandleMouseEnter(object sender, MouseEventArgs e)
        {
            OnMouseEnter(e);
        }

        private void HandleMouseLeave(object sender, MouseEventArgs e)
        {
            OnMouseLeave(e);
        }

        public virtual void Dispose()
        {
            if (IsChecked)
            {
                IsChecked = false;
            }
            else
            {
                DetachDragHandlers();
                OnDeactivated();
            }

            GC.SuppressFinalize(this);
        }
    }
}
