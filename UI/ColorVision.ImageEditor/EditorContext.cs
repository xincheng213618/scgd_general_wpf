using ColorVision.ImageEditor.Abstractions;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.ImageEditor
{
    public class EditorContext
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public ContextMenu ContextMenu { get; set; } = new ContextMenu();
        public IImageOpen? IImageOpen { get; set; }

        public ImageView ImageView { get; set; }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        public ImageViewModel ImageViewModel { get; set; }

        public ImageViewConfig Config { get; set; }  = new ImageViewConfig();

        public DrawCanvas DrawCanvas { get; set; }

        public Zoombox Zoombox { get; set; }

        public double ZoomRatio => Zoombox.ContentMatrix.M11;

        public DrawEditorManager DrawEditorManager { get; init; } = new DrawEditorManager();

        public IEditorToolFactory IEditorToolFactory { get; set; }

    }

}
