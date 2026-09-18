using ColorVision.FileIO;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace ColorVision.Engine.Media;

/// <summary>Request-owned, read-only mapping of the file's planar measurement payload.</summary>
public sealed class CvcieProfileSource : IImageProfileMeasurementSource
{
    private readonly FileStream _stream;
    private readonly MemoryMappedFile _mapping;
    private readonly MemoryMappedViewAccessor _view;
    private readonly long _payloadStart;
    private readonly long _planeBytes;
    private readonly int _bpp;
    private readonly int[] _channels;
    private readonly CancellationToken _cancellationToken;
    private bool _disposed;
    private readonly bool _chromaticity;
    private readonly bool _includeY;

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<string> ChannelNames { get; }
    public int RawChannelCount => _channels.Length;
    public string ValueDescription => _chromaticity
        ? "Y is original; x=X/(X+Y+Z), y=Y/(X+Y+Z), computed after XYZ interpolation; invalid/zero sums produce NaN."
        : "Original measurement plane; no normalization or clamping.";
    public string? GetUnit(int channel) => ChannelNames[channel] is "CIE x" or "CIE y" ? "1" : null;

    public double GetChannelValue(int channel, ReadOnlySpan<double> sampledValues)
    {
        if (!_chromaticity) return sampledValues[channel];
        if (_includeY && channel == 0) return sampledValues[1];
        double sum = sampledValues[0] + sampledValues[1] + sampledValues[2];
        if (!double.IsFinite(sampledValues[0]) || !double.IsFinite(sampledValues[1])
            || !double.IsFinite(sampledValues[2]) || !double.IsFinite(sum) || sum == 0) return double.NaN;
        return sampledValues[channel - (_includeY ? 1 : 0)] / sum;
    }
    public string FormatName => $"CVCIE planar {_bpp}-bit";

    public static IReadOnlyList<ImageProfileSourceOption> CreateOptions(string path, int channels)
    {
        FileInfo identity = new(path);
        long length = identity.Length;
        DateTime modified = identity.LastWriteTimeUtc;
        ImageProfileSourceOption Option(string name, int[] indices, bool chromaticity = false, bool includeY = false) => new(name, token =>
        {
            var source = new CvcieProfileSource(path, indices, token, chromaticity, includeY);
            try
            {
                FileInfo current = new(path);
                if (current.Length == length && current.LastWriteTimeUtc == modified) return source;
                throw new IOException("CVCIE 文件已改变，请重新打开图像后分析。");
            }
            catch { source.Dispose(); throw; }
        });
        return channels == 1
            ? [Option("CIE Y", [0])]
            : [Option("CIE Y", [1]), Option("CIE x/y", [0, 1, 2], true),
                Option("CIE Yxy", [0, 1, 2], true, true), Option("CIE XYZ", [0, 1, 2])];
    }

    public CvcieProfileSource(string path, int[] channels, CancellationToken cancellationToken = default, bool chromaticity = false, bool includeY = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(Path.GetExtension(path), ".cvcie", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("原始 CIE 剖面需要 CVCIE 文件。");
        _stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            int headerEnd = CVFileUtil.ReadCIEFileHeader(path, out CVCIEFile header);
            using (header)
            {
                if (headerEnd <= 0 || header.Version is < 1 or > 3 || header.Rows <= 0 || header.Cols <= 0
                    || header.Channels is not (1 or 3) || header.Bpp is not (8 or 16 or 32 or 64)
                    || channels.Length is < 1 or > 3)
                    throw new InvalidDataException("CVCIE 测量数据头无效。");
                _channels = (int[])channels.Clone();
                var names = new string[channels.Length];
                for (int i = 0; i < channels.Length; i++)
                {
                    if (channels[i] < 0 || channels[i] >= header.Channels)
                        throw new InvalidDataException("CVCIE 测量通道不存在。");
                    names[i] = header.Channels == 1 ? "CIE Y" : new[] { "CIE X", "CIE Y", "CIE Z" }[channels[i]];
                }
                Width = header.Cols;
                Height = header.Rows;
                _bpp = header.Bpp;
                if (chromaticity && (header.Channels != 3 || channels.Length != 3
                    || channels[0] != 0 || channels[1] != 1 || channels[2] != 2))
                    throw new InvalidDataException("色度计算需要完整的原始 XYZ。");
                _chromaticity = chromaticity;
                _includeY = includeY;
                ChannelNames = Array.AsReadOnly(chromaticity ? includeY ? ["CIE Y", "CIE x", "CIE y"] : new[] { "CIE x", "CIE y" } : names);
                _planeBytes = checked((long)Width * Height * (_bpp / 8));
                _stream.Position = headerEnd;
                using var reader = new BinaryReader(_stream, System.Text.Encoding.UTF8, leaveOpen: true);
                long payloadBytes = header.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
                _payloadStart = _stream.Position;
                if (payloadBytes < checked(_planeBytes * header.Channels) || payloadBytes > _stream.Length - _payloadStart)
                    throw new InvalidDataException("CVCIE 测量数据不完整。");
            }
            cancellationToken.ThrowIfCancellationRequested();
            _mapping = MemoryMappedFile.CreateFromFile(_stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
            try { _view = _mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read); }
            catch { _mapping.Dispose(); throw; }
            _cancellationToken = cancellationToken;
        }
        catch { _stream.Dispose(); throw; }
    }

    public double Read(int x, int y, int channel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cancellationToken.ThrowIfCancellationRequested();
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)channel >= (uint)_channels.Length)
            throw new ArgumentOutOfRangeException(nameof(x));
        long offset = checked(_payloadStart + _planeBytes * _channels[channel] + ((long)y * Width + x) * (_bpp / 8));
        return _bpp switch
        {
            8 => _view.ReadByte(offset),
            16 => _view.ReadUInt16(offset),
            32 => _view.ReadSingle(offset),
            64 => _view.ReadDouble(offset),
            _ => throw new InvalidDataException(),
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _view.Dispose();
        _mapping.Dispose();
        _stream.Dispose();
    }
}
