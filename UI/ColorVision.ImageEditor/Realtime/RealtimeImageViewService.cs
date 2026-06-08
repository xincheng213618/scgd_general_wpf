using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        public RealtimeFrameStats Stats => Presenter.Stats;

        public bool IsFrozen
        {
            get => Options.IsFrozen;
            set
            {
                Options.IsFrozen = value;
                if (_presenter != null)
                {
                    _presenter.Stats.Refresh(value);
                }
            }
        }

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
            ArgumentNullException.ThrowIfNull(options);
            Options.ApplyFrom(options);
            _presenter?.Configure(Options);
        }

        public bool SubmitFrame(IntPtr sourcePointer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, RealtimeFrameTransform transform = RealtimeFrameTransform.None)
        {
            return Presenter.SubmitFrame(sourcePointer, width, height, pixelFormat, sourceStride, bufferLength, transform);
        }

        public bool SubmitFrame(byte[] sourceBuffer, int width, int height, PixelFormat pixelFormat, int sourceStride = 0, int bufferLength = 0, RealtimeFrameTransform transform = RealtimeFrameTransform.None)
        {
            return Presenter.SubmitFrame(sourceBuffer, width, height, pixelFormat, sourceStride, bufferLength, transform);
        }

        public RealtimeFrameSnapshot? CaptureCurrentFrame()
        {
            return _presenter?.CaptureCurrentFrame();
        }

        public BitmapSource? CaptureDisplayedBitmap()
        {
            if (!_imageView.Dispatcher.CheckAccess())
            {
                return _imageView.Dispatcher.Invoke(CaptureDisplayedBitmap);
            }

            if (_imageView.ImageShow.Source is not BitmapSource source)
            {
                return null;
            }

            BitmapSource snapshot = source.Clone();
            if (snapshot.CanFreeze)
            {
                snapshot.Freeze();
            }

            return snapshot;
        }

        public bool SaveDisplayedPng(string fileName)
        {
            BitmapSource? snapshot = CaptureDisplayedBitmap();
            if (snapshot == null)
            {
                return false;
            }

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(snapshot));

            using FileStream stream = new(fileName, FileMode.Create, FileAccess.Write);
            encoder.Save(stream);
            return true;
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
