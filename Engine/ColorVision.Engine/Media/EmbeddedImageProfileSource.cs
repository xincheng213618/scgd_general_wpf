using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ColorVision.Engine.Media;

/// <summary>One content activation; revocation does not wait for a borrowed native buffer.</summary>
internal sealed class EmbeddedProfileLifetime
{
    private int _retired;
    internal void Retire() => Interlocked.Exchange(ref _retired, 1);
    internal void CheckActive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _retired) != 0, this);
}

/// <summary>A request-owned reader borrowing this content's measurement buffer, never its file path.</summary>
internal sealed class EmbeddedImageProfileSource : IImageProfileMeasurementSource
{
    private PoiMeasurementBuffer? _buffer;
    private readonly CancellationToken _token;
    private readonly int _bits;
    private readonly EmbeddedProfileLifetime _lifetime;
    public int Width { get; }
    public int Height { get; }
    public int RawChannelCount { get; }
    public IReadOnlyList<string> ChannelNames { get; }
    public string FormatName => $"Decoded CIE {_bits}-bit";
    public string ValueDescription => "Original measurement samples; no normalization or clamping; x/y is computed after XYZ interpolation.";
    public string? GetUnit(int channel) => ChannelNames[channel] is "CIE x" or "CIE y" ? "1" : null;

    internal static IReadOnlyList<ImageProfileSourceOption> CreateOptions(PoiMeasurementBuffer buffer, ColorCalibrationSnapshot? snapshot, EmbeddedProfileLifetime lifetime)
    {
        if (buffer.RawSource is { } raw)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return [new("原始图像 / CIE 全部通道", token => new CvRawProfileSource(raw, snapshot, token, lifetime.CheckActive))];
        }
        return [new("CIE 全部通道", token => new EmbeddedImageProfileSource(buffer, token, lifetime))];
    }

    private EmbeddedImageProfileSource(PoiMeasurementBuffer buffer, CancellationToken token, EmbeddedProfileLifetime lifetime)
    {
        token.ThrowIfCancellationRequested();
        lifetime.CheckActive();
        buffer.Borrow((_, _) => 0);
        _buffer = buffer; _token = token; _bits = buffer.BitsPerChannel; _lifetime = lifetime;
        Width = buffer.Width; Height = buffer.Height; RawChannelCount = buffer.Channels;
        ChannelNames = Array.AsReadOnly(buffer.Channels == 1 ? new[] { "CIE Y" }
            : new[] { "CIE X", "CIE Y", "CIE Z", "CIE x", "CIE y" });
    }

    public double GetChannelValue(int channel, ReadOnlySpan<double> sampledValues) => channel < RawChannelCount
        ? sampledValues[channel] : CvcieProfileSource.Chromaticity(sampledValues[0], sampledValues[1], sampledValues[2], channel - 3);

    public unsafe double Read(int x, int y, int channel)
    {
        PoiMeasurementBuffer buffer = Volatile.Read(ref _buffer) ?? throw new ObjectDisposedException(nameof(EmbeddedImageProfileSource));
        _token.ThrowIfCancellationRequested();
        _lifetime.CheckActive();
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)channel >= (uint)RawChannelCount)
            throw new ArgumentOutOfRangeException(nameof(x));
        return buffer.Borrow((pointer, _) =>
        {
            long offset = ((long)channel * Height + y) * Width + x;
            return _bits == 32 ? ((float*)pointer)[offset] : ((double*)pointer)[offset];
        });
    }

    public void Dispose() => Interlocked.Exchange(ref _buffer, null);
}
