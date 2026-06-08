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

        public RealtimeFrameOptions Options { get; }

        private RealtimeFramePresenter Presenter => _presenter ??= new RealtimeFramePresenter(_imageView, Options);

        public void Configure(RealtimeFrameOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Options.ApplyFrom(options);
            _presenter?.Configure(Options);
        }

        public bool SubmitFrame(IntPtr sourcePointer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, int transform = RealtimeFramePresenter.TransformNone)
        {
            return Presenter.SubmitFrame(sourcePointer, width, height, pixelFormat, sourceStride, bufferLength, transform);
        }

        public bool SubmitFrame(byte[] sourceBuffer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, int transform = RealtimeFramePresenter.TransformNone)
        {
            return Presenter.SubmitFrame(sourceBuffer, width, height, pixelFormat, sourceStride, bufferLength, transform);
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
