#pragma warning disable CS8625
using ColorVision.Core;
using OpenCvSharp;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Algorithms
{
    internal sealed class ImageAlgorithmPreviewSession
    {
        private readonly ImageProcessingContext _image;
        private readonly byte[] _originalPixels;
        private bool _isCompleted;

        private ImageAlgorithmPreviewSession(ImageProcessingContext image, WriteableBitmap previewBitmap)
        {
            _image = image;
            PreviewBitmap = previewBitmap;
            _originalPixels = CopyPixels(previewBitmap);
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
            return new ImageAlgorithmPreviewSession(image, preview);
        }

        public void Apply(Action<Mat> apply)
        {
            RestoreOriginal();
            PreviewBitmap.Lock();
            try
            {
                MatType matType = GetMatType(PreviewBitmap.Format);
                using Mat mat = Mat.FromPixelData(PreviewBitmap.PixelHeight, PreviewBitmap.PixelWidth, matType, PreviewBitmap.BackBuffer, PreviewBitmap.BackBufferStride);
                apply(mat);
                PreviewBitmap.AddDirtyRect(new Int32Rect(0, 0, PreviewBitmap.PixelWidth, PreviewBitmap.PixelHeight));
            }
            finally
            {
                PreviewBitmap.Unlock();
            }
        }

        public void Commit()
        {
            _image.ViewBitmapSource = PreviewBitmap;
            _image.ImageShow.Source = _image.ViewBitmapSource;
            _image.HImageCache = PreviewBitmap.ToHImage();
            _image.FunctionImage = null;
            _isCompleted = true;
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
            PreviewBitmap.Lock();
            try
            {
                Marshal.Copy(_originalPixels, 0, PreviewBitmap.BackBuffer, _originalPixels.Length);
                PreviewBitmap.AddDirtyRect(new Int32Rect(0, 0, PreviewBitmap.PixelWidth, PreviewBitmap.PixelHeight));
            }
            finally
            {
                PreviewBitmap.Unlock();
            }
        }

        private static byte[] CopyPixels(WriteableBitmap bitmap)
        {
            int size = bitmap.BackBufferStride * bitmap.PixelHeight;
            byte[] pixels = new byte[size];

            bitmap.Lock();
            try
            {
                Marshal.Copy(bitmap.BackBuffer, pixels, 0, size);
            }
            finally
            {
                bitmap.Unlock();
            }

            return pixels;
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
