using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Buffers.Binary;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms;

public sealed partial class DisplayMetrologyProvider
{
    // The buffer records storage depth, not ADC depth. Left-aligned 8/10/12/14-bit
    // samples clip at 65280/65472/65520/65532. Keep one 8-bit code width of headroom
    // (less than 0.4% of Gray16 full scale) as a conservative rejection boundary;
    // this does not infer effective depth or saturation from image maxima.
    private const ushort RingGhostNearFullScale16 = ushort.MaxValue - byte.MaxValue;

    private static void RejectSaturatedRing(AlgorithmImageBuffer image, int channel, RotatedRect ellipse,
        RingBand band, double step, CancellationToken token)
    {
        int channels = image.Format.Channels(), bytes = image.Format.BytesPerPixel() / channels;
        if (channels == 1) channel = 0;
        double fullScale = bytes == 1 ? byte.MaxValue : bytes == 2 ? ushort.MaxValue : 1;
        double rejectionBoundary = bytes == 2 ? RingGhostNearFullScale16 : fullScale;
        ReadOnlySpan<byte> data = image.Data.Span;
        double angle = ellipse.Angle * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle);
        double reference = (ellipse.Size.Width + ellipse.Size.Height) / 4;
        double halfWidth = ellipse.Size.Width / 2, halfHeight = ellipse.Size.Height / 2;
        double extent = Math.Max(halfWidth, halfHeight) * band.Right * step / reference + 2;
        bool hasNearFullScalePixel = false;
        for (int y = Math.Max(0, (int)(ellipse.Center.Y - extent)); y < Math.Min(image.Height, ellipse.Center.Y + extent); y++)
        {
            token.ThrowIfCancellationRequested();
            for (int x = Math.Max(0, (int)(ellipse.Center.X - extent)); x < Math.Min(image.Width, ellipse.Center.X + extent); x++)
            {
                int offset = y * image.Stride + (x * channels + channel) * bytes;
                // Read before DecodeExponent so changing the known signal encoding
                // cannot hide a near-full-scale acquisition value.
                double value = bytes == 1 ? data[offset]
                    : bytes == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2))
                    : BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4)));
                if (value < rejectionBoundary) continue;
                double dx = x - ellipse.Center.X, dy = y - ellipse.Center.Y;
                double u = (dx * cos + dy * sin) / halfWidth, v = (-dx * sin + dy * cos) / halfHeight;
                double radial = Math.Sqrt(u * u + v * v) * reference;
                if (radial < band.Left * step || radial > band.Right * step) continue;
                if (value >= fullScale)
                    throw new MeasurementException("saturated_primary_ring", "主像测量环存在存储满量程像素，亮度比无效，请降低曝光后重拍。");
                hasNearFullScalePixel = true;
            }
        }
        if (hasNearFullScalePixel)
            throw new MeasurementException("suspected_saturated_primary_ring",
                "主像测量环存在接近 16 位存储满量程的像素（65280～65534），疑似较低有效位深采集的饱和数据。当前没有 ADC 满量程元数据，本次不输出亮度比，请降低曝光后重拍。");
    }
}
