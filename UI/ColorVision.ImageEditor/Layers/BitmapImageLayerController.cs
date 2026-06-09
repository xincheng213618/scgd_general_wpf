#pragma warning disable CA1859
using System.Collections.Generic;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Layers
{
    internal sealed class BitmapImageLayerController : IImageLayerController
    {
        private readonly ImageView _imageView;

        private BitmapImageLayerController(ImageView imageView, IReadOnlyList<ImageLayerDescriptor> layers)
        {
            _imageView = imageView;
            Layers = layers;
        }

        public IReadOnlyList<ImageLayerDescriptor> Layers { get; }

        public ImageLayerDescriptor? DefaultLayer => Layers.Count > 0 ? Layers[0] : null;

        public static IImageLayerController CreateForCurrentImage(ImageView imageView)
        {
            int channelCount = imageView.Config.GetProperties<int>(ImageViewPropertyKeys.Channel);
            PixelFormat pixelFormat = imageView.Config.GetProperties<PixelFormat>(ImageViewPropertyKeys.PixelFormat);
            return new BitmapImageLayerController(imageView, BuildLayers(channelCount, pixelFormat));
        }

        public void SelectLayer(ImageLayerDescriptor layer)
        {
            _imageView.ExtractChannel(layer.SourceChannelIndex ?? -1);
        }

        private static IReadOnlyList<ImageLayerDescriptor> BuildLayers(int channelCount, PixelFormat pixelFormat)
        {
            List<ImageLayerDescriptor> layers = new()
            {
                new ImageLayerDescriptor
                {
                    Id = "composite",
                    DisplayName = "Composite",
                    Kind = ImageLayerKind.Composite,
                }
            };

            if (channelCount < 3)
            {
                return layers;
            }

            if (!TryGetRgbChannelMap(pixelFormat, out int redIndex, out int greenIndex, out int blueIndex))
            {
                return layers;
            }

            layers.Add(new ImageLayerDescriptor
            {
                Id = "red",
                DisplayName = "Red",
                Kind = ImageLayerKind.Channel,
                SourceChannelIndex = redIndex,
            });
            layers.Add(new ImageLayerDescriptor
            {
                Id = "green",
                DisplayName = "Green",
                Kind = ImageLayerKind.Channel,
                SourceChannelIndex = greenIndex,
            });
            layers.Add(new ImageLayerDescriptor
            {
                Id = "blue",
                DisplayName = "Blue",
                Kind = ImageLayerKind.Channel,
                SourceChannelIndex = blueIndex,
            });

            return layers;
        }

        private static bool TryGetRgbChannelMap(PixelFormat pixelFormat, out int redIndex, out int greenIndex, out int blueIndex)
        {
            if (pixelFormat == PixelFormats.Bgr24 || pixelFormat == PixelFormats.Bgr32 || pixelFormat == PixelFormats.Bgra32 || pixelFormat == PixelFormats.Pbgra32)
            {
                redIndex = 2;
                greenIndex = 1;
                blueIndex = 0;
                return true;
            }

            if (pixelFormat == PixelFormats.Rgb24 || pixelFormat == PixelFormats.Rgb48 || pixelFormat == PixelFormats.Rgba64)
            {
                redIndex = 0;
                greenIndex = 1;
                blueIndex = 2;
                return true;
            }

            redIndex = -1;
            greenIndex = -1;
            blueIndex = -1;
            return false;
        }
    }
}