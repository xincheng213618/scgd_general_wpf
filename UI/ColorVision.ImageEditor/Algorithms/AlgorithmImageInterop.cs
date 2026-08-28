using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Buffers;

namespace ColorVision.ImageEditor.Algorithms
{
    internal static class AlgorithmImageInterop
    {
        public static Mat ToMat(AlgorithmImageBuffer image)
        {
            using AlgorithmImageMatLease source = BorrowReadOnly(image);
            return source.Mat.Clone();
        }

        /// <summary>
        /// Pins the immutable algorithm input and exposes it as an OpenCV header without copying pixels.
        /// Providers must not mutate this Mat and must dispose the lease before the image buffer.
        /// </summary>
        public static AlgorithmImageMatLease BorrowReadOnly(AlgorithmImageBuffer image) => new(image);

        public static AlgorithmImageBuffer FromMat(Mat mat, double dpiX = 96, double dpiY = 96)
        {
            if (mat.Empty()) throw new ArgumentException("The provider returned an empty image.", nameof(mat));
            AlgorithmImageFormat format = ToImageFormat(mat.Type());
            int width = mat.Cols;
            int height = mat.Rows;
            int stride = checked(width * format.BytesPerPixel());
            byte[] data = new byte[checked(stride * height)];
            CopyRows(mat, data, stride, height);
            return new AlgorithmImageBuffer(width, height, stride, format, data, dpiX, dpiY);
        }

        private static unsafe void CopyRows(Mat source, byte[] destination, int rowBytes, int height)
        {
            for (int row = 0; row < height; row++)
            {
                ReadOnlySpan<byte> sourceRow = new((void*)source.Ptr(row), rowBytes);
                sourceRow.CopyTo(destination.AsSpan(row * rowBytes, rowBytes));
            }
        }

        public static MatType ToMatType(AlgorithmImageFormat format) => format switch
        {
            AlgorithmImageFormat.Gray8 => MatType.CV_8UC1,
            AlgorithmImageFormat.Gray16 => MatType.CV_16UC1,
            AlgorithmImageFormat.Gray32Float => MatType.CV_32FC1,
            AlgorithmImageFormat.Bgr24 => MatType.CV_8UC3,
            AlgorithmImageFormat.Bgr48 => MatType.CV_16UC3,
            AlgorithmImageFormat.Bgr96Float => MatType.CV_32FC3,
            AlgorithmImageFormat.Bgra32 => MatType.CV_8UC4,
            AlgorithmImageFormat.Bgra64 => MatType.CV_16UC4,
            AlgorithmImageFormat.Bgra128Float => MatType.CV_32FC4,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        public static AlgorithmImageFormat ToImageFormat(MatType type)
        {
            if (type == MatType.CV_8UC1) return AlgorithmImageFormat.Gray8;
            if (type == MatType.CV_16UC1) return AlgorithmImageFormat.Gray16;
            if (type == MatType.CV_32FC1) return AlgorithmImageFormat.Gray32Float;
            if (type == MatType.CV_8UC3) return AlgorithmImageFormat.Bgr24;
            if (type == MatType.CV_16UC3) return AlgorithmImageFormat.Bgr48;
            if (type == MatType.CV_32FC3) return AlgorithmImageFormat.Bgr96Float;
            if (type == MatType.CV_8UC4) return AlgorithmImageFormat.Bgra32;
            if (type == MatType.CV_16UC4) return AlgorithmImageFormat.Bgra64;
            if (type == MatType.CV_32FC4) return AlgorithmImageFormat.Bgra128Float;
            throw new NotSupportedException($"Unsupported OpenCV image type: {type}.");
        }
    }

    internal sealed class AlgorithmImageMatLease : IDisposable
    {
        private MemoryHandle _pin;
        private bool _disposed;

        public unsafe AlgorithmImageMatLease(AlgorithmImageBuffer image)
        {
            ArgumentNullException.ThrowIfNull(image);
            _pin = image.Data.Pin();
            try
            {
                Mat = Mat.FromPixelData(
                    image.Height,
                    image.Width,
                    AlgorithmImageInterop.ToMatType(image.Format),
                    (IntPtr)_pin.Pointer,
                    image.Stride);
            }
            catch
            {
                _pin.Dispose();
                throw;
            }
        }

        public Mat Mat { get; }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Mat.Dispose();
            _pin.Dispose();
        }
    }
}
