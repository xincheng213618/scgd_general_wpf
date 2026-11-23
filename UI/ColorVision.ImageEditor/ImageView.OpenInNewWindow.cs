using System;
using System.IO;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor;

public partial class ImageView
{
    private void CanOpenCurrentImageInNewWindow(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = !_document.IsDisposed && Presentation.DisplaySource is BitmapSource;
        e.Handled = true;
    }

    private async void OpenCurrentImageInNewWindow(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
        if (_document.IsDisposed || Presentation.DisplaySource is not BitmapSource bitmap) return;

        var owner = EditorContext.OwnerWindow;
        if (GetDisplayedFilePath(bitmap) is { } path)
        {
            await ImageViewWindow.OpenFileAsync(path, owner);
            return;
        }

        ImageViewWindow.Show(view => view.OpenImage(new WriteableBitmap(bitmap)), owner);
    }

    private string? GetDisplayedFilePath(BitmapSource bitmap)
    {
        if (!ReferenceEquals(ViewBitmapSource, bitmap) || SelectedLayer != _layerController?.DefaultLayer
            || !ImageOpenCompletion.IsCompletedSuccessfully || EditorContext.IImageOpen is not { } opener)
            return null;

        // The requested path can outlive the pixels. Use only provenance owned by the current image.
        string? loadedPath = ImageContent?.FilePath;
        if (loadedPath == null && ReferenceEquals(ImageContentCapability, opener))
            loadedPath = Config.GetProperties<string>(ImageViewPropertyKeys.FileSource);

        return string.Equals(loadedPath, Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), StringComparison.OrdinalIgnoreCase)
            && File.Exists(loadedPath) ? loadedPath : null;
    }
}
