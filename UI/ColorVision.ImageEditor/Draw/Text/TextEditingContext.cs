using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.Draw
{
    public sealed class TextEditingContext
    {
        public TextEditingContext(
            Guid id,
            DrawCanvas drawCanvas,
            Zoombox zoombox,
            Panel textEditorOverlay,
            SelectEditorVisual selectionVisual,
            DrawEditorManager drawEditorManager,
            ObservableCollection<IDrawingVisual> drawingVisualLists)
        {
            Id = id;
            DrawCanvas = drawCanvas;
            Zoombox = zoombox;
            TextEditorOverlay = textEditorOverlay;
            SelectionVisual = selectionVisual;
            DrawEditorManager = drawEditorManager;
            DrawingVisualLists = drawingVisualLists;
        }

        public Guid Id { get; }

        public DrawCanvas DrawCanvas { get; }

        public Zoombox Zoombox { get; }

        public Panel TextEditorOverlay { get; }

        public SelectEditorVisual SelectionVisual { get; }

        public DrawEditorManager DrawEditorManager { get; }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; }

        public double ZoomRatio => Zoombox.ContentMatrix.M11;

        public Point TranslatePointToTextEditorOverlay(Point point)
        {
            return DrawCanvas.TranslatePoint(point, TextEditorOverlay);
        }
    }
}

