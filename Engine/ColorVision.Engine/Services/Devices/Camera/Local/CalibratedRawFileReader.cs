using ColorVision.Core;
using ColorVision.FileIO;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

/// <summary>
/// Replays the saved color calibration one float channel at a time, without copying
/// the RAW payload. The read lease keeps pixels and calibration consistent until disposed.
/// </summary>
public sealed class CalibratedRawFileReader : IDisposable
{
    private readonly FileStream stream;
    private readonly MemoryMappedFile mapping;
    private readonly MemoryMappedViewAccessor view;
    private readonly ColorCalibrationSnapshot snapshot;
    private readonly RawColorTransformV1 transform;
    private readonly long payloadStart;
    private readonly long payloadBytes;
    private readonly object sync = new();
    private bool disposed;

    public int Width => snapshot.Width;
    public int Height => snapshot.Height;
    public int Channels => snapshot.Channels;

    public CalibratedRawFileReader(string filePath)
    {
        stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            int headerEnd = CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile header);
            using (header)
            {
                if (headerEnd <= 0 || !string.Equals(Path.GetExtension(filePath), ".cvraw", StringComparison.OrdinalIgnoreCase) || header.Version is < 1 or > 3
                    || header.Rows <= 0 || header.Cols <= 0 || header.Channels is not (1 or 3) || header.Bpp is not (8 or 16))
                    throw new InvalidDataException("CVRAW 数据头无效或不受支持。");
                // The legacy header reader infers type using a case-sensitive substring.
                header.FileExtType = CVType.Raw;
                snapshot = ColorCalibrationSnapshot.Read(filePath, header)
                    ?? throw new InvalidDataException("CVRAW 没有色度校正参数。");
                if (!snapshot.CanReplay) throw new InvalidDataException("CVRAW 的色度校正参数不适用于当前像素数据，无法重放。");
                if (checked((long)Width * Height * sizeof(float)) > Array.MaxLength)
                    throw new InvalidDataException("CVRAW 校正通道过大。");
                transform = snapshot.ToNative();
                stream.Position = headerEnd;
                using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
                long declaredBytes = header.Version == 2 ? reader.ReadInt64() : reader.ReadInt32();
                payloadStart = stream.Position;
                payloadBytes = checked((long)Width * Height * Channels * (snapshot.RawBpp / 8));
                if (declaredBytes < payloadBytes || declaredBytes > stream.Length - payloadStart)
                    throw new InvalidDataException("CVRAW 像素数据不完整。");
            }
            mapping = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
            try { view = mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read); }
            catch { mapping.Dispose(); throw; }
        }
        catch { stream.Dispose(); throw; }
    }

    /// <summary>Reads X/Y/Z by index 0/1/2; a monochrome calibration exposes only Y at index 0.</summary>
    public unsafe CVCIEFile ReadChannel(int channel, CancellationToken cancellationToken = default)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if ((uint)channel >= (uint)Channels) throw new ArgumentOutOfRangeException(nameof(channel));
            cancellationToken.ThrowIfCancellationRequested();
            byte[] data = GC.AllocateUninitializedArray<byte>(checked(Width * Height * sizeof(float)));
            byte* raw = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref raw);
            try
            {
                fixed (byte* output = data)
                {
                    int status = OpenCVMediaHelper.M_TransformRawColorV1(Width, Height, snapshot.RawBpp,
                        (IntPtr)(raw + view.PointerOffset + payloadStart), (ulong)payloadBytes,
                        in transform, channel, (IntPtr)output, (ulong)Width * (ulong)Height);
                    if (status != OpenCVCalibration.PoiOk) throw new InvalidOperationException($"RAW 色度计算失败：{status}。");
                }
            }
            finally { view.SafeMemoryMappedViewHandle.ReleasePointer(); }
            cancellationToken.ThrowIfCancellationRequested();
            return new CVCIEFile
            {
                Version = 2, FileExtType = CVType.CIE, FilePath = stream.Name,
                Rows = Height, Cols = Width, Bpp = 32, Channels = Channels,
                Exp = (float[])snapshot.Exposure.Clone(), Data = data,
            };
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            view.Dispose();
            mapping.Dispose();
            stream.Dispose();
        }
    }
}
