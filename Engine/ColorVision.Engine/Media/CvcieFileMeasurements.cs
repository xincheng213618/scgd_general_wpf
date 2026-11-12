using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Draw;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;

namespace ColorVision.Engine.Media;

internal readonly record struct CvcieProbeResult(int Channels, PoiMeasurementResult Measurement);
internal readonly record struct CvcieFilePoiRequest(PoiMeasurementPoint Point, ClosedPixelRegion? Region, string Name);

/// <summary>On-demand measurements against the same file revision that supplied the displayed image.</summary>
internal sealed class CvcieFileMeasurements(string path, CvcieFileRevision revision, int headerEnd, uint version,
    int width, int height, int bitsPerChannel, int channels)
{
    private const int PackedByteBudget = 8 * 1024 * 1024;
    private const int SparseRowReadBudget = 4096;

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

    public CvcieProbeResult? CalculateProbe(PoiMeasurementPoint point, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var (planeBytes, requiredBytes) = GetLayout();
        if (!IsValidPoint(point))
            throw new ArgumentOutOfRangeException(nameof(point));
        Int32Rect crop = GetPointCrop(point);
        long cropBytes = GetCropByteCount(crop);
        if (cropBytes > PackedByteBudget) return null;

        using FileStream stream = OpenRead(out CVCIEFile header);
        using (header)
        {
            CvcieDiskPixels pixels = CvcieDiskPixels.Locate(stream, header, headerEnd, requiredBytes, requireExactLength: false);
            int length = (int)cropBytes;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                return new CvcieProbeResult(channels, CalculatePacked(pixels, planeBytes, crop, buffer.AsSpan(0, length),
                    point, null, preserveNonPositiveValues: false, token));
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
    }

    /// <summary>Returns null when the plan is better served by the whole-image snapshot.</summary>
    public (int Channels, PoiMeasurementResult[] Results)? TryCalculateSparse(IReadOnlyList<CvcieFilePoiRequest> requests,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var (planeBytes, requiredBytes) = GetLayout();
        if (requiredBytes < PackedByteBudget || requests.Count == 0 || requests.Count > SparseRowReadBudget / channels)
            return null;

        long byteBudget = Math.Min(PackedByteBudget, requiredBytes / 8);
        var plans = new (Int32Rect Crop, int Length)[requests.Count];
        long totalBytes = 0, totalRows = 0;
        int largestCrop = 0;
        for (int index = 0; index < requests.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            CvcieFilePoiRequest request = requests[index];
            // The full path retains its existing diagnostics for inputs without a safe crop.
            if (request.Region == null && !IsValidPoint(request.Point)) return null;
            Int32Rect crop = request.Region is { } region ? GetRegionCrop(region.Bounds) : GetPointCrop(request.Point);
            if (crop.Width == 0 || crop.Height == 0) return null;
            long cropBytes = GetCropByteCount(crop);
            totalRows += (long)crop.Height * channels;
            if (cropBytes > byteBudget - totalBytes || totalRows > SparseRowReadBudget) return null;
            totalBytes += cropBytes;
            plans[index] = (crop, (int)cropBytes);
            largestCrop = Math.Max(largestCrop, (int)cropBytes);
        }

        using FileStream stream = OpenRead(out CVCIEFile header);
        using (header)
        {
            CvcieDiskPixels pixels = CvcieDiskPixels.Locate(stream, header, headerEnd, requiredBytes, requireExactLength: false);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(largestCrop);
            try
            {
                var results = new PoiMeasurementResult[requests.Count];
                // Match the full path's region error order while keeping all results in request order.
                for (int index = 0; index < requests.Count; index++)
                {
                    if (requests[index].Region == null) continue;
                    try { results[index] = Calculate(index); }
                    catch (ArgumentException error) { throw new ArgumentException($"区域 {requests[index].Name}：{error.Message}", error); }
                }
                for (int index = 0; index < requests.Count; index++)
                {
                    if (requests[index].Region == null) results[index] = Calculate(index);
                }
                token.ThrowIfCancellationRequested();
                return (channels, results);

                PoiMeasurementResult Calculate(int index)
                {
                    var (crop, length) = plans[index];
                    CvcieFilePoiRequest request = requests[index];
                    return CalculatePacked(pixels, planeBytes, crop, buffer.AsSpan(0, length),
                        request.Point, request.Region, preserveNonPositiveValues: true, token);
                }
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }
    }

    private (long PlaneBytes, long RequiredBytes) GetLayout()
    {
        long planeBytes = checked((long)width * height * sizeof(float));
        long requiredBytes = checked(planeBytes * channels);
        PoiMeasurementService.ValidateLayout(width, height, bitsPerChannel, channels, requiredBytes);
        return (planeBytes, requiredBytes);
    }

    private bool IsValidPoint(PoiMeasurementPoint point) => (uint)point.X < (uint)width && (uint)point.Y < (uint)height
        && point.Width > 0 && point.Height > 0 && point.Shape is >= PoiMeasurementShape.Point and <= PoiMeasurementShape.Ellipse;

    private Int32Rect GetPointCrop(PoiMeasurementPoint point)
    {
        // Use a conservative box. The original shape and native inclusion rules still choose the samples.
        long radiusX = point.Shape == PoiMeasurementShape.Point ? 0 : (point.Width + 1L) / 2;
        long radiusY = point.Shape is PoiMeasurementShape.Point or PoiMeasurementShape.Circle ? radiusX : (point.Height + 1L) / 2;
        int left = (int)Math.Max(0, point.X - radiusX);
        int top = (int)Math.Max(0, point.Y - radiusY);
        int right = (int)Math.Min(width, point.X + radiusX + 1);
        int bottom = (int)Math.Min(height, point.Y + radiusY + 1);
        return new Int32Rect(left, top, right - left, bottom - top);
    }

    private Int32Rect GetRegionCrop(Rect bounds)
    {
        if (bounds.IsEmpty || !double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Top)
            || !double.IsFinite(bounds.Right) || !double.IsFinite(bounds.Bottom)) return Int32Rect.Empty;

        // Leave a pixel of slack for floating-point boundaries in the original-coordinate runs.
        int left = (int)Math.Clamp(Math.Floor(bounds.Left) - 1, 0, width);
        int top = (int)Math.Clamp(Math.Floor(bounds.Top) - 1, 0, height);
        int right = (int)Math.Clamp(Math.Ceiling(bounds.Right) + 1, 0, width);
        int bottom = (int)Math.Clamp(Math.Ceiling(bounds.Bottom) + 1, 0, height);
        return new Int32Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    private long GetCropByteCount(Int32Rect crop) => checked((long)crop.Width * crop.Height * sizeof(float) * channels);

    private unsafe PoiMeasurementResult CalculatePacked(CvcieDiskPixels pixels, long planeBytes, Int32Rect crop,
        Span<byte> packed, PoiMeasurementPoint point, ClosedPixelRegion? region, bool preserveNonPositiveValues,
        CancellationToken token)
    {
        int rowBytes = crop.Width * sizeof(float);
        for (int channel = 0; channel < channels; channel++)
        {
            for (int row = 0; row < crop.Height; row++)
            {
                long offset = channel * planeBytes + ((long)(crop.Y + row) * width + crop.X) * sizeof(float);
                Span<byte> target = packed.Slice((channel * crop.Height + row) * rowBytes, rowBytes);
                pixels.ReadExactly(target, offset, token);
            }
        }

        token.ThrowIfCancellationRequested();
        fixed (byte* data = packed)
        {
            if (region != null)
                return PoiMeasurementService.CalculateCroppedRegion((IntPtr)data, channels, crop, region.GetRuns(width, height), preserveNonPositiveValues);
            if (point.Shape == PoiMeasurementShape.Ellipse)
                return PoiMeasurementService.CalculateCroppedRegion((IntPtr)data, channels, crop,
                    PoiMeasurementService.GetEllipseRuns(point, width, height), preserveNonPositiveValues);
            return PoiMeasurementService.CalculateCore((IntPtr)data, packed.Length, crop.Width, crop.Height, bitsPerChannel, channels,
                [point with { X = point.X - crop.X, Y = point.Y - crop.Y }], preserveNonPositiveValues)[0];
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
