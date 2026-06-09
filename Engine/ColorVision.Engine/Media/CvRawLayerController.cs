#pragma warning disable CA1859,CS8604
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Layers;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    internal sealed class CvRawLayerController : IImageLayerController
    {
        private readonly ImageView _imageView;
        private readonly string _filePath;
        private readonly bool _isCie;

        private CvRawLayerController(ImageView imageView, string filePath, bool isCie, IReadOnlyList<ImageLayerDescriptor> layers)
        {
            _imageView = imageView;
            _filePath = filePath;
            _isCie = isCie;
            Layers = layers;
        }

        public IReadOnlyList<ImageLayerDescriptor> Layers { get; }

        public ImageLayerDescriptor? DefaultLayer => Layers.Count > 0 ? Layers[0] : null;

        public static IImageLayerController Create(ImageView imageView, string filePath, bool isCie, int channelCount, bool hasRgbLayers)
        {
            return new CvRawLayerController(imageView, filePath, isCie, BuildLayers(isCie, channelCount, hasRgbLayers));
        }

        public void SelectLayer(ImageLayerDescriptor layer)
        {
            switch (layer.Id)
            {
                case "composite":
                    ShowLayer(CVFileUtil.OpenLocalFileChannel(_filePath, CVImageChannelType.SRC).ToWriteableBitmap());
                    return;
                case "red":
                case "green":
                case "blue":
                    ShowCompositeChannel(layer.SourceChannelIndex ?? 0);
                    return;
                case "cie-x":
                    ShowLayer(CVFileUtil.OpenLocalFileChannel(_filePath, CVImageChannelType.CieXyzX).ToWriteableBitmap());
                    return;
                case "cie-y":
                    ShowLayer(CVFileUtil.OpenLocalFileChannel(_filePath, CVImageChannelType.CieXyzY).ToWriteableBitmap());
                    return;
                case "cie-z":
                    ShowLayer(CVFileUtil.OpenLocalFileChannel(_filePath, CVImageChannelType.CieXyzZ).ToWriteableBitmap());
                    return;
                default:
                    return;
            }
        }

        private void ShowCompositeChannel(int channelIndex)
        {
            ShowLayer(CVFileUtil.OpenLocalFileChannel(_filePath, CVImageChannelType.SRC).ToWriteableBitmap());
            _imageView.ExtractChannel(channelIndex);
        }

        private void ShowLayer(WriteableBitmap writeableBitmap)
        {
            _imageView.SetImageSource(writeableBitmap, _imageView.EnableEditorImageServices, configureDefaultLayerController: false);
        }

        private static IReadOnlyList<ImageLayerDescriptor> BuildLayers(bool isCie, int channelCount, bool hasRgbLayers)
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

            if (!isCie)
            {
                if (channelCount >= 3)
                {
                    layers.AddRange(CreateRgbLayers());
                }

                return layers;
            }

            if (channelCount >= 3 && hasRgbLayers)
            {
                layers.AddRange(CreateRgbLayers());
            }

            if (channelCount >= 3)
            {
                layers.Add(new ImageLayerDescriptor
                {
                    Id = "cie-x",
                    DisplayName = "CIE X",
                    Kind = ImageLayerKind.Derived,
                });
                layers.Add(new ImageLayerDescriptor
                {
                    Id = "cie-y",
                    DisplayName = "CIE Y",
                    Kind = ImageLayerKind.Derived,
                });
                layers.Add(new ImageLayerDescriptor
                {
                    Id = "cie-z",
                    DisplayName = "CIE Z",
                    Kind = ImageLayerKind.Derived,
                });
            }
            else if (channelCount == 1 && hasRgbLayers)
            {
                layers.Add(new ImageLayerDescriptor
                {
                    Id = "cie-y",
                    DisplayName = "Luminance",
                    Kind = ImageLayerKind.Derived,
                });
            }

            return layers;
        }

        private static IEnumerable<ImageLayerDescriptor> CreateRgbLayers()
        {
            yield return new ImageLayerDescriptor
            {
                Id = "red",
                DisplayName = "Red",
                Kind = ImageLayerKind.Channel,
                SourceChannelIndex = 0,
            };
            yield return new ImageLayerDescriptor
            {
                Id = "green",
                DisplayName = "Green",
                Kind = ImageLayerKind.Channel,
                SourceChannelIndex = 1,
            };
            yield return new ImageLayerDescriptor
            {
                Id = "blue",
                DisplayName = "Blue",
                Kind = ImageLayerKind.Channel,
                SourceChannelIndex = 2,
            };
        }
    }
}