using System;

namespace ColorVision.ImageEditor
{
    public sealed record ImageViewImageItem(string FilePath, string? DisplayName = null, object? Tag = null);

    public sealed class ImageViewImageChangedEventArgs : EventArgs
    {
        public ImageViewImageChangedEventArgs(ImageViewImageItem item, int index, int count, bool userInitiated)
        {
            Item = item;
            Index = index;
            Count = count;
            UserInitiated = userInitiated;
        }

        public ImageViewImageItem Item { get; }

        public int Index { get; }

        public int Count { get; }

        public bool UserInitiated { get; }
    }
}
