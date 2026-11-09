using ColorVision.FileIO;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ColorVision.Engine.Media;

// The caller keeps the read-only stream open for every pass over this payload.
internal readonly record struct CvcieDiskPixels(SafeFileHandle Handle, long Offset)
{
    public static CvcieDiskPixels Locate(FileStream stream, CVCIEFile header, int headerEnd, long requiredBytes, bool requireExactLength)
    {
        int prefixLength = header.Version == 2 ? sizeof(long) : sizeof(int);
        if (headerEnd <= 0 || stream.Length - headerEnd < prefixLength)
            throw new InvalidDataException("CVCIE 内嵌数据缺少长度字段。");

        stream.Position = headerEnd;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        long declared = header.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
        long payloadStart = stream.Position;
        if (requiredBytes <= 0 || declared < requiredBytes || declared > stream.Length - payloadStart
            || (requireExactLength && declared != requiredBytes))
            throw new InvalidDataException("CVCIE 内嵌数据长度与图像布局不匹配。");
        return new CvcieDiskPixels(stream.SafeFileHandle, payloadStart);
    }

    private void ReadExactly(Span<byte> destination, long offset, CancellationToken token)
    {
        long position = checked(Offset + offset);
        while (!destination.IsEmpty)
        {
            token.ThrowIfCancellationRequested();
            int read = RandomAccess.Read(Handle, destination, position);
            if (read == 0) throw new EndOfStreamException("CVCIE 内嵌数据提前结束。");
            destination = destination[read..];
            position += read;
        }
    }

    public readonly ref struct Chunk<T> where T : unmanaged
    {
        private readonly byte[]? _rented;
        public ReadOnlySpan<T> Samples { get; }

        public Chunk(CvcieDiskPixels file, int planePixels, int channels, int start, int count, CancellationToken token)
        {
            int sampleBytes = Unsafe.SizeOf<T>();
            int chunkPlaneBytes = count * sampleBytes;
            long planeStride = (long)planePixels * sampleBytes;
            long offset = (long)start * sampleBytes;
            byte[] rented = ArrayPool<byte>.Shared.Rent(channels * chunkPlaneBytes);
            try
            {
                Span<byte> bytes = rented.AsSpan(0, channels * chunkPlaneBytes);
                for (int channel = 0; channel < channels; channel++)
                    file.ReadExactly(bytes.Slice(channel * chunkPlaneBytes, chunkPlaneBytes), offset + channel * planeStride, token);
                Samples = MemoryMarshal.Cast<byte, T>(bytes);
                _rented = rented;
            }
            catch
            {
                ArrayPool<byte>.Shared.Return(rented);
                throw;
            }
        }

        public void Dispose()
        {
            if (_rented != null) ArrayPool<byte>.Shared.Return(_rented);
        }
    }
}
