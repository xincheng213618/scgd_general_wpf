using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Documents
{
    /// <summary>Maps supported source pixels to the editor's existing metadata contract.</summary>
    internal static class ImageSourceMetadata
    {
        internal static bool TryApply(ImageSource? source, ImageViewConfig config)
        {
            if (source is not WriteableBitmap bitmap) return true;
            if (!TryGetPixelLayout(bitmap.Format, out int channels, out int depth)) return false;

            int cols = bitmap.PixelWidth;
            int rows = bitmap.PixelHeight;
            // This metadata describes tightly packed source pixels, not WPF BackBufferStride.
            int stride = cols * channels * (depth / 8);
            config.SetImageMetadata(ImageViewPropertyKeys.PixelFormat, bitmap.Format, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_PixelFormat);
            config.SetImageMetadata(ImageViewPropertyKeys.Cols, cols, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_Cols);
            config.SetImageMetadata(ImageViewPropertyKeys.Rows, rows, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_Rows);
            config.SetImageMetadata(ImageViewPropertyKeys.Channel, channels, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_Channel);
            config.SetImageMetadata(ImageViewPropertyKeys.Depth, depth, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_Depth);
            config.SetImageMetadata(ImageViewPropertyKeys.Stride, stride, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_Stride);
            config.SetImageMetadata(ImageViewPropertyKeys.DpiX, bitmap.DpiX, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_DpiX);
            config.SetImageMetadata(ImageViewPropertyKeys.DpiY, bitmap.DpiY, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_DpiY);
            return true;
        }

        private static bool TryGetPixelLayout(PixelFormat format, out int channels, out int depth)
        {
            (channels, depth) = format.ToString() switch
            {
                "Bgr32" or "Bgra32" or "Pbgra32" => (4, 8),
                "Bgr24" or "Rgb24" => (3, 8),
                "Indexed8" or "Gray8" => (1, 8),
                "Rgb48" => (3, 16),
                "Gray16" => (1, 16),
                "Gray32Float" => (1, 32),
                _ => (0, 0),
            };
            return channels != 0;
        }
    }
}
