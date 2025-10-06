
namespace ColorVision.ImageEditor
{
    public class EditorContext
    {
        public ImageViewModel ImageViewModel { get; set; }

        public ImageViewConfig ImageViewConfig { get; set; }

        public DrawCanvas DrawCanvas { get; set; }
        public Zoombox ZoomboxSub { get; set; }
    }

}
