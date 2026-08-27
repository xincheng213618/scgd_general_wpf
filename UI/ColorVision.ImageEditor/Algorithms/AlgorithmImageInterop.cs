using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Runtime.InteropServices;

namespace ColorVision.ImageEditor.Algorithms
{
    internal static class AlgorithmImageInterop
    {
        public static Mat ToMat(AlgorithmImageBuffer image)
        {
            Mat result = new(image.Height, image.Width, ToMatType(image.Format));
            ReadOnlySpan<byte> source = image.Data.Span;
            int rowBytes = checked(image.Width * image.Format.BytesPerPixel());
            for (int row = 0; row < image.Height; row++)
            {
                byte[] rowData = source.Slice(row * image.Stride, rowBytes).ToArray();
                Marshal.Copy(rowData, 0, result.Ptr(row), rowBytes);
            }
            return result;
        }

        public static AlgorithmImageBuffer FromMat(Mat mat, double dpiX = 96, double dpiY = 96)
        {
            if (mat.Empty()) throw new ArgumentException("The provider returned an empty image.", nameof(mat));
            AlgorithmImageFormat format = ToImageFormat(mat.Type());
            int width = mat.Cols;
            int height = mat.Rows;
            int stride = checked(width * format.BytesPerPixel());
            byte[] data = new byte[checked(stride * height)];
            byte[] rowData = new byte[stride];
            for (int row = 0; row < height; row++)
            {
                Marshal.Copy(mat.Ptr(row), rowData, 0, stride);
                Buffer.BlockCopy(rowData, 0, data, row * stride, stride);
            }
            return new AlgorithmImageBuffer(width, height, stride, format, data, dpiX, dpiY);
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
}
