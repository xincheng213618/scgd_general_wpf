using ColorVision.FileIO;
using Conoscope.Domain.Models;
using OpenCvSharp;
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Conoscope.Infrastructure.FileIO
{
    internal enum CvcieChannel
    {
        X = 0,
        Y = 1,
        Z = 2
    }

    internal sealed class CvcieImagePayload
    {
        public CvcieImagePayload(byte[] data, int rows, int cols, int bitsPerPixel, string? exposureSummary)
        {
            Data = data;
            Rows = rows;
            Cols = cols;
            BitsPerPixel = bitsPerPixel;
            ExposureSummary = exposureSummary;
        }

        public byte[] Data { get; }

        public int Rows { get; }

        public int Cols { get; }

        public int BitsPerPixel { get; }

        public string? ExposureSummary { get; }

        public int Width => Cols;

        public int Height => Rows;

        public int ChannelSize => Cols * Rows * (BitsPerPixel / 8);
    }

    internal static class CvcieImageLoader
    {
        public static ConoscopeImageData Load(string filename)
        {
            return Load(LoadPayload(filename));
        }

        internal static CvcieImagePayload LoadPayload(string filename)
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
                ValidatePayload(fileInfo);

                byte[] data = fileInfo.Data;
                fileInfo.Data = Array.Empty<byte>();
                return new CvcieImagePayload(
                    data,
                    fileInfo.Rows,
                    fileInfo.Cols,
                    fileInfo.Bpp,
                    BuildExposureSummary(fileInfo.Exp));
            }
        }

        internal static ConoscopeImageData Load(CvcieImagePayload payload)
        {
            Mat x = CreateChannelMat(payload, CvcieChannel.X);
            Mat y = CreateChannelMat(payload, CvcieChannel.Y);
            Mat z = CreateChannelMat(payload, CvcieChannel.Z);
            return new ConoscopeImageData(x, y, z, payload.BitsPerPixel, payload.ExposureSummary);
        }

        internal static Mat CreateChannelMat(CvcieImagePayload payload, CvcieChannel channel)
        {
            ArgumentNullException.ThrowIfNull(payload);

            MatType singleChannelType = GetSingleChannelMatType(payload.BitsPerPixel);
            int offset = payload.ChannelSize * (int)channel;
            return CreateFloatChannelMat(payload.Data, offset, payload.ChannelSize, payload.Rows, payload.Cols, singleChannelType);
        }

        private static void ValidatePayload(CVCIEFile fileInfo)
        {
            if (fileInfo.Channels < 3)
            {
                throw new NotSupportedException(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.CVCIEChannelInsufficientLoader, fileInfo.Channels));
            }

            int bytesPerPixel = fileInfo.Bpp / 8;
            int channelSize = fileInfo.Cols * fileInfo.Rows * bytesPerPixel;
            if (fileInfo.Data == null || fileInfo.Data.Length < channelSize * 3)
            {
                throw new InvalidDataException(Properties.Resources.CVCIEDataLengthInsufficientLoader);
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

        private static unsafe Mat CreateFloatChannelMat(byte[] source, int offset, int channelSize, int rows, int cols, MatType sourceType)
        {
            ArgumentNullException.ThrowIfNull(source);
            if (offset < 0 || channelSize <= 0 || offset + channelSize > source.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            fixed (byte* sourcePtr = source)
            {
                using Mat raw = Mat.FromPixelData(rows, cols, sourceType, (nint)(sourcePtr + offset));
                if (sourceType == MatType.CV_32FC1)
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