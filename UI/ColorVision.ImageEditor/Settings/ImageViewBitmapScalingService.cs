using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Settings
{
    public static class ImageViewBitmapScalingService
    {
        public static void Apply(ImageView imageView, BitmapScalingMode bitmapScalingMode)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            RenderOptions.SetBitmapScalingMode(imageView.ImageShow, bitmapScalingMode);
        }

        public static BitmapScalingMode GetCurrent(ImageView imageView)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            return RenderOptions.GetBitmapScalingMode(imageView.ImageShow);
        }

        public static void Initialize(ImageView imageView)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            Apply(imageView, DefaultBitmapScalingConfig.Current.DefaultBitmapScalingMode);
        }
    }
}