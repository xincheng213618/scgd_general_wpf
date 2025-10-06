
using ColorVision.Core;

namespace ColorVision.ImageEditor
{
    public class EditorContext
    {
        public ImageView? ImageView { get; set; }

        public ImageViewModel ImageViewModel { get; set; }

        public ImageViewConfig Config { get; set; }

        public DrawCanvas DrawCanvas { get; set; }

        public Zoombox Zoombox { get; set; }
    }

}
