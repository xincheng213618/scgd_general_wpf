namespace ColorVision.Algorithms;

/// <summary>An owned, tightly packed or explicitly-strided image buffer independent of UI and provider libraries.</summary>
public sealed class AlgorithmImageBuffer : IDisposable
{
    private SharedImageData? _data;

    public AlgorithmImageBuffer(int width, int height, int stride, AlgorithmImageFormat format, byte[] data, double dpiX = 96, double dpiY = 96)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        int minimumStride = checked(width * format.BytesPerPixel());
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, minimumStride);
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < checked(stride * height)) throw new ArgumentException("The image buffer is smaller than its declared layout.", nameof(data));
        if (!double.IsFinite(dpiX) || dpiX <= 0) throw new ArgumentOutOfRangeException(nameof(dpiX));
        if (!double.IsFinite(dpiY) || dpiY <= 0) throw new ArgumentOutOfRangeException(nameof(dpiY));

        Width = width;
        Height = height;
        Stride = stride;
        Format = format;
        DpiX = dpiX;
        DpiY = dpiY;
        _data = new SharedImageData(data);
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public AlgorithmImageFormat Format { get; }

    public double DpiX { get; }

    public double DpiY { get; }

    public bool IsDisposed => Volatile.Read(ref _data) == null;

    public ReadOnlyMemory<byte> Data => GetData().Data;

    /// <summary>
    /// Retains the immutable image bytes without copying them. The lease remains readable after this
    /// buffer is disposed and must itself be disposed by long-lived consumers such as presenters.
    /// </summary>
    public AlgorithmImageBufferLease AcquireReadOnlyLease()
        => new(Width, Height, Stride, Format, DpiX, DpiY, GetData());

    public AlgorithmImageBuffer Clone() => new(Width, Height, Stride, Format, Data.ToArray(), DpiX, DpiY);

    public void Dispose()
    {
        Interlocked.Exchange(ref _data, null);
    }

    private SharedImageData GetData()
        => Volatile.Read(ref _data) ?? throw new ObjectDisposedException(nameof(AlgorithmImageBuffer));

    internal sealed class SharedImageData(byte[] data)
    {
        public ReadOnlyMemory<byte> Data { get; } = data;
    }
}

/// <summary>A disposable, zero-copy read-only hold on an <see cref="AlgorithmImageBuffer"/>.</summary>
public sealed class AlgorithmImageBufferLease : IDisposable
{
    private AlgorithmImageBuffer.SharedImageData? _data;

    internal AlgorithmImageBufferLease(
        int width,
        int height,
        int stride,
        AlgorithmImageFormat format,
        double dpiX,
        double dpiY,
        AlgorithmImageBuffer.SharedImageData data)
    {
        Width = width;
        Height = height;
        Stride = stride;
        Format = format;
        DpiX = dpiX;
        DpiY = dpiY;
        _data = data;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public AlgorithmImageFormat Format { get; }

    public double DpiX { get; }

    public double DpiY { get; }

    public bool IsDisposed => Volatile.Read(ref _data) == null;

    public ReadOnlyMemory<byte> Data
        => (Volatile.Read(ref _data) ?? throw new ObjectDisposedException(nameof(AlgorithmImageBufferLease))).Data;

    public void Dispose() => Interlocked.Exchange(ref _data, null);
}

public enum AlgorithmInputOwnership
{
    Borrowed,
    Transferred,
}

public sealed class AlgorithmInput
{
    public required string Name { get; init; }

    public required AlgorithmImageBuffer Image { get; init; }

    public AlgorithmInputOwnership Ownership { get; init; } = AlgorithmInputOwnership.Borrowed;

    public string? SourceRevision { get; init; }

    public string? SourceUri { get; init; }

    public string? Checksum { get; init; }

    /// <summary>Stable color-space or encoded-sample label used by multi-input algorithms to reject ambiguous comparisons.</summary>
    public string? ColorSpace { get; init; }
}
