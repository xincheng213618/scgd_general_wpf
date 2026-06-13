using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeImageViewService : IDisposable
    {
        private readonly RealtimeFramePresenter _presenter;

        public RealtimeImageViewService(ImageView imageView)
        {
            Options = new RealtimeFrameOptions();
            _presenter = new RealtimeFramePresenter(imageView ?? throw new ArgumentNullException(nameof(imageView)), Options);
        }

        public RealtimeFrameOptions Options { get; }

        public void Configure(RealtimeFrameOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            Options.ApplyFrom(options);
            _presenter.Configure(Options);
        }

        public bool SubmitFrame(IntPtr sourcePointer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, int transform = RealtimeFramePresenter.TransformNone)
        {
            return _presenter.SubmitFrame(sourcePointer, width, height, pixelFormat, sourceStride, bufferLength, transform);
        }

        public bool SubmitFrame(byte[] sourceBuffer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, int transform = RealtimeFramePresenter.TransformNone)
        {
            return _presenter.SubmitFrame(sourceBuffer, width, height, pixelFormat, sourceStride, bufferLength, transform);
        }

        public void Reset(bool clearImageSource = false)
        {
            _presenter.Reset(clearImageSource);
        }

        public void Dispose()
        {
            _presenter.Dispose();
        }
    }
}
