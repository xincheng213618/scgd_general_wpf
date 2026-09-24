using ColorVision.FileIO;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
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
    private readonly bool _allChannels;

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<string> ChannelNames { get; }
    public int RawChannelCount => _channels.Length;
    public string ValueDescription => _chromaticity || _allChannels
        ? "Y is original; x=X/(X+Y+Z), y=Y/(X+Y+Z), computed after XYZ interpolation; invalid/zero sums produce NaN."
        : "Original measurement plane; no normalization or clamping.";
    public string? GetUnit(int channel) => ChannelNames[channel] is "CIE x" or "CIE y" ? "1" : null;

    public double GetChannelValue(int channel, ReadOnlySpan<double> sampledValues)
    {
        if (_allChannels)
            return channel < 3 ? sampledValues[channel] : Chromaticity(sampledValues[0], sampledValues[1], sampledValues[2], channel - 3);
        if (!_chromaticity) return sampledValues[channel];
        if (_includeY && channel == 0) return sampledValues[1];
        return Chromaticity(sampledValues[0], sampledValues[1], sampledValues[2], channel - (_includeY ? 1 : 0));
    }

    internal static double Chromaticity(double x, double y, double z, int channel)
    {
        double sum = x + y + z;
        return !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || !double.IsFinite(sum) || sum == 0
            ? double.NaN : (channel == 0 ? x : y) / sum;
    }
    public string FormatName => $"CVCIE planar {_bpp}-bit";

    public static IReadOnlyList<ImageProfileSourceOption> CreateOptions(string path, int channels)
    {
        FileInfo identity = new(path);
        long length = identity.Length;
        DateTime modified = identity.LastWriteTimeUtc;
        string? rawPath = FindAssociatedRaw(path);
        FileInfo? rawIdentity = rawPath == null ? null : new FileInfo(rawPath);
        long? rawLength = rawIdentity?.Length;
        DateTime? rawModified = rawIdentity?.LastWriteTimeUtc;
        ImageProfileSourceOption Option(string name, int[] indices, bool chromaticity = false, bool includeY = false, bool allChannels = false) => new(name, token =>
        {
            IImageProfileMeasurementSource source = new CvcieProfileSource(path, indices, token, chromaticity, includeY, allChannels);
            try
            {
                FileInfo current = new(path);
                if (current.Length != length || current.LastWriteTimeUtc != modified)
                    throw new IOException("CVCIE 文件已改变，请重新打开图像后分析。");
                if (allChannels && rawPath != null)
                {
                    var raw = new CvRawProfileSource(rawPath, false, token);
                    try
                    {
                        FileInfo currentRaw = new(rawPath);
                        if (currentRaw.Length != rawLength || currentRaw.LastWriteTimeUtc != rawModified)
                            throw new IOException("关联 CVRAW 文件已改变，请重新打开图像后分析。");
                        source = new CombinedProfileSource(raw, source);
                    }
                    catch { raw.Dispose(); throw; }
                }
                return source;
            }
            catch { source.Dispose(); throw; }
        });
        ImageProfileSourceOption all = Option(rawPath == null ? "CIE 全部通道" : "原始图像 / CIE 全部通道", channels == 1 ? [0] : [0, 1, 2], allChannels: true);
        return new[] { all }.Concat(channels == 1
            ? [Option("CIE Y", [0])]
            : [Option("CIE Y", [1]), Option("CIE x/y", [0, 1, 2], true),
                Option("CIE Yxy", [0, 1, 2], true, true), Option("CIE XYZ", [0, 1, 2])]).ToArray();
    }

    public CvcieProfileSource(string path, int[] channels, CancellationToken cancellationToken = default, bool chromaticity = false, bool includeY = false, bool allChannels = false)
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
                _allChannels = allChannels && header.Channels == 3;
                if (_allChannels && !channels.SequenceEqual(new[] { 0, 1, 2 })) throw new InvalidDataException("全部 CIE 通道需要完整的原始 XYZ。");
                ChannelNames = Array.AsReadOnly(_allChannels ? ["CIE X", "CIE Y", "CIE Z", "CIE x", "CIE y"]
                    : chromaticity ? includeY ? ["CIE Y", "CIE x", "CIE y"] : new[] { "CIE x", "CIE y" } : names);
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

    private static string? FindAssociatedRaw(string path)
    {
        if (CVFileUtil.ReadCIEFileHeader(path, out CVCIEFile header) <= 0) { header.Dispose(); return null; }
        using (header)
        {
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            List<string> candidates = new();
            if (!string.IsNullOrWhiteSpace(header.SrcFileName))
            {
                try
                {
                    candidates.Add(Path.Combine(directory, header.SrcFileName));
                    candidates.Add(Path.Combine(directory, Path.GetFileName(header.SrcFileName)));
                }
                catch (ArgumentException) { } // A malformed optional link must not hide embedded CIE data.
            }
            candidates.Add(Path.ChangeExtension(path, ".cvraw"));
            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!string.Equals(Path.GetExtension(candidate), ".cvraw", StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate)) continue;
                    using var source = new CvRawProfileSource(candidate, false);
                    if (source.Width == header.Cols && source.Height == header.Rows) return candidate;
                }
                catch (InvalidDataException) { }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (ArgumentException) { }
            }
            return null;
        }
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
