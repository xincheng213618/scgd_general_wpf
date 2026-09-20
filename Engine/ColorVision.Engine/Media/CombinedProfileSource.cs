using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Media;

/// <summary>Owns aligned original pixels and CIE planes, sampled along the same path.</summary>
internal sealed class CombinedProfileSource : IImageProfileMeasurementSource
{
    private readonly IImageProfileMeasurementSource _raw;
    private readonly IImageProfileMeasurementSource _cie;
    private readonly int[] _rawOutputChannels;
    public int Width => _cie.Width;
    public int Height => _cie.Height;
    public int RawChannelCount => _raw.RawChannelCount + _cie.RawChannelCount;
    public IReadOnlyList<string> ChannelNames { get; }
    public string FormatName => $"{_raw.FormatName} + {_cie.FormatName}";
    public string ValueDescription => $"{_raw.ValueDescription} {_cie.ValueDescription}";

    internal CombinedProfileSource(IImageProfileMeasurementSource raw, IImageProfileMeasurementSource cie)
    {
        if (raw.Width != cie.Width || raw.Height != cie.Height) throw new ArgumentException("原图与 CIE 测量尺寸不一致。");
        _raw = raw; _cie = cie;
        _rawOutputChannels = Enumerable.Range(0, raw.ChannelNames.Count).Where(index => raw.ChannelNames[index] != "Luminance").ToArray();
        ChannelNames = Array.AsReadOnly(_rawOutputChannels.Select(index => raw.ChannelNames[index]).Concat(cie.ChannelNames).ToArray());
    }
    public string? GetUnit(int channel) => channel < _rawOutputChannels.Length ? _raw.GetUnit(_rawOutputChannels[channel]) : _cie.GetUnit(channel - _rawOutputChannels.Length);
    public double Read(int x, int y, int channel) => channel < _raw.RawChannelCount ? _raw.Read(x, y, channel) : _cie.Read(x, y, channel - _raw.RawChannelCount);
    public double GetChannelValue(int channel, ReadOnlySpan<double> sampledValues) => channel < _rawOutputChannels.Length
        ? _raw.GetChannelValue(_rawOutputChannels[channel], sampledValues[.._raw.RawChannelCount])
        : _cie.GetChannelValue(channel - _rawOutputChannels.Length, sampledValues[_raw.RawChannelCount..]);
    public void Dispose() { try { _raw.Dispose(); } finally { _cie.Dispose(); } }
}
