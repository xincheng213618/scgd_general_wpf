using System;
using System.Collections.Generic;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms;

/// <summary>A request-owned measurement plane. Reads retain double precision and never normalize values.</summary>
public interface IImageProfileMeasurementSource : IDisposable
{
    int Width { get; }
    int Height { get; }
    IReadOnlyList<string> ChannelNames { get; }
    int RawChannelCount { get; }
    string ValueDescription { get; }
    string? GetUnit(int channel);
    double GetChannelValue(int channel, ReadOnlySpan<double> sampledValues);
    string FormatName { get; }
    double Read(int x, int y, int channel);
}

/// <summary>Reads all raw channels for each requested pixel, in point order and without normalization.</summary>
public interface IBatchedImageProfileMeasurementSource : IImageProfileMeasurementSource
{
    /// <summary>Writes <see cref="IImageProfileMeasurementSource.RawChannelCount"/> consecutive values per pixel.</summary>
    void ReadPixels(ReadOnlyMemory<(int X, int Y)> pixels, Memory<double> destination);
}

/// <summary>An immutable source choice. Opening happens on a worker; each call owns its reader.</summary>
public sealed record ImageProfileSourceOption(string Name, Func<CancellationToken, IImageProfileMeasurementSource> Open);
