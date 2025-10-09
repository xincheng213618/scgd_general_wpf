
using ColorVision.Core;
using System;

namespace ColorVision.ImageEditor
{
    public class EditorContext
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public ImageView? ImageView { get; set; }

        public ImageViewModel ImageViewModel { get; set; }

        public ImageViewConfig Config { get; init; }  = new ImageViewConfig();

        public DrawCanvas DrawCanvas { get; set; }

        public Zoombox Zoombox { get; set; }

        public DrawEditorManager DrawEditorManager { get; init; } = new DrawEditorManager();

    }

}
