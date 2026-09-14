using ColorVision.ImageEditor;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Pattern;

public partial class PatternWindow
{
    private Size _previewImageSize = Size.Empty;
    private bool _previewLayoutQueued;
    private bool _updatingPreviewLayout;
    private bool _previewLayoutDisposed;

    private void InitializePreviewLayout()
    {
        imgDisplay.ImageSourceLoaded += PreviewImageLoaded;
        imgDisplay.ClearImageEventHandler += PreviewImageCleared;
        Loaded += PreviewWindowLoaded;
        SizeChanged += PreviewWindowSizeChanged;
        StateChanged += PreviewWindowStateChanged;
    }

    private void PreviewWindowLoaded(object sender, RoutedEventArgs e) => RequestPreviewLayout();
    private void PreviewWindowSizeChanged(object sender, SizeChangedEventArgs e) => RequestPreviewLayout();
    private void PreviewWindowStateChanged(object? sender, EventArgs e) => RequestPreviewLayout();
    private void LibrarySplitter_DragCompleted(object sender, DragCompletedEventArgs e) => RequestPreviewLayout();

    private void PreviewImageLoaded(object? sender, ImageViewImageSourceLoadedEventArgs e)
    {
        // Follow the displayed image, not parameters that may not have been generated yet.
        _previewImageSize = e.Source is BitmapSource bitmap
            ? new Size(bitmap.PixelWidth, bitmap.PixelHeight)
            : new Size(e.Source.Width, e.Source.Height);
        RequestPreviewLayout();
    }

    private void PreviewImageCleared(object? sender, EventArgs e)
    {
        _previewImageSize = Size.Empty;
        SaveImageButton.IsEnabled = false;
        RequestPreviewLayout();
    }

    private void RequestPreviewLayout()
    {
        if (!IsLoaded || _previewLayoutDisposed || _previewLayoutQueued || _updatingPreviewLayout)
            return;

        _previewLayoutQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _previewLayoutQueued = false;
            if (!_previewLayoutDisposed)
                UpdatePreviewLayout();
        }));
    }

    private void UpdatePreviewLayout()
    {
        if (WindowState == WindowState.Minimized || WorkspaceGrid.ActualHeight <= 0)
            return;

        _updatingPreviewLayout = true;
        try
        {
            Rect workArea = GetPreviewWorkArea();
            if (WindowState == WindowState.Normal && Height > workArea.Height)
            {
                Height = workArea.Height;
                UpdateLayout();
            }

            double chromeWidth = ActualWidth - WorkspaceGrid.ActualWidth;
            double editorWidth = LibraryColumn.ActualWidth + 8 + ParameterColumn.ActualWidth;
            bool hasImage = !_previewImageSize.IsEmpty && _previewImageSize.Width > 0 && _previewImageSize.Height > 0;
            double previewWidth = 0;
            if (hasImage)
            {
                const double border = 2;
                double availableWidth = (WindowState == WindowState.Normal ? workArea.Width : ActualWidth) - chromeWidth - editorWidth - 8 - border;
                double availableHeight = WorkspaceGrid.ActualHeight - border;
                double scale = Math.Min(Math.Max(1, availableWidth) / _previewImageSize.Width, Math.Max(1, availableHeight) / _previewImageSize.Height);
                DisplayGrid.Width = _previewImageSize.Width * scale;
                DisplayGrid.Height = _previewImageSize.Height * scale;
                previewWidth = DisplayGrid.Width + border;
                PreviewPane.Width = previewWidth;
                PreviewPane.Height = DisplayGrid.Height + border;
            }

            PreviewPane.Visibility = hasImage ? Visibility.Visible : Visibility.Collapsed;
            PreviewGapColumn.Width = new GridLength(hasImage ? 8 : 0);
            PreviewColumn.Width = new GridLength(previewWidth);

            // Height determines the preview width. A maximized window keeps its state;
            // only its image viewport is fitted to the remaining space.
            if (WindowState == WindowState.Normal)
            {
                Width = editorWidth + chromeWidth + (hasImage ? 8 + previewWidth : 0);
                Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
                Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
            }

            UpdateLayout();
            if (hasImage)
                imgDisplay.Zoombox1.ZoomUniform();
        }
        finally
        {
            _updatingPreviewLayout = false;
        }
    }

    private Rect GetPreviewWorkArea()
    {
        var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
        var bounds = screen.WorkingArea;
        Matrix fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var workArea = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        workArea.Transform(fromDevice);
        return workArea;
    }

    private void DisposePreviewLayout()
    {
        _previewLayoutDisposed = true;
        imgDisplay.ImageSourceLoaded -= PreviewImageLoaded;
        imgDisplay.ClearImageEventHandler -= PreviewImageCleared;
        Loaded -= PreviewWindowLoaded;
        SizeChanged -= PreviewWindowSizeChanged;
        StateChanged -= PreviewWindowStateChanged;
    }
}
