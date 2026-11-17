using OpenCvSharp;
using System;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum ImageColorChannel
    {
        Gray = -1,
        Blue = 0,
        Green = 1,
        Red = 2,
    }

    public static class ImageChannelProcessor
    {
        /// <summary>Writes a grayscale conversion or a single BGR channel at the source bit depth.</summary>
        public static void Select(Mat source, Mat destination, ImageColorChannel channel, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = ResolveIndex(source, channel);
            if (source.Channels() == 1)
                source.CopyTo(destination);
            else if (index < 0)
                Cv2.CvtColor(source, destination, source.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
            else
                Cv2.ExtractChannel(source, destination, index);
            cancellationToken.ThrowIfCancellationRequested();
        }

        internal static int ResolveIndex(Mat source, ImageColorChannel channel)
        {
            if (source.Empty()) throw new ArgumentException("An image is required.", nameof(source));
            if (!Enum.IsDefined(channel)) throw new ArgumentOutOfRangeException(nameof(channel));

            int channels = source.Channels();
            if (channels == 1 && channel == ImageColorChannel.Gray) return 0;
            if (channels is 3 or 4) return (int)channel;
            throw new ArgumentException("R, G and B require a color image; grayscale conversion supports 1, 3 or 4 channels.", nameof(source));
        }
    }
}
