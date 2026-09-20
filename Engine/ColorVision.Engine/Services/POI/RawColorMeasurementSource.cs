using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.POI
{
    /// <summary>Owns one RAW snapshot. Small measurements never allocate full XYZ planes.</summary>
    internal sealed class RawColorMeasurementSource : IDisposable
    {
        private readonly object sync = new();
        private readonly RawColorTransformV1 transform;
        private byte[]? raw;
        private byte[]? xyz;
        private PoiMeasurementBuffer? xyzBuffer;
        private long visitedPixels;
        internal int Width { get; }
        internal int Height { get; }
        internal int Bpp { get; }
        internal int Channels => transform.Channels;
        internal long CachedXyzBytes { get { lock (sync) return xyz?.LongLength ?? 0; } }

        internal CVCIEFile GetRawFile()
        {
            lock (sync)
            {
                ThrowIfDisposed();
                return new CVCIEFile { Cols = Width, Rows = Height, Bpp = Bpp, Channels = Channels, FileExtType = CVType.Raw, Data = raw! };
            }
        }

        internal void UseCalculatedXyz(byte[] data)
        {
            lock (sync)
            {
                ThrowIfDisposed();
                if (data.LongLength != checked((long)Width * Height * Channels * sizeof(float))) throw new ArgumentException("XYZ 布局不匹配。", nameof(data));
                xyzBuffer?.Dispose();
                xyz = data;
                xyzBuffer = new PoiMeasurementBuffer(data, Width, Height, 32, Channels);
            }
        }

        internal RawColorMeasurementSource(CVCIEFile file, ColorCalibrationSnapshot snapshot)
        {
            snapshot.Validate();
            if (!snapshot.CanReplay || file.Cols != snapshot.Width || file.Rows != snapshot.Height
                || file.Bpp != snapshot.RawBpp || file.Channels != snapshot.Channels
                || file.Data == null || file.Data.LongLength != checked((long)file.Cols * file.Rows * file.Channels * (file.Bpp / 8)))
                throw new InvalidDataException("当前 RAW 像素不能直接重放此色度校正。");
            Width = file.Cols; Height = file.Rows; Bpp = file.Bpp;
            raw = file.Data;
            transform = snapshot.ToNative();
        }

        internal unsafe byte[] CreateChannel(int channel, CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();
                if (channel < 0 || channel >= Channels) throw new ArgumentOutOfRangeException(nameof(channel));
                byte[] output = GC.AllocateUninitializedArray<byte>(checked(Width * Height * sizeof(float)));
                if (xyz != null) Buffer.BlockCopy(xyz, checked(channel * output.Length), output, 0, output.Length);
                else TransformInto(output, channel);
                cancellationToken.ThrowIfCancellationRequested();
                return output;
            }
        }

        internal PoiMeasurementResult[] Calculate(IReadOnlyList<PoiMeasurementPoint> points, bool preserveNonPositiveValues)
        {
            ArgumentNullException.ThrowIfNull(points);
            lock (sync)
            {
                ThrowIfDisposed();
                if (points.Count == 0) return Array.Empty<PoiMeasurementResult>();
                long visits = 0;
                foreach (PoiMeasurementPoint point in points)
                {
                    if (point.X < 0 || point.X >= Width || point.Y < 0 || point.Y >= Height || point.Width <= 0 || point.Height <= 0
                        || point.Shape is < PoiMeasurementShape.Point or > PoiMeasurementShape.Ellipse)
                        throw new ArgumentOutOfRangeException(nameof(points), "POI 坐标或尺寸无效。");
                    visits = Math.Min((long)Width * Height, visits + (point.Shape == PoiMeasurementShape.Point ? 1
                        : (long)Math.Min(Width, point.Width) * Math.Min(Height, point.Height)));
                }
                ConsiderFullBuffer(visits);
                if (xyzBuffer != null) return preserveNonPositiveValues
                    ? PoiMeasurementService.CalculateRaw(xyzBuffer, points) : PoiMeasurementService.Calculate(xyzBuffer, points);
                if (points.Any(point => point.Shape == PoiMeasurementShape.Ellipse))
                {
                    var mixed = new PoiMeasurementResult[points.Count];
                    for (int i = 0; i < points.Count; i++)
                    {
                        PoiMeasurementPoint point = points[i];
                        mixed[i] = point.Shape == PoiMeasurementShape.Ellipse
                            ? CalculateRegion(ClosedPixelRegion.Ellipse(new System.Windows.Point(point.X, point.Y), point.Width / 2.0, point.Height / 2.0), preserveNonPositiveValues)
                            : Calculate(new[] { point }, preserveNonPositiveValues)[0];
                    }
                    return mixed;
                }
                PoiRequestV1[] requests = points.Select(point => new PoiRequestV1
                {
                    Type = (int)point.Shape, X = point.X, Y = point.Y,
                    Width = point.Shape == PoiMeasurementShape.Point ? 1 : point.Width,
                    Height = point.Shape == PoiMeasurementShape.Point ? 1 : point.Height,
                }).ToArray();
                PoiResultV1[] results = new PoiResultV1[requests.Length];
                PoiOptionsV2 options = Options(preserveNonPositiveValues);
                unsafe
                {
                    fixed (byte* pointer = raw)
                        Check(OpenCVMediaHelper.M_CalculateRawPoiBatchV1(Width, Height, Bpp, (IntPtr)pointer, (ulong)raw!.LongLength,
                            in transform, requests, (uint)requests.Length, in options, results));
                }
                return results.Select(Convert).ToArray();
            }
        }

        internal PoiMeasurementResult CalculateRegion(ClosedPixelRegion region, bool preserveNonPositiveValues)
        {
            ArgumentNullException.ThrowIfNull(region);
            lock (sync)
            {
                ThrowIfDisposed();
                RawPixelRunV1[] runs = region.GetRuns(Width, Height).Select(run => new RawPixelRunV1 { Y = run.Y, StartX = run.StartX, EndX = run.EndX }).ToArray();
                if (runs.Length == 0) throw new ArgumentException("该封闭区域内没有可测量的图像像素。");
                ConsiderFullBuffer(runs.Sum(run => (long)run.EndX - run.StartX));
                if (xyzBuffer != null) return PoiMeasurementService.CalculateRegion(xyzBuffer, region, preserveNonPositiveValues);
                PoiOptionsV2 options = Options(preserveNonPositiveValues);
                PoiResultV1 result;
                unsafe
                {
                    fixed (byte* pointer = raw)
                        Check(OpenCVMediaHelper.M_CalculateRawRegionV1(Width, Height, Bpp, (IntPtr)pointer, (ulong)raw!.LongLength,
                            in transform, runs, (uint)runs.Length, in options, out result));
                }
                return Convert(result);
            }
        }

        internal T BorrowXyz<T>(Func<IntPtr, long, T> action)
        {
            lock (sync)
            {
                ThrowIfDisposed();
                EnsureFullBuffer();
                return xyzBuffer!.Borrow(action);
            }
        }

        private void ConsiderFullBuffer(long visits)
        {
            long pixels = (long)Width * Height;
            visitedPixels = Math.Min(pixels, visitedPixels + visits);
            long bytes = pixels * Channels * sizeof(float);
            // A bounded optional cache: large/repeated exact scans can amortize full conversion.
            // Low-memory machines keep the slower on-demand path instead of forcing a huge allocation.
            long budget = Math.Min(1024L * 1024 * 1024, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 8);
            if (xyz == null && visitedPixels >= (pixels + 1) / 2 && bytes <= budget && bytes <= Array.MaxLength)
                EnsureFullBuffer();
        }

        private void EnsureFullBuffer()
        {
            if (xyz != null) return;
            byte[] output = GC.AllocateUninitializedArray<byte>(checked(Width * Height * Channels * sizeof(float)));
            TransformInto(output, -1);
            xyzBuffer = new PoiMeasurementBuffer(output, Width, Height, 32, Channels);
            xyz = output;
        }

        private unsafe void TransformInto(byte[] output, int channel)
        {
            fixed (byte* input = raw)
            fixed (byte* destination = output)
                Check(OpenCVMediaHelper.M_TransformRawColorV1(Width, Height, Bpp, (IntPtr)input, (ulong)raw!.LongLength,
                    in transform, channel, (IntPtr)destination, (ulong)output.LongLength / sizeof(float)));
        }

        private static PoiOptionsV2 Options(bool preserve)
        {
            PoiOptionsV2 options = PoiOptionsV2.Create();
            if (preserve) options.Flags = PoiOptionsFlagsV2.PreserveNonPositiveValues;
            return options;
        }
        private static PoiMeasurementResult Convert(PoiResultV1 result)
            => new(result.X, result.Y, result.Z, result.ChromaX, result.ChromaY, result.u, result.v, result.Cct, result.Wave);
        private static void Check(int result)
        {
            if (result != OpenCVCalibration.PoiOk) throw new InvalidOperationException($"RAW 色度计算失败：{result}。");
        }
        private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(raw == null, this);
        public void Dispose()
        {
            lock (sync) { raw = null; xyz = null; xyzBuffer?.Dispose(); xyzBuffer = null; }
        }
    }
}
