using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Diagnostics;

internal sealed record HikCaptureRequest(string? RawExportPath = null);

internal static class HikRawFrameExport
{
    // Called on the capture worker while it still owns the SDK frame. Never retains the pointer.
    internal static void Save(string path, IntPtr source, int length, object metadata, CancellationToken cancellation)
    {
        if (source == IntPtr.Zero || length <= 0) throw new ArgumentException("Empty Bayer frame.");
        string temporary = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                using (var raw = archive.CreateEntry("frame.bayer16.raw", CompressionLevel.NoCompression).Open())
                {
                    byte[] buffer = new byte[Math.Min(length, 1024 * 1024)];
                    for (int offset = 0; offset < length;)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        int count = Math.Min(buffer.Length, length - offset);
                        Marshal.Copy(IntPtr.Add(source, offset), buffer, 0, count);
                        raw.Write(buffer, 0, count);
                        offset += count;
                    }
                }
                using var writer = new StreamWriter(archive.CreateEntry("metadata.json").Open(), new UTF8Encoding(false));
                writer.Write(JsonConvert.SerializeObject(metadata, Formatting.Indented));
            }
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
