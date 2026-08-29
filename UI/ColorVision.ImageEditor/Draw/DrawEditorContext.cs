using ColorVision.ImageEditor.Draw.Special;
using System;
using System.Collections.Generic;
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

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new BulkObservableCollection<IDrawingVisual>();

        internal void AddDrawingVisuals(IReadOnlyList<IDrawingVisual> visuals)
        {
            if (DrawingVisualLists is BulkObservableCollection<IDrawingVisual> bulkCollection)
            {
                bulkCollection.AddRange(visuals);
                return;
            }

            foreach (IDrawingVisual visual in visuals)
            {
                DrawingVisualLists.Add(visual);
            }
        }

        public DrawCanvas DrawCanvas { get; }

        public Zoombox Zoombox { get; }

        internal ImageProcessingContext? ProcessingContext { get; set; }

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
