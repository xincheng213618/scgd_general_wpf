using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Windows;

namespace ColorVision.Engine.Media;

internal readonly record struct CvcieProbeResult(int Channels, PoiMeasurementResult Measurement);

/// <summary>On-demand measurements against the same file revision that supplied the displayed image.</summary>
internal sealed class CvcieFileMeasurements(string path, CvcieFileRevision revision, int headerEnd, uint version,
    int width, int height, int bitsPerChannel, int channels)
{
    private const int MaxProbeBytes = 8 * 1024 * 1024;

    public PoiMeasurementBuffer Load(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using FileStream stream = OpenRead(out CVCIEFile header);
        using (header)
        {
            if (!CVFileUtil.ReadCIEFilePixels(stream, header, headerEnd, requireExactPayload: false, token))
                throw new InvalidDataException("读取 CVCIE 测量数据失败。");
            return new PoiMeasurementBuffer(header.Data!, width, height, bitsPerChannel, channels);
        }
    }

    public unsafe CvcieProbeResult? CalculateProbe(PoiMeasurementPoint point, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        long planeBytes = checked((long)width * height * sizeof(float));
        long requiredBytes = checked(planeBytes * channels);
        PoiMeasurementService.ValidateLayout(width, height, bitsPerChannel, channels, requiredBytes);
        if ((uint)point.X >= (uint)width || (uint)point.Y >= (uint)height || point.Width <= 0 || point.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(point));

        // Use a conservative box. The original shape and native inclusion rules still choose the samples.
        long radiusX = point.Shape == PoiMeasurementShape.Point ? 0 : (point.Width + 1L) / 2;
        long radiusY = point.Shape is PoiMeasurementShape.Point or PoiMeasurementShape.Circle ? radiusX : (point.Height + 1L) / 2;
        int left = (int)Math.Max(0, point.X - radiusX);
        int top = (int)Math.Max(0, point.Y - radiusY);
        int right = (int)Math.Min(width, point.X + radiusX + 1);
        int bottom = (int)Math.Min(height, point.Y + radiusY + 1);
        var crop = new Int32Rect(left, top, right - left, bottom - top);
        long cropBytes = checked((long)crop.Width * crop.Height * sizeof(float) * channels);
        if (cropBytes > MaxProbeBytes) return null;

        using FileStream stream = OpenRead(out CVCIEFile header);
        using (header)
        {
            CvcieDiskPixels pixels = CvcieDiskPixels.Locate(stream, header, headerEnd, requiredBytes, requireExactLength: false);
            int length = (int)cropBytes;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                Span<byte> packed = buffer.AsSpan(0, length);
                int rowBytes = crop.Width * sizeof(float);
                for (int channel = 0; channel < channels; channel++)
                {
                    for (int row = 0; row < crop.Height; row++)
                    {
                        long offset = channel * planeBytes + ((long)(top + row) * width + left) * sizeof(float);
                        Span<byte> target = packed.Slice((channel * crop.Height + row) * rowBytes, rowBytes);
                        pixels.ReadExactly(target, offset, token);
                    }
                }

                token.ThrowIfCancellationRequested();
                fixed (byte* data = packed)
                {
                    PoiMeasurementResult result = point.Shape == PoiMeasurementShape.Ellipse
                        ? PoiMeasurementService.CalculateCroppedEllipse((IntPtr)data, width, height, channels, point, crop)
                        : PoiMeasurementService.Calculate((IntPtr)data, length, crop.Width, crop.Height, bitsPerChannel, channels,
                            [point with { X = point.X - left, Y = point.Y - top }])[0];
                    return new CvcieProbeResult(channels, result);
                }
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
    }

    private FileStream OpenRead(out CVCIEFile header)
    {
        FileStream stream = revision.OpenRead(path);
        try
        {
            int currentEnd = CVFileUtil.ReadCIEFileHeader(stream, out header, maximumChannels: 3);
            if (currentEnd == headerEnd && header.Version == version && header.Cols == width
                && header.Rows == height && header.Bpp == bitsPerChannel && header.Channels == channels)
                return stream;
            header.Dispose();
            throw new CvcieFileChangedException(path);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}
