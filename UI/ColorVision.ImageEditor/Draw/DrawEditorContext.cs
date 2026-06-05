using ColorVision.ImageEditor.Draw.Special;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.ImageEditor.Draw
{
    public sealed class DrawEditorContext
    {
        public DrawEditorContext(DrawCanvas drawCanvas, Zoombox zoombox, Guid? id = null)
        {
            ArgumentNullException.ThrowIfNull(drawCanvas);
            ArgumentNullException.ThrowIfNull(zoombox);

            Id = id ?? Guid.NewGuid();
            DrawCanvas = drawCanvas;
            Zoombox = zoombox;
            MouseInfoProvider = new ImageMouseInfoProvider(this);
        }

        public Guid Id { get; }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        public DrawCanvas DrawCanvas { get; }

        public Zoombox Zoombox { get; }

        public ImageMouseInfoProvider MouseInfoProvider { get; }

        public SelectEditorVisual SelectionVisual { get; set; } = null!;

        public event EventHandler<bool>? ImageEditModeChanged;

        public bool IsImageEditMode
        {
            get => _isImageEditMode;
            set
            {
                if (_isImageEditMode == value)
                {
                    return;
                }

                _isImageEditMode = value;
                ImageEditModeChanged?.Invoke(this, value);
            }
        }
        private bool _isImageEditMode;

        public double ZoomRatio => Zoombox.ContentMatrix.M11;

        public DrawEditorManager DrawEditorManager { get; } = new DrawEditorManager();
    }
}
