using ColorVision.FileIO;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ColorVision.Engine.Media;

// The caller keeps the read-only stream open for every pass over this payload.
internal readonly partial record struct CvcieDiskPixels(SafeFileHandle Handle, long Offset)
{
    public static CvcieDiskPixels Locate(FileStream stream, CVCIEFile header, int headerEnd, long requiredBytes, bool requireExactLength)
    {
        CvcieDiskPixels? pixels = TryLocate(stream, header, headerEnd, out long declared);
        if (pixels == null || requiredBytes <= 0 || declared < requiredBytes || (requireExactLength && declared != requiredBytes))
            throw new InvalidDataException("CVCIE 内嵌数据长度与图像布局不匹配。");
        return pixels.Value;
    }

    public static CvcieDiskPixels? TryLocate(FileStream stream, CVCIEFile header, int headerEnd, out long declared)
        => TryGetPayloadOffset(stream, header, headerEnd, out declared) is long offset ? new CvcieDiskPixels(stream.SafeFileHandle, offset) : null;

    internal static long? TryGetPayloadOffset(Stream stream, CVCIEFile header, int headerEnd, out long declared)
    {
        declared = 0;
        int prefixLength = header.Version == 2 ? sizeof(long) : sizeof(int);
        if (headerEnd <= 0 || stream.Length - headerEnd < prefixLength)
            return null;

        stream.Position = headerEnd;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        declared = header.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
        long payloadStart = stream.Position;
        return declared > 0 && declared <= stream.Length - payloadStart ? payloadStart : null;
    }

    public void ReadExactly(Span<byte> destination, long offset, CancellationToken token)
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

    internal MappedPlane? TryMapFixedVolume(long byteCount)
    {
        if (!IsOnFixedVolume(Handle)) return null;
        try
        {
            using var mapping = MemoryMappedFile.CreateFromFile(Handle, null, 0, MemoryMappedFileAccess.Read,
                HandleInheritability.None, leaveOpen: true);
            MemoryMappedViewAccessor view = mapping.CreateViewAccessor(Offset, byteCount, MemoryMappedFileAccess.Read);
            try { return new MappedPlane(view); }
            catch { view.Dispose(); throw; }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private static unsafe bool IsOnFixedVolume(SafeFileHandle handle)
    {
        // The opened handle identifies the actual volume even through a directory mount or link.
        // UNC shares and volumes that Windows cannot identify keep the checked read path.
        const uint fileNameOpened = 0x8, volumeNameGuid = 0x1;
        const string volumePrefix = @"\\?\Volume{";
        const int capacity = 1024;
        char* path = stackalloc char[capacity];
        uint length = GetFinalPathNameByHandle(handle, path, capacity, fileNameOpened | volumeNameGuid);
        if (length == 0 || length >= capacity) return false;
        var finalPath = new ReadOnlySpan<char>(path, (int)length);
        if (!finalPath.StartsWith(volumePrefix, StringComparison.OrdinalIgnoreCase)) return false;
        int endOfGuid = finalPath.IndexOf('}');
        if (endOfGuid < 0 || endOfGuid + 1 >= finalPath.Length || finalPath[endOfGuid + 1] != '\\') return false;
        path[endOfGuid + 2] = '\0';
        return (DriveType)GetDriveType(path) == DriveType.Fixed;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW")]
    private static unsafe partial uint GetFinalPathNameByHandle(SafeFileHandle handle, char* path, uint capacity, uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "GetDriveTypeW")]
    private static unsafe partial uint GetDriveType(char* root);

    internal sealed unsafe class MappedPlane : IDisposable
    {
        private MemoryMappedViewAccessor? _view;
        private readonly byte* _start;

        internal MappedPlane(MemoryMappedViewAccessor view)
        {
            byte* pointer = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            _start = pointer + view.PointerOffset;
            _view = view;
        }

        internal ReadOnlySpan<T> GetSamples<T>(int start, int count) where T : unmanaged
        {
            ObjectDisposedException.ThrowIf(_view == null, this);
            return new ReadOnlySpan<T>((T*)_start + start, count);
        }

        public void Dispose()
        {
            MemoryMappedViewAccessor? view = Interlocked.Exchange(ref _view, null);
            if (view == null) return;
            view.SafeMemoryMappedViewHandle.ReleasePointer();
            view.Dispose();
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
