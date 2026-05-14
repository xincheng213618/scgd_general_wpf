using System;
using System.Windows.Input;

namespace ColorVision.ImageEditor.Draw
{
    public abstract class RegionOperationToolBase : DrawEditorToggleToolBase, IDisposable
    {
        private bool _isChecked;

        protected RegionOperationToolBase(DrawEditorContext editorContext)
        {
            EditorContext = editorContext;
            ToolBarLocal = ToolBarLocal.Draw;
        }

        protected DrawEditorContext EditorContext { get; }
        protected DrawCanvas DrawCanvas => EditorContext.DrawCanvas;
        protected Zoombox Zoombox => EditorContext.Zoombox;

        protected virtual Cursor ActiveCursor => Cursors.Cross;
        protected virtual Cursor InactiveCursor => Cursors.Cross;

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
                    Zoombox.Cursor = ActiveCursor;
                    LoadCore();
                    OnActivated();
                }
                else
                {
                    EditorContext.DrawEditorManager.SetCurrentDrawEditor(null);
                    Zoombox.Cursor = InactiveCursor;
                    UnLoadCore();
                    OnDeactivated();
                }

                OnPropertyChanged();
            }
        }

        protected virtual void OnActivated()
        {
        }

        protected virtual void OnDeactivated()
        {
        }

        protected abstract void LoadCore();
        protected abstract void UnLoadCore();

        public virtual void Dispose()
        {
            if (IsChecked)
            {
                IsChecked = false;
            }
            else
            {
                UnLoadCore();
                OnDeactivated();
            }

            GC.SuppressFinalize(this);
        }
    }
}