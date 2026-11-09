using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices.ComTypes;

namespace ColorVision.ShellExtension.Interop;

/// <summary>Read-only adapter that borrows the COM stream for initialization.</summary>
internal sealed class ComInputStream : Stream
{
    private const int ChunkSize = 64 * 1024;
    private readonly IStream _source;
    private byte[]? _chunk;

    public ComInputStream(IStream source)
    {
        _source = source;
        source.Stat(out STATSTG stat, 1); // STATFLAG_NONAME
        Length = stat.cbSize;
        _chunk = ArrayPool<byte>.Shared.Rent(ChunkSize);
    }

    public override bool CanRead => _chunk != null;
    public override bool CanSeek => _chunk != null;
    public override bool CanWrite => false;
    public override long Length { get; }
    public override long Position
    {
        get => Seek(0, SeekOrigin.Current);
        set => Seek(value, SeekOrigin.Begin);
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override unsafe int Read(Span<byte> destination)
    {
        byte[] chunk = _chunk ?? throw new ObjectDisposedException(nameof(ComInputStream));
        int requested = Math.Min(destination.Length, ChunkSize);
        if (requested == 0) return 0;
        int read = 0;
        _source.Read(chunk, requested, (IntPtr)(&read));
        if ((uint)read > (uint)requested) throw new IOException("COM stream returned an invalid byte count.");
        chunk.AsSpan(0, read).CopyTo(destination);
        return read;
    }

    public override unsafe long Seek(long offset, SeekOrigin origin)
    {
        ObjectDisposedException.ThrowIf(_chunk == null, this);
        long position = 0;
        _source.Seek(offset, (int)origin, (IntPtr)(&position));
        return position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && _chunk is { } chunk)
        {
            _chunk = null;
            ArrayPool<byte>.Shared.Return(chunk);
        }
        base.Dispose(disposing);
    }
}
