using ColorVision.FileIO;
using OpenCvSharp;
using System;
using System.IO;

namespace Conoscope.Core
{
    public sealed class ConoscopeXyzData : IDisposable
    {
        private Mat? x;
        private Mat? y;
        private Mat? z;

        public ConoscopeXyzData(Mat x, Mat y, Mat z, int bitsPerPixel)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            Width = x.Width;
            Height = x.Height;
            BitsPerPixel = bitsPerPixel;
        }

        public int BitsPerPixel { get; }

        public int Width { get; }

        public int Height { get; }

        public Mat X => x ?? throw new ObjectDisposedException(nameof(ConoscopeXyzData));

        public Mat Y => y ?? throw new ObjectDisposedException(nameof(ConoscopeXyzData));

        public Mat Z => z ?? throw new ObjectDisposedException(nameof(ConoscopeXyzData));

        public (Mat X, Mat Y, Mat Z) Detach()
        {
            Mat detachedX = X;
            Mat detachedY = Y;
            Mat detachedZ = Z;
            x = null;
            y = null;
            z = null;
            return (detachedX, detachedY, detachedZ);
        }

        public void Dispose()
        {
            x?.Dispose();
            x = null;
            y?.Dispose();
            y = null;
            z?.Dispose();
            z = null;
            GC.SuppressFinalize(this);
        }
    }

    internal static class ConoscopeXyzDataLoader
    {
        public static ConoscopeXyzData Load(string filename)
        {
            if (!CVFileUtil.IsCVCIEFile(filename))
            {
                throw new NotSupportedException("当前视图仅支持 CVCIE XYZ 图像文件");
            }

            CVFileUtil.Read(filename, out CVCIEFile fileInfo);
            if (fileInfo.Channels < 3)
            {
                throw new NotSupportedException($"CVCIE 文件通道数不足: {fileInfo.Channels}");
            }

            int bytesPerPixel = fileInfo.Bpp / 8;
            int channelSize = fileInfo.Cols * fileInfo.Rows * bytesPerPixel;
            if (fileInfo.Data == null || fileInfo.Data.Length < channelSize * 3)
            {
                throw new InvalidDataException("CVCIE 文件数据长度不足，无法拆分 XYZ 通道");
            }

            MatType singleChannelType = GetSingleChannelMatType(fileInfo.Bpp);
            Mat x = CreateFloatChannelMat(fileInfo.Data, 0, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
            Mat y = CreateFloatChannelMat(fileInfo.Data, channelSize, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
            Mat z = CreateFloatChannelMat(fileInfo.Data, channelSize * 2, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
            return new ConoscopeXyzData(x, y, z, fileInfo.Bpp);
        }

        private static MatType GetSingleChannelMatType(int bpp)
        {
            return bpp switch
            {
                8 => MatType.CV_8UC1,
                16 => MatType.CV_16UC1,
                32 => MatType.CV_32FC1,
                64 => MatType.CV_64FC1,
                _ => throw new NotSupportedException($"Bpp {bpp} not supported")
            };
        }

        private static Mat CreateFloatChannelMat(byte[] source, int offset, int channelSize, int rows, int cols, MatType sourceType)
        {
            byte[] channelData = new byte[channelSize];
            Buffer.BlockCopy(source, offset, channelData, 0, channelSize);

            using Mat raw = Mat.FromPixelData(rows, cols, sourceType, channelData);
            Mat copied = raw.Clone();
            if (copied.Type() == MatType.CV_32FC1)
            {
                return copied;
            }

            Mat floatMat = new Mat();
            copied.ConvertTo(floatMat, MatType.CV_32FC1);
            copied.Dispose();
            return floatMat;
        }
    }
}