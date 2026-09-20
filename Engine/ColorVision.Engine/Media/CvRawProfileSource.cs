using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Media;

/// <summary>Reads original RAW and replays calibration only at the sampled pixels.</summary>
internal sealed class CvRawProfileSource : IImageProfileMeasurementSource
{
    private readonly FileStream _stream;
    private readonly MemoryMappedFile _mapping;
    private readonly MemoryMappedViewAccessor _view;
    private readonly long _payloadStart;
    private readonly int _bpp;
    private readonly int _channels;
    private readonly bool _interleaved;
    private readonly RawColorTransformV1? _transform;
    private readonly CancellationToken _token;
    // Bilinear sampling revisits the same four neighbours for each channel.
    private readonly (long Pixel, float[] Values)[] _cache = new (long, float[])[4];
    private int _cacheIndex;
    private bool _disposed;

    public int Width { get; }
    public int Height { get; }
    public int RawChannelCount => _channels * (_transform.HasValue ? 2 : 1);
    public IReadOnlyList<string> ChannelNames { get; }
    public string FormatName => $"CVRAW {_bpp}-bit; original pixels";
    public string ValueDescription => _transform.HasValue
        ? "RGB/Gray in DN; calibrated XYZ matches native float output before interpolation; x/y is computed after XYZ interpolation. No normalization or clamping."
        : "Original RAW pixels in DN; no normalization or clamping.";
    public string? GetUnit(int channel) => ChannelNames[channel] switch { "CIE x" or "CIE y" => "1", "CIE X" or "CIE Y" or "CIE Z" => null, _ => "DN" };

    internal static IReadOnlyList<ImageProfileSourceOption> CreateOptions(string path)
    {
        FileInfo identity = new(path);
        long length = identity.Length;
        DateTime modified = identity.LastWriteTimeUtc;
        return [new("原始图像 / CIE 全部通道", token =>
        {
            var source = new CvRawProfileSource(path, true, token);
            try
            {
                FileInfo current = new(path);
                if (current.Length == length && current.LastWriteTimeUtc == modified) return source;
                throw new IOException("CVRAW 文件已改变，请重新打开图像后分析。");
            }
            catch { source.Dispose(); throw; }
        })];
    }

    internal CvRawProfileSource(string path, bool calibrated, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            int headerEnd = CVFileUtil.ReadCIEFileHeader(path, out CVCIEFile header);
            using (header)
            {
                if (headerEnd <= 0 || header.FileExtType != CVType.Raw || header.Version is < 1 or > 3
                    || header.Rows <= 0 || header.Cols <= 0 || header.Channels is not (1 or 3) || header.Bpp is not (8 or 16))
                    throw new InvalidDataException("CVRAW 剖面数据头无效。");
                Width = header.Cols; Height = header.Rows; _bpp = header.Bpp; _channels = header.Channels;
                ColorCalibrationSnapshot? snapshot = calibrated ? ColorCalibrationSnapshot.Read(path, header) : null;
                if (calibrated && snapshot?.CanReplay != true) throw new InvalidDataException("CVRAW 没有可重放的色度校正参数。");
                _interleaved = snapshot?.InterleavedBgr ?? true;
                _transform = snapshot?.ToNative();
                string[] rawNames = _channels == 1 ? ["Gray"] : calibrated ? ["B", "G", "R"] : ["B", "G", "R", "Luminance"];
                ChannelNames = Array.AsReadOnly(!calibrated ? rawNames : rawNames.Concat(_channels == 1
                    ? new[] { "CIE Y" } : new[] { "CIE X", "CIE Y", "CIE Z", "CIE x", "CIE y" }).ToArray());
                _stream.Position = headerEnd;
                using var reader = new BinaryReader(_stream, System.Text.Encoding.UTF8, leaveOpen: true);
                long bytes = header.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
                _payloadStart = _stream.Position;
                if (bytes < checked((long)Width * Height * _channels * (_bpp / 8)) || bytes > _stream.Length - _payloadStart)
                    throw new InvalidDataException("CVRAW 像素数据不完整。");
            }
            token.ThrowIfCancellationRequested();
            _mapping = MemoryMappedFile.CreateFromFile(_stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
            try { _view = _mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read); }
            catch { _mapping.Dispose(); throw; }
            _token = token;
        }
        catch { _stream.Dispose(); throw; }
    }

    public double GetChannelValue(int channel, ReadOnlySpan<double> values)
    {
        if (_channels == 1) return values[channel];
        if (!_transform.HasValue) return channel < 3 ? values[channel] : 0.114 * values[0] + 0.587 * values[1] + 0.299 * values[2];
        return channel switch
        {
            < 6 => values[channel],
            _ => CvcieProfileSource.Chromaticity(values[3], values[4], values[5], channel == 6 ? 0 : 1),
        };
    }

    public double Read(int x, int y, int channel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _token.ThrowIfCancellationRequested();
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)channel >= (uint)RawChannelCount)
            throw new ArgumentOutOfRangeException(nameof(x));
        long pixel = (long)y * Width + x;
        if (channel < _channels) return ReadRaw(pixel, channel);
        foreach (var cached in _cache)
            if (cached.Values != null && cached.Pixel == pixel) return cached.Values[channel - _channels];
        float[] values = TransformPixel(pixel);
        _cache[_cacheIndex++ % _cache.Length] = (pixel, values);
        return values[channel - _channels];
    }

    private double ReadRaw(long pixel, int channel)
    {
        long element = _interleaved || _channels == 1 ? pixel * _channels + channel : (2 - channel) * (long)Width * Height + pixel;
        long offset = checked(_payloadStart + element * (_bpp / 8));
        return _bpp == 8 ? _view.ReadByte(offset) : _view.ReadUInt16(offset);
    }

    private unsafe float[] TransformPixel(long pixel)
    {
        byte* raw = stackalloc byte[6];
        for (int channel = 0; channel < _channels; channel++)
        {
            // A 1x1 planar RGB buffer and a 1x1 interleaved BGR buffer differ only in order.
            int inputChannel = _interleaved || _channels == 1 ? channel : 2 - channel;
            double value = ReadRaw(pixel, inputChannel);
            if (_bpp == 8) raw[channel] = (byte)value;
            else ((ushort*)raw)[channel] = (ushort)value;
        }
        RawColorTransformV1 transform = _transform!.Value;
        float[] values = new float[_channels];
        fixed (float* output = values)
        {
            int status = OpenCVMediaHelper.M_TransformRawColorV1(1, 1, _bpp, (IntPtr)raw, (ulong)(_channels * (_bpp / 8)),
                in transform, -1, (IntPtr)output, (ulong)_channels);
            if (status != OpenCVCalibration.PoiOk) throw new InvalidOperationException($"RAW 色度计算失败：{status}。");
        }
        return values;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _view.Dispose(); _mapping.Dispose(); _stream.Dispose();
    }
}
