using System;

namespace ColorVision.ImageEditor.EditorTools
{
    public sealed class ImageEditModeEditorTool : IEditorToggleToolBase, IDisposable
    {
        private readonly EditorContext _context;

        public ImageEditModeEditorTool(EditorContext context)
        {
            _context = context;
            ToolBarLocal = ToolBarLocal.Top;
            Order = -100;
            Icon = IEditorToolFactory.TryFindResource("DrawingImagedrag");
            _context.ImageEditModeChanged += Context_ImageEditModeChanged;
        }

        public override string? GuidId => "ImageEditMode";

        public override bool IsChecked
        {
            get => _context.IsImageEditMode;
            set
            {
                if (_context.IsImageEditMode == value)
                {
                    return;
                }

                _context.SetImageEditMode(value);
                OnPropertyChanged();
            }
        }

        private void Context_ImageEditModeChanged(object? sender, bool e)
        {
            OnPropertyChanged(nameof(IsChecked));
        }

        public void Dispose()
        {
            _context.ImageEditModeChanged -= Context_ImageEditModeChanged;
            GC.SuppressFinalize(this);
        }
    }
}
