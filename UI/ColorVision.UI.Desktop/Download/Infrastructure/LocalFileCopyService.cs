using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace ColorVision.UI.Desktop.Download
{
    internal static class LocalFileCopyService
    {
        private const int BufferSize = 1024 * 1024;
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(150);

        public static async Task<LocalFileCopyResult> CopyAsync(string sourcePath, string destinationPath, long expectedBytes,
            Action<LocalFileCopyProgress> reportProgress, CancellationToken cancellationToken, string? expectedSha256 = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string targetDirectory = Path.GetDirectoryName(Path.GetFullPath(destinationPath))!;
            Directory.CreateDirectory(targetDirectory);
            string temporaryPath = Path.Combine(targetDirectory, $".cvdownload-{Guid.NewGuid():N}.tmp");
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                long copiedBytes = 0;
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
                using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    if (expectedBytes <= 0) expectedBytes = source.Length;
                    long intervalBytes = 0;
                    var stopwatch = Stopwatch.StartNew();
                    while (true)
                    {
                        int bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false);
                        if (bytesRead == 0) break;
                        await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                        hash.AppendData(buffer, 0, bytesRead);
                        copiedBytes += bytesRead;
                        intervalBytes += bytesRead;
                        if (stopwatch.Elapsed >= ProgressInterval || copiedBytes >= expectedBytes)
                        {
                            long speed = stopwatch.ElapsedMilliseconds > 0 ? intervalBytes * 1000 / stopwatch.ElapsedMilliseconds : 0;
                            int progress = expectedBytes > 0 ? (int)Math.Min(100, copiedBytes * 100 / expectedBytes) : 0;
                            reportProgress(new LocalFileCopyProgress(expectedBytes, copiedBytes, progress, speed));
                            intervalBytes = 0;
                            stopwatch.Restart();
                        }
                    }
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                if (copiedBytes <= 0 || copiedBytes != expectedBytes)
                    throw new InvalidDataException($"Local copy size mismatch: {copiedBytes}/{expectedBytes} bytes.");
                string actualHash = Convert.ToHexString(hash.GetHashAndReset());
                if (expectedSha256 != null && !actualHash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Local copy SHA-256 verification failed.");
                cancellationToken.ThrowIfCancellationRequested();
                // Only our unique temporary file is disposable. Never truncate or delete an existing destination.
                File.Move(temporaryPath, destinationPath, overwrite: false);
                return new LocalFileCopyResult(copiedBytes, copiedBytes, actualHash);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    internal readonly record struct LocalFileCopyProgress(long TotalBytes, long CopiedBytes, int Progress, long BytesPerSecond);
    internal readonly record struct LocalFileCopyResult(long TotalBytes, long CompletedBytes, string ContentSha256);
}
