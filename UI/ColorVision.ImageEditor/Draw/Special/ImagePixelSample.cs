using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw.Special
{
    public sealed class ImagePixelSample
    {
        public Point ViewPosition { get; init; }

        public int PixelX { get; init; }

        public int PixelY { get; init; }

        public PixelFormat PixelFormat { get; init; }

        public string ValueText { get; init; } = string.Empty;

        public Color PreviewColor { get; init; }

        public bool HasRgbSourceChannels { get; init; }

        public Point PixelPosition => new(PixelX, PixelY);

        public string CoordinateText => $"({PixelX},{PixelY})";
    }
}