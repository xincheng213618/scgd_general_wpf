using ColorVision.Core;
using OpenCvSharp;
using System;
using System.Runtime.InteropServices;

namespace ColorVision.Engine.Media
{
    public static class HImageOpenCvExtensions
    {
        // Mat -> HImage（深拷贝）
        public static HImage ToHImage(this Mat mat)
        {
            if (mat == null || mat.Empty()) throw new ArgumentException("Mat is null or empty", nameof(mat));

            mat = mat.Clone(); // 避免后续被释放或共享引用修改

            int channels = mat.Channels();
            int depthBits = mat.ElemSize() * 8 / channels; // e.g. 8, 16
            int stride = (int)mat.Step();

            HImage h = new()
            {
                rows = mat.Rows,
                cols = mat.Cols,
                channels = channels,
                depth = depthBits,
                stride = stride,
                pData = Marshal.AllocHGlobal(mat.Rows * stride)
            };

            unsafe
            {
                byte* src = (byte*)mat.DataPointer;
                byte* dst = (byte*)h.pData;
                for (int y = 0; y < mat.Rows; y++)
                {
                    Buffer.MemoryCopy(src, dst, stride, stride);
                    src += mat.Step();
                    dst += stride;
                }
            }
            return h;
        }

        // HImage -> Mat（零拷贝，注意生命周期；需要深拷贝可 Clone()）
        public static Mat ToMat(this HImage hImage, bool clone = true)
        {
            if (hImage.pData == IntPtr.Zero) throw new ArgumentException("HImage pData is null", nameof(hImage));

            MatType type = GetMatType(hImage.depth, hImage.channels);
            int expectedStride = hImage.cols * hImage.channels * (hImage.depth / 8);

            // 若 stride 连续，可省略 stride 参数
            Mat mat = (hImage.stride == expectedStride)
                ? Mat.FromPixelData(hImage.rows, hImage.cols, type, hImage.pData)
                : Mat.FromPixelData(hImage.rows, hImage.cols, type, hImage.pData, hImage.stride);

            return clone ? mat.Clone() : mat;
        }

        private static MatType GetMatType(int depthBits, int channels) => (depthBits, channels) switch
        {
            (8, 1) => MatType.CV_8UC1,
            (8, 3) => MatType.CV_8UC3,
            (8, 4) => MatType.CV_8UC4,
            (16, 1) => MatType.CV_16UC1,
            (16, 3) => MatType.CV_16UC3,
            (16, 4) => MatType.CV_16UC4,
            (32, 1) => MatType.CV_32FC1, // 如需要
            _ => throw new NotSupportedException($"Unsupported depth {depthBits} channels {channels}")
        };
    }
}
