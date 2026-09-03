using ColorVision.Core;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.POI
{
    public enum PoiMeasurementShape
    {
        Point,
        Circle,
        Rect
    }

    public readonly record struct PoiMeasurementPoint(
        int X,
        int Y,
        int Width,
        int Height,
        PoiMeasurementShape Shape);

    public readonly record struct PoiMeasurementResult(
        float X,
        float Y,
        float Z,
        float ChromaX,
        float ChromaY,
        float U,
        float V,
        float Cct,
        float Wave);

    /// <summary>
    /// Owns one managed planar CIE image. The array is pinned only while a native POI call is running.
    /// </summary>
    public sealed class PoiMeasurementBuffer : IDisposable
    {
        private readonly object sync = new();
        private byte[]? data;

        public PoiMeasurementBuffer(byte[] data, int width, int height, int bitsPerChannel, int channels)
        {
            ArgumentNullException.ThrowIfNull(data);
            PoiMeasurementService.ValidateLayout(width, height, bitsPerChannel, channels, data.LongLength);
            this.data = data;
            Width = width;
            Height = height;
            BitsPerChannel = bitsPerChannel;
            Channels = channels;
        }

        public int Width { get; }
        public int Height { get; }
        public int BitsPerChannel { get; }
        public int Channels { get; }

        internal unsafe T Borrow<T>(Func<IntPtr, long, T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            lock (sync)
            {
                byte[] current = data ?? throw new ObjectDisposedException(nameof(PoiMeasurementBuffer));
                fixed (byte* pointer = current)
                {
                    return action((IntPtr)pointer, current.LongLength);
                }
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                data = null;
            }
        }
    }

    /// <summary>
    /// The single Engine boundary for standard, unfiltered POI measurement.
    /// </summary>
    public static class PoiMeasurementService
    {
        internal static PoiMeasurementResult[] CalculateRaw(PoiMeasurementBuffer buffer, IReadOnlyList<PoiMeasurementPoint> points)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            return buffer.Borrow((pointer, length) => CalculateCore(pointer, length, buffer.Width, buffer.Height,
                buffer.BitsPerChannel, buffer.Channels, points, true));
        }

        internal static unsafe PoiMeasurementResult CalculateColorMetrics(double x, double y, double z)
        {
            double scale = Math.Max(x, Math.Max(y, z));
            if (!double.IsFinite(scale) || scale <= 0)
                return new(0, 0, 0, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);

            float* xyz = stackalloc float[] { (float)(x / scale), (float)(y / scale), (float)(z / scale) };
            return CalculateCore((IntPtr)xyz, 3 * sizeof(float), 1, 1, 32, 3,
                new[] { new PoiMeasurementPoint(0, 0, 1, 1, PoiMeasurementShape.Point) }, true)[0];
        }

        public static PoiMeasurementResult[] Calculate(
            PoiMeasurementBuffer buffer,
            IReadOnlyList<PoiMeasurementPoint> points)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            return buffer.Borrow((pointer, length) => Calculate(
                pointer,
                length,
                buffer.Width,
                buffer.Height,
                buffer.BitsPerChannel,
                buffer.Channels,
                points));
        }

        public static PoiMeasurementResult Calculate(
            PoiMeasurementBuffer buffer,
            PoiMeasurementPoint point)
        {
            PoiMeasurementResult[] results = Calculate(buffer, new[] { point });
            return results[0];
        }

        public static PoiMeasurementResult[] Calculate(
            IntPtr cieData,
            long cieByteLength,
            int width,
            int height,
            int bitsPerChannel,
            int channels,
            IReadOnlyList<PoiMeasurementPoint> points)
            => CalculateCore(cieData, cieByteLength, width, height, bitsPerChannel, channels, points, false);

        private static PoiMeasurementResult[] CalculateCore(
            IntPtr cieData,
            long cieByteLength,
            int width,
            int height,
            int bitsPerChannel,
            int channels,
            IReadOnlyList<PoiMeasurementPoint> points,
            bool preserveNonPositiveValues)
        {
            ArgumentNullException.ThrowIfNull(points);
            ValidateLayout(width, height, bitsPerChannel, channels, cieByteLength);
            if (cieData == IntPtr.Zero) throw new ArgumentException("CIE data pointer cannot be null.", nameof(cieData));
            if (points.Count == 0) return Array.Empty<PoiMeasurementResult>();

            PoiRequestV1[] requests = new PoiRequestV1[points.Count];
            for (int index = 0; index < points.Count; index++)
            {
                PoiMeasurementPoint point = points[index];
                ValidatePoint(point, width, height, index);
                requests[index] = new PoiRequestV1
                {
                    Type = ToNativeType(point.Shape),
                    X = point.X,
                    Y = point.Y,
                    Width = point.Shape == PoiMeasurementShape.Point ? 1 : point.Width,
                    Height = point.Shape == PoiMeasurementShape.Point ? 1 : point.Height
                };
            }

            PoiResultV1[] nativeResults = new PoiResultV1[points.Count];
            PoiOptionsV2 options = PoiOptionsV2.Create();
            if (preserveNonPositiveValues) options.Flags = PoiOptionsFlagsV2.PreserveNonPositiveValues;
            int result = OpenCVCalibration.M_CalculatePoiBatchV2(
                width,
                height,
                bitsPerChannel,
                channels,
                cieData,
                checked((ulong)cieByteLength / sizeof(float)),
                requests,
                checked((uint)requests.Length),
                in options,
                nativeResults);
            if (result != OpenCVCalibration.PoiOk)
            {
                throw new InvalidOperationException($"Native batch POI calculation failed with error code {result}.");
            }

            PoiMeasurementResult[] results = new PoiMeasurementResult[nativeResults.Length];
            for (int index = 0; index < nativeResults.Length; index++)
            {
                PoiResultV1 native = nativeResults[index];
                results[index] = new PoiMeasurementResult(
                    native.X,
                    native.Y,
                    native.Z,
                    native.ChromaX,
                    native.ChromaY,
                    native.u,
                    native.v,
                    native.Cct,
                    native.Wave);
            }
            return results;
        }

        internal static void ValidateLayout(int width, int height, int bitsPerChannel, int channels, long cieByteLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            if (bitsPerChannel != 32)
            {
                throw new NotSupportedException($"POI measurement requires 32-bit floating-point CIE data; received {bitsPerChannel} bits.");
            }
            if (channels is not (1 or 3))
            {
                throw new NotSupportedException($"POI measurement supports one or three CIE channels; received {channels}.");
            }

            long requiredLength = checked((long)width * height * channels * sizeof(float));
            if (cieByteLength < requiredLength || cieByteLength % sizeof(float) != 0)
            {
                throw new ArgumentException(
                    $"CIE buffer length is invalid. At least {requiredLength} bytes are required; received {cieByteLength}.",
                    nameof(cieByteLength));
            }
        }

        private static void ValidatePoint(PoiMeasurementPoint point, int width, int height, int index)
        {
            if (point.X < 0 || point.X >= width || point.Y < 0 || point.Y >= height)
            {
                throw new ArgumentOutOfRangeException(nameof(point), $"POI {index} center ({point.X}, {point.Y}) is outside the image.");
            }
            if (point.Width <= 0 || point.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(point), $"POI {index} dimensions must be positive.");
            }
        }

        private static int ToNativeType(PoiMeasurementShape shape)
        {
            return shape switch
            {
                PoiMeasurementShape.Point => 0,
                PoiMeasurementShape.Circle => 1,
                PoiMeasurementShape.Rect => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unsupported POI shape.")
            };
        }
    }
}
