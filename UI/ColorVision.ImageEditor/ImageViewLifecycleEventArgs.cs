using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor;

public sealed class ImageViewImageSourceLoadedEventArgs : EventArgs
{
    internal ImageViewImageSourceLoadedEventArgs(ImageSource source, long imageRevision)
    {
        Source = source;
        ImageRevision = imageRevision;
    }

    public ImageSource Source { get; }

    public long ImageRevision { get; }
}

public sealed class ImageViewExternalRenderCompletedEventArgs : EventArgs
{
    internal ImageViewExternalRenderCompletedEventArgs(
        ImageSource? source,
        long imageRevision,
        object? context,
        bool succeeded)
    {
        Source = source;
        ImageRevision = imageRevision;
        Context = context;
        Succeeded = succeeded;
    }

    public ImageSource? Source { get; }

    public long ImageRevision { get; }

    public object? Context { get; }

    public bool Succeeded { get; }
}
