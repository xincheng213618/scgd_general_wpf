using ColorVision.FileIO;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Creation;

public sealed record CalibrationRawAverage(int Width, int Height, int BitsPerChannel, int Channels, double[] Values, int FrameCount);

/// <summary>Offline creation only. Inputs must be uncorrected dark or flat-field captures at matching settings.</summary>
public static class CalibrationMapCreation
{
    public static CalibrationRawAverage Average(IReadOnlyList<string> paths, CancellationToken cancellationToken = default, IProgress<string>? progress = null, bool interleavedBgr = true)
    {
        if (paths.Count == 0) throw new ArgumentException("请导入至少一张未校正的 CVRAW 图像。");
        CalibrationRawAverage? result = null;
        float[]? exposure = null; float? gain = null;
        byte[] buffer = new byte[65536];
        foreach (string path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(Path.GetExtension(path), ".cvraw", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("只接受未校正的 CVRAW 文件。");
            if (CVFileMetadata.Read(path).ContainsKey("colorvision.calibration.color")) throw new InvalidDataException("输入文件含颜色校正元数据，请使用采集时关闭所有校正的原始图像。");
            using FileStream stream = File.OpenRead(path);
            int offset = CVFileUtil.ReadCIEFileHeader(stream, out CVCIEFile info);
            using (info)
            {
                if (offset < 0 || info.Cols <= 0 || info.Rows <= 0 || info.Channels is not (1 or 3) || info.Bpp is not (8 or 16)) throw new InvalidDataException("CVRAW 必须是 8/16 位、单通道或三通道图像。");
                int count = checked(info.Cols * info.Rows * info.Channels);
                if (result == null) result = new(info.Cols, info.Rows, info.Bpp, info.Channels, new double[count], paths.Count);
                else if (result.Width != info.Cols || result.Height != info.Rows || result.Channels != info.Channels || result.BitsPerChannel != info.Bpp) throw new InvalidDataException("所有输入图像的宽、高、位深和通道数必须相同。");
                if (!float.IsFinite(info.Gain) || info.Exp == null || info.Exp.Length != info.Channels) throw new InvalidDataException("输入采集参数无效。");
                if (gain.HasValue && gain.Value != info.Gain) throw new InvalidDataException("平均图像的采集增益必须相同。");
                for (int channel = 0; channel < info.Channels; channel++)
                {
                    if (!float.IsFinite(info.Exp[channel]) || info.Exp[channel] <= 0) throw new InvalidDataException("输入曝光必须是正数。");
                    if (exposure != null && exposure[channel] != info.Exp[channel]) throw new InvalidDataException("平均图像的各通道曝光必须相同。");
                }
                exposure ??= (float[])info.Exp.Clone(); gain ??= info.Gain;
                stream.Position = offset;
                using BinaryReader reader = new(stream, System.Text.Encoding.UTF8, true);
                long bytes = info.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
                long required = checked((long)count * (info.Bpp / 8));
                if (bytes < required || bytes > stream.Length - stream.Position) throw new InvalidDataException("CVRAW 像素数据长度无效。");
                int sample = 0;
                while (required > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int length = (int)Math.Min(buffer.Length, required);
                    stream.ReadExactly(buffer.AsSpan(0, length));
                    for (int i = 0; i < length; i += info.Bpp / 8)
                    {
                        int index = info.Channels == 3 && interleavedBgr ? (2 - sample % 3) * (count / 3) + sample / 3 : sample;
                        result.Values[index] += info.Bpp == 8 ? buffer[i] : buffer[i] | buffer[i + 1] << 8;
                        sample++;
                    }
                    required -= length;
                }
            }
            progress?.Report($"已读取 {Path.GetFileName(path)}");
        }
        for (int i = 0; i < result!.Values.Length; i++) result.Values[i] /= paths.Count;
        return result;
    }

    public static void WriteMap(Stream output, CalibrationRawAverage image, CalibrationType type, int centerRadius = 50, CancellationToken cancellationToken = default)
    {
        if (type is not (CalibrationType.DSNU or CalibrationType.Uniformity)) throw new ArgumentException("不支持的校正类型。");
        Validate(image);
        if (centerRadius <= 0) throw new ArgumentOutOfRangeException(nameof(centerRadius));
        using BinaryWriter writer = new(output, System.Text.Encoding.UTF8, true);
        writer.Write((uint)image.Height); writer.Write((uint)image.Width);
        writer.Write(type == CalibrationType.DSNU ? 16u : 32u);
        writer.Write((uint)image.Channels); writer.Write((uint)image.BitsPerChannel);
        int plane = checked(image.Width * image.Height);
        double[] targets = new double[image.Channels];
        for (int channel = 0; channel < image.Channels; channel++)
        {
            double target = 0; int targetCount = 0;
            if (type == CalibrationType.Uniformity)
            {
                for (int y = (int)Math.Max(0, image.Height / 2 - (long)centerRadius); y < (int)Math.Min(image.Height, image.Height / 2 + (long)centerRadius + 1); y++)
                    for (int x = (int)Math.Max(0, image.Width / 2 - (long)centerRadius); x < (int)Math.Min(image.Width, image.Width / 2 + (long)centerRadius + 1); x++)
                    { target += image.Values[channel * plane + y * image.Width + x]; targetCount++; }
                target /= targetCount;
                if (target <= 0) throw new InvalidDataException("均匀场中心亮度为零，不能生成校正。");
            }
            targets[channel] = target;
        }
        // Native V1 map payload is interleaved B,G,R even for planar R,G,B RAW.
        for (int i = 0; i < plane; i++)
        {
            for (int outputChannel = 0; outputChannel < image.Channels; outputChannel++)
            {
                if ((i & 65535) == 0) cancellationToken.ThrowIfCancellationRequested();
                int channel = image.Channels == 3 ? 2 - outputChannel : 0;
                double value = image.Values[channel * plane + i];
                if (type == CalibrationType.DSNU) writer.Write((ushort)Math.Round(value, MidpointRounding.AwayFromZero));
                else
                {
                    if (value <= 0) throw new InvalidDataException("均匀场含零像素，请检查暗场扣除、曝光和缺陷点后重新导入。");
                    float factor = (float)(targets[channel] / value);
                    if (!float.IsFinite(factor) || factor <= 0) throw new InvalidDataException("均匀场增益超出有效范围。");
                    writer.Write(factor);
                }
            }
        }
    }

    public static int WriteDefects(Stream output, CalibrationRawAverage image, double threshold, bool bright, CancellationToken cancellationToken = default)
    {
        Validate(image);
        if (!double.IsFinite(threshold) || threshold < 0 || threshold > (image.BitsPerChannel == 8 ? 255 : 65535)) throw new ArgumentOutOfRangeException(nameof(threshold));
        if (!output.CanSeek) throw new ArgumentException("缺陷点输出流必须可以定位。");
        using BinaryWriter writer = new(output, System.Text.Encoding.UTF8, true);
        long start = output.Position; writer.Write(0u); int count = 0; int plane = checked(image.Width * image.Height);
        for (int i = 0; i < plane; i++)
        {
            if ((i & 65535) == 0) cancellationToken.ThrowIfCancellationRequested();
            bool defect = false;
            for (int channel = 0; channel < image.Channels; channel++) defect |= bright ? image.Values[channel * plane + i] > threshold : image.Values[channel * plane + i] < threshold;
            if (!defect) continue;
            writer.Write((uint)(i / image.Width)); writer.Write((uint)(i % image.Width)); count++;
        }
        long end = output.Position; output.Position = start; writer.Write((uint)count); output.Position = end;
        return count;
    }

    private static void Validate(CalibrationRawAverage image)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.Channels is not (1 or 3) || image.BitsPerChannel is not (8 or 16) || image.FrameCount <= 0 || image.Values.Length != checked(image.Width * image.Height * image.Channels)) throw new InvalidDataException("平均图像布局无效。");
        double maximum = image.BitsPerChannel == 8 ? 255 : 65535;
        foreach (double value in image.Values) if (!double.IsFinite(value) || value < 0 || value > maximum) throw new InvalidDataException("平均图像像素无效。");
    }
}

