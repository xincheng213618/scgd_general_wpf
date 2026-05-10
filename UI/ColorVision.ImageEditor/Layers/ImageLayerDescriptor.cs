using System.Collections.Generic;

namespace ColorVision.ImageEditor.Layers
{
    public enum ImageLayerKind
    {
        Composite,
        Channel,
        Derived,
    }

    public sealed record class ImageLayerDescriptor
    {
        public required string Id { get; init; }

        public required string DisplayName { get; init; }

        public ImageLayerKind Kind { get; init; }

        public int? SourceChannelIndex { get; init; }
    }

    public interface IImageLayerController
    {
        IReadOnlyList<ImageLayerDescriptor> Layers { get; }

        ImageLayerDescriptor? DefaultLayer { get; }

        void SelectLayer(ImageLayerDescriptor layer);
    }
}