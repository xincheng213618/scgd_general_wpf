using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Settings;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools
{
    public class BitmapScalingInitializationComponent : IImageComponent
    {
        public void Execute(ImageView imageView)
        {
            RenderOptions.SetBitmapScalingMode(imageView.ImageShow, DefaultBitmapScalingConfig.Current.DefaultBitmapScalingMode);
        }
    }
}