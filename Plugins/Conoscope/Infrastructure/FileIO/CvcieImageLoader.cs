using ColorVision.FileIO;
using Conoscope.Domain.Models;
using OpenCvSharp;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Conoscope.Infrastructure.FileIO
{
    internal static class CvcieImageLoader
    {
        public static ConoscopeImageData Load(string filename)
        {
            if (!CVFileUtil.IsCVCIEFile(filename))
            {
                throw new NotSupportedException(Properties.Resources.OnlyCVCIESupported);
            }

            if (!CVFileUtil.Read(filename, out CVCIEFile fileInfo))
            {
                throw new InvalidDataException(Properties.Resources.ReadCVCIEFailed);
            }

            using (fileInfo)
            {
                if (fileInfo.Channels < 3)
                {
                    throw new NotSupportedException(string.Format(Properties.Resources.CVCIEChannelInsufficientLoader, fileInfo.Channels));
                }

                int bytesPerPixel = fileInfo.Bpp / 8;
                int channelSize = fileInfo.Cols * fileInfo.Rows * bytesPerPixel;
                if (fileInfo.Data == null || fileInfo.Data.Length < channelSize * 3)
                {
                    throw new InvalidDataException(Properties.Resources.CVCIEDataLengthInsufficientLoader);
                }

                MatType singleChannelType = GetSingleChannelMatType(fileInfo.Bpp);
                Mat x = CreateFloatChannelMat(fileInfo.Data, 0, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
                Mat y = CreateFloatChannelMat(fileInfo.Data, channelSize, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
                Mat z = CreateFloatChannelMat(fileInfo.Data, channelSize * 2, channelSize, fileInfo.Rows, fileInfo.Cols, singleChannelType);
                return new ConoscopeImageData(x, y, z, fileInfo.Bpp, BuildExposureSummary(fileInfo.Exp));
            }
        }

        private static string? BuildExposureSummary(float[]? exposureTimes)
        {
            if (exposureTimes == null || exposureTimes.Length == 0)
            {
                return null;
            }

            double[] normalizedExposureTimes = exposureTimes
                .Select(value => double.IsFinite(value) ? (double)value : 0d)
                .ToArray();
            if (!normalizedExposureTimes.Any(value => value > 0))
            {
                return null;
            }

            if (normalizedExposureTimes.Length <= 1)
            {
                return $"{normalizedExposureTimes[0].ToString("0.###", CultureInfo.InvariantCulture)} ms";
            }

            string[] channelNames = new[] { "R", "G", "B" };
            string channelSummary = string.Join(" / ", normalizedExposureTimes.Select((value, index) =>
            {
                string channelName = index < channelNames.Length ? channelNames[index] : $"Ch{index + 1}";
                return $"{channelName}:{value.ToString("0.###", CultureInfo.InvariantCulture)}";
            }));
            return $"{channelSummary} ms";
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