using ColorVision.ImageEditor.Draw.Special;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.Draw
{
    public sealed class DrawEditorContext
    {
        private static Panel CreateFallbackTextEditorOverlay()
        {
            return new Canvas
            {
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
            };
        }

        public DrawEditorContext(Guid? id = null)
        {
            Id = id ?? Guid.NewGuid();
            MouseInfoProvider = new ImageMouseInfoProvider(this);
            TextEditorOverlay = CreateFallbackTextEditorOverlay();
        }

        public DrawEditorContext(DrawCanvas drawCanvas, Zoombox zoombox, Panel? textEditorOverlay = null, Guid? id = null)
            : this(id)
        {
            DrawCanvas = drawCanvas;
            Zoombox = zoombox;
            if (textEditorOverlay != null)
            {
                TextEditorOverlay = textEditorOverlay;
            }
        }

        public Guid Id { get; }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        public DrawCanvas DrawCanvas { get; set; } = null!;

        public Zoombox Zoombox { get; set; } = null!;

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

        public Panel TextEditorOverlay { get; set; }

        public Point TranslatePointToTextEditorOverlay(Point point)
        {
            return DrawCanvas.TranslatePoint(point, TextEditorOverlay);
        }

        public double ZoomRatio => Zoombox.ContentMatrix.M11;

        public DrawEditorManager DrawEditorManager { get; } = new DrawEditorManager();
    }
}