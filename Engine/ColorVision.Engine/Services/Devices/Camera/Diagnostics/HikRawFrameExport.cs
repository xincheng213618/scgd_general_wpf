using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Diagnostics;

internal sealed record HikCaptureRequest(string? RawExportPath = null);

internal static class HikRawFrameExport
{
    // Called on the capture worker while it still owns the SDK frame. Never retains the pointer.
    internal static void Save(string path, IntPtr source, int length, string rawFileName, object metadata, ReadOnlySpan<byte> sdkRgb16, CancellationToken cancellation)
    {
        if (source == IntPtr.Zero || length <= 0) throw new ArgumentException("Empty raw frame.");
        string temporary = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                using (var raw = archive.CreateEntry(rawFileName, CompressionLevel.NoCompression).Open())
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
                var manifest = JObject.FromObject(metadata);
                manifest["SchemaVersion"] = sdkRgb16.IsEmpty ? 1 : 2;
                if (!sdkRgb16.IsEmpty)
                {
                    // The capture worker owns this same-frame SDK destination until Save returns.
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    int bytes = sdkRgb16.Length;
                    while (!sdkRgb16.IsEmpty)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        int count = Math.Min(sdkRgb16.Length, 1024 * 1024);
                        hash.AppendData(sdkRgb16[..count]);
                        sdkRgb16 = sdkRgb16[count..];
                    }
                    manifest["SdkReference"] = JObject.FromObject(new
                    {
                        PixelFormat = "RGB16_Packed", ByteOrder = "little-endian", ByteLength = bytes,
                        Sha256 = Convert.ToHexString(hash.GetHashAndReset())
                    });
                }
                using var writer = new StreamWriter(archive.CreateEntry("metadata.json").Open(), new UTF8Encoding(false));
                writer.Write(manifest.ToString(Formatting.Indented));
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
