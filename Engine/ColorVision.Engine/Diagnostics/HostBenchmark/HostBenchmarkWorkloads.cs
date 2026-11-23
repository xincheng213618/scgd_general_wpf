using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Diagnostics.HostBenchmark;

internal sealed record HostBenchmarkWorkload(string Id, Action Execute);

internal sealed unsafe class HostBenchmarkWorkloads : IDisposable
{
    internal const int Width = 9568;
    internal const int Height = 6380;
    internal const string Version = "cv-host-cpu-memory-v1";

    private const int Channels = 3;
    private const int PixelCount = Width * Height;
    private const int SamplesPerRow = Width * Channels;
    private const int SampleCount = PixelCount * Channels;

    private ushort* source;
    private ushort* destination;
    private readonly short[] offsets = new short[SamplesPerRow];
    private readonly float[] gains = new float[SamplesPerRow];

    internal HostBenchmarkWorkloads(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            source = (ushort*)NativeMemory.Alloc((nuint)SampleCount, sizeof(ushort));
            destination = (ushort*)NativeMemory.Alloc((nuint)SampleCount, sizeof(ushort));
            Initialize(cancellationToken);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void Initialize(CancellationToken cancellationToken)
    {
        // Touch every page before measuring the preallocated buffers.
        uint state = 0x6d2b79f5;
        for (int row = 0; row < Height; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int start = row * SamplesPerRow;
            int end = start + SamplesPerRow;
            for (int index = start; index < end; index++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                ushort sample = unchecked((ushort)state);
                source[index] = sample;
                destination[index] = sample;
            }
        }
        for (int column = 0; column < SamplesPerRow; column++)
        {
            offsets[column] = (short)((column % 257) - 128);
            gains[column] = 0.875f + (column % 65) / 256f;
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal HostBenchmarkWorkload[] CreateCases() =>
    [
        new("memory-copy", CopyMemory),
        new("channel-swap", SwapChannels),
        new("bayer-rg16", ExpandBayer),
        new("pixel-compute", ComputePixels)
    ];

    private void CopyMemory()
    {
        new ReadOnlySpan<ushort>(source, SampleCount).CopyTo(new Span<ushort>(destination, SampleCount));
        if (destination[0] != source[0] || destination[SampleCount - 1] != source[SampleCount - 1])
            throw new InvalidOperationException("Memory copy produced an unexpected sample.");
    }

    private void SwapChannels()
    {
        ushort firstBlue = destination[0];
        ushort firstRed = destination[2];
        ushort lastBlue = destination[SampleCount - Channels];
        ushort lastRed = destination[SampleCount - 1];

        for (int index = 0; index < SampleCount; index += Channels)
        {
            ushort blue = destination[index];
            destination[index] = destination[index + 2];
            destination[index + 2] = blue;
        }

        if (destination[0] != firstRed || destination[2] != firstBlue
            || destination[SampleCount - Channels] != lastRed || destination[SampleCount - 1] != lastBlue)
            throw new InvalidOperationException("Channel swap produced an unexpected sample.");
    }

    private void ExpandBayer()
    {
        // Use the first plane of source as a tightly packed RGGB image.
        // This workload samples Bayer expansion with a fixed 2x2 rule.
        Parallel.For(0, Height / 2, rowPair =>
        {
            int upper = rowPair * 2 * Width;
            int lower = upper + Width;
            int upperOutput = upper * Channels;
            int lowerOutput = lower * Channels;

            for (int column = 0; column < Width; column += 2)
            {
                ushort red = source[upper + column];
                ushort upperGreen = source[upper + column + 1];
                ushort lowerGreen = source[lower + column];
                ushort blue = source[lower + column + 1];

                destination[upperOutput] = destination[upperOutput + 3] = blue;
                destination[upperOutput + 1] = destination[upperOutput + 4] = upperGreen;
                destination[upperOutput + 2] = destination[upperOutput + 5] = red;
                destination[lowerOutput] = destination[lowerOutput + 3] = blue;
                destination[lowerOutput + 1] = destination[lowerOutput + 4] = lowerGreen;
                destination[lowerOutput + 2] = destination[lowerOutput + 5] = red;

                upperOutput += 2 * Channels;
                lowerOutput += 2 * Channels;
            }
        });

        if (destination[0] != source[Width + 1] || destination[1] != source[1]
            || destination[SamplesPerRow + 1] != source[Width]
            || destination[SampleCount - 1] != source[PixelCount - Width - 2])
            throw new InvalidOperationException("Bayer expansion produced an unexpected sample.");
    }

    private void ComputePixels()
    {
        // Column coefficients keep this workload fixed without loading calibration maps.
        Parallel.For(0, Height, row =>
        {
            int start = row * SamplesPerRow;
            ReadOnlySpan<ushort> input = new(source + start, SamplesPerRow);
            Span<ushort> output = new(destination + start, SamplesPerRow);
            for (int column = 0; column < output.Length; column++)
                output[column] = CorrectSample(input[column], offsets[column], gains[column]);
        });

        if (destination[0] != CorrectSample(source[0], offsets[0], gains[0])
            || destination[SampleCount - 1] != CorrectSample(source[SampleCount - 1], offsets[^1], gains[^1]))
            throw new InvalidOperationException("Pixel computation produced an unexpected sample.");
    }

    private static ushort CorrectSample(ushort sample, short offset, float gain)
        => (ushort)Math.Clamp((sample - offset) * gain, 0f, ushort.MaxValue);

    public void Dispose()
    {
        NativeMemory.Free(source);
        NativeMemory.Free(destination);
        source = null;
        destination = null;
    }
}
