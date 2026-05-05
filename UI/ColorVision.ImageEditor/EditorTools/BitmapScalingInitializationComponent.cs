using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Settings;

namespace ColorVision.ImageEditor.EditorTools
{
    public class BitmapScalingInitializationComponent : IImageComponent
    {
        public void Execute(ImageView imageView)
        {
            ImageViewBitmapScalingService.Initialize(imageView);
        }
    }
}