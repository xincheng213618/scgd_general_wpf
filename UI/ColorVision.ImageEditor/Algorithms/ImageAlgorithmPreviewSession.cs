#pragma warning disable CS8625
using ColorVision.Core;
using OpenCvSharp;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Algorithms
{
    internal sealed class ImageAlgorithmPreviewSession
    {
        private readonly ImageProcessingContext _image;
        private readonly BitmapSource _originalSource;
        private readonly long _originalRevision;
        private bool _isCompleted;
        private bool _needsRestore;

        private ImageAlgorithmPreviewSession(ImageProcessingContext image, BitmapSource originalSource, WriteableBitmap previewBitmap)
        {
            _image = image;
            _originalSource = originalSource;
            _originalRevision = image?.ImageRevision ?? 0;
            PreviewBitmap = previewBitmap;
        }

        public WriteableBitmap PreviewBitmap { get; }

        public static ImageAlgorithmPreviewSession Start(ImageProcessingContext image)
        {
            ImageSource source = image.ViewBitmapSource ?? image.ImageShow.Source;
            if (source is not BitmapSource bitmapSource)
            {
                throw new InvalidOperationException("Current image source is not a bitmap.");
            }

            WriteableBitmap preview = new WriteableBitmap(bitmapSource);
            image.FunctionImage = preview;
            image.ImageShow.Source = preview;
            return new ImageAlgorithmPreviewSession(image, bitmapSource, preview);
        }

        public void Apply(Action<Mat> apply)
        {
            if (!TryContinue())
            {
                return;
            }

            PreviewBitmap.Lock();
            try
            {
                if (_needsRestore)
                {
                    CopyOriginalPixels();
                }

                _needsRestore = true;
                PreviewBitmap.AddDirtyRect(new Int32Rect(0, 0, PreviewBitmap.PixelWidth, PreviewBitmap.PixelHeight));
                MatType matType = GetMatType(PreviewBitmap.Format);
                using Mat mat = Mat.FromPixelData(PreviewBitmap.PixelHeight, PreviewBitmap.PixelWidth, matType, PreviewBitmap.BackBuffer, PreviewBitmap.BackBufferStride);
                apply(mat);
            }
            finally
            {
                PreviewBitmap.Unlock();
            }
        }

        public void Commit()
        {
            if (!TryContinue())
            {
                return;
            }

            _image.ViewBitmapSource = PreviewBitmap;
            _image.ImageShow.Source = _image.ViewBitmapSource;
            _image.NotifySourcePixelsChanged();
            _image.FunctionImage = null;
            _isCompleted = true;
        }

        public void ShowOriginal()
        {
            if (!TryContinue())
            {
                return;
            }

            RestoreOriginal();
        }

        public void Cancel()
        {
            if (_isCompleted)
            {
                return;
            }

            _image.ImageShow.Source = _image.ViewBitmapSource;
            _image.FunctionImage = null;
            _isCompleted = true;
        }

        public void CancelIfActive()
        {
            if (!_isCompleted)
            {
                Cancel();
            }
        }

        private void RestoreOriginal()
        {
            if (!_needsRestore)
            {
                return;
            }

            PreviewBitmap.Lock();
            try
            {
                CopyOriginalPixels();
                PreviewBitmap.AddDirtyRect(new Int32Rect(0, 0, PreviewBitmap.PixelWidth, PreviewBitmap.PixelHeight));
                _needsRestore = false;
            }
            finally
            {
                PreviewBitmap.Unlock();
            }
        }

        private bool TryContinue()
        {
            if (_isCompleted)
            {
                return false;
            }

            if (_image is not null && !_image.IsCurrentImageRevision(_originalRevision))
            {
                Cancel();
                return false;
            }

            return true;
        }

        private void CopyOriginalPixels()
        {
            int bufferSize = checked(PreviewBitmap.BackBufferStride * PreviewBitmap.PixelHeight);
            _originalSource.CopyPixels(Int32Rect.Empty, PreviewBitmap.BackBuffer, bufferSize, PreviewBitmap.BackBufferStride);
        }

        private static MatType GetMatType(PixelFormat pixelFormat)
        {
            if (pixelFormat == PixelFormats.Gray8 || pixelFormat == PixelFormats.Indexed8)
            {
                return MatType.CV_8UC1;
            }

            if (pixelFormat == PixelFormats.Gray16)
            {
                return MatType.CV_16UC1;
            }

            if (pixelFormat == PixelFormats.Gray32Float)
            {
                return MatType.CV_32FC1;
            }

            if (pixelFormat == PixelFormats.Bgr24 || pixelFormat == PixelFormats.Rgb24)
            {
                return MatType.CV_8UC3;
            }

            if (pixelFormat == PixelFormats.Rgb48)
            {
                return MatType.CV_16UC3;
            }

            if (pixelFormat == PixelFormats.Bgr32 || pixelFormat == PixelFormats.Bgra32 || pixelFormat == PixelFormats.Pbgra32)
            {
                return MatType.CV_8UC4;
            }

            if (pixelFormat == PixelFormats.Prgba64)
            {
                return MatType.CV_16UC4;
            }

            throw new NotSupportedException($"Unsupported pixel format: {pixelFormat}");
        }
    }
}
