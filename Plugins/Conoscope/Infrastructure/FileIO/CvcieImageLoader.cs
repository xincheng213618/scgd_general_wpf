using ColorVision.FileIO;
using ColorVision.UI.Extension;
using OpenCvSharp;
using System;

namespace Conoscope.Infrastructure.FileIO
{
    internal enum CvcieChannel
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    internal sealed record CvcieImagePayload(byte[] Data, int Rows, int Cols, int BitsPerPixel, string? ExposureSummary)
    {
        public int Width => Cols;

        public int Height => Rows;

        public int ChannelSize => Cols * Rows * (BitsPerPixel / 8);

        public MatType ChannelMatType => BitsPerPixel switch
        {
            8 => MatType.CV_8UC1,
            16 => MatType.CV_16UC1,
            64 => MatType.CV_64FC1,
            _ => MatType.CV_32FC1
        };
    }

    internal static class CvcieImageLoader
    {

        internal static CvcieImagePayload LoadPayload(string filename)
        {
            CVFileUtil.Read(filename, out CVCIEFile fileInfo);
            using (fileInfo)
            {
                byte[] data = fileInfo.Data;
                fileInfo.Data = Array.Empty<byte>();

                return new CvcieImagePayload(data, fileInfo.Rows, fileInfo.Cols, fileInfo.Bpp, fileInfo.Exp.ToJsonN());
            }
        }
            

        internal static unsafe Mat CreateChannelMat(CvcieImagePayload payload, CvcieChannel channel)
        {
            fixed (byte* data = payload.Data)
            {
                using Mat raw = Mat.FromPixelData(payload.Rows, payload.Cols, payload.ChannelMatType, (nint)(data + payload.ChannelSize * (int)channel));
                if (payload.ChannelMatType == MatType.CV_32FC1)
                {
                    return raw.Clone();
                }
                Mat floatMat = new Mat();
                raw.ConvertTo(floatMat, MatType.CV_32FC1);
                return floatMat;
            }
        }
    }
}
