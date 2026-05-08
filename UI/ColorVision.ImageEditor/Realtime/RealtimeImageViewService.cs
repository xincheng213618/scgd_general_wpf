using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeImageViewService : IDisposable
    {
        private readonly ImageView _imageView;
        private RealtimeFramePresenter? _presenter;

        public RealtimeImageViewService(ImageView imageView)
        {
            _imageView = imageView ?? throw new ArgumentNullException(nameof(imageView));
            Options = new RealtimeFrameOptions();
        }

        public RealtimeFrameOptions Options { get; private set; }

        private RealtimeFramePresenter Presenter => _presenter ??= new RealtimeFramePresenter(_imageView, Options);

        public event EventHandler<RealtimeFrameRenderedEventArgs> FrameRendered
        {
            add => Presenter.FrameRendered += value;
            remove
            {
                if (_presenter != null)
                {
                    _presenter.FrameRendered -= value;
                }
            }
        }

        public void Configure(RealtimeFrameOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _presenter?.Configure(options);
        }

        public bool SubmitFrame(IntPtr sourcePointer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0)
        {
            return Presenter.SubmitFrame(sourcePointer, width, height, pixelFormat, sourceStride, bufferLength);
        }

        public bool SubmitFrame(byte[] sourceBuffer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0)
        {
            return Presenter.SubmitFrame(sourceBuffer, width, height, pixelFormat, sourceStride, bufferLength);
        }

        public void AddOverlayVisual(Visual visual)
        {
            _imageView.ImageShow.AddOverlayVisual(visual);
        }

        public void RemoveOverlayVisual(Visual? visual)
        {
            _imageView.ImageShow.RemoveOverlayVisual(visual);
        }

        public void ClearOverlayVisuals()
        {
            _imageView.ImageShow.ClearOverlayVisuals();
        }

        public void Reset(bool clearImageSource = false)
        {
            _presenter?.Reset(clearImageSource);
        }

        public void Dispose()
        {
            _presenter?.Dispose();
            _presenter = null;
        }
    }
}
