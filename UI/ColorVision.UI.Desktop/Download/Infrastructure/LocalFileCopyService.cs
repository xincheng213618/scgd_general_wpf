using System.Buffers;
using System.Diagnostics;
using System.IO;

namespace ColorVision.UI.Desktop.Download
{
    internal static class LocalFileCopyService
    {
        private const int BufferSize = 1024 * 1024;
        private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(150);

        public static async Task<LocalFileCopyResult> CopyAsync(
            string sourcePath,
            string destinationPath,
            long expectedBytes,
            Action<LocalFileCopyProgress> reportProgress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("Local reuse source file not found.", sourcePath);

            string? targetDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (expectedBytes <= 0)
                    expectedBytes = sourceStream.Length;

                using var destinationStream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                long copiedBytes = 0;
                long intervalBytes = 0;
                var intervalStopwatch = Stopwatch.StartNew();
                var progressStopwatch = Stopwatch.StartNew();

                while (true)
                {
                    int bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false);
                    if (bytesRead <= 0)
                        break;

                    await destinationStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);

                    copiedBytes += bytesRead;
                    intervalBytes += bytesRead;

                    if (progressStopwatch.Elapsed >= ProgressInterval || (expectedBytes > 0 && copiedBytes >= expectedBytes))
                    {
                        long speed = intervalStopwatch.ElapsedMilliseconds > 0
                            ? intervalBytes * 1000 / intervalStopwatch.ElapsedMilliseconds
                            : 0;

                        long currentTotalBytes = expectedBytes > 0 ? expectedBytes : copiedBytes;
                        int progress = currentTotalBytes > 0 ? (int)Math.Min(100, copiedBytes * 100 / currentTotalBytes) : 0;
                        reportProgress(new LocalFileCopyProgress(currentTotalBytes, copiedBytes, progress, speed));

                        intervalBytes = 0;
                        intervalStopwatch.Restart();
                        progressStopwatch.Restart();
                    }
                }

                await destinationStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            long completedBytes = new FileInfo(destinationPath).Length;
            long totalBytes = expectedBytes > 0 ? expectedBytes : completedBytes;

            if (completedBytes <= 0)
                throw new InvalidDataException("Downloaded file is empty.");

            if (totalBytes > 0 && completedBytes < totalBytes)
                throw new InvalidDataException($"Downloaded file is incomplete: {completedBytes}/{totalBytes} bytes.");

            return new LocalFileCopyResult(Math.Max(totalBytes, completedBytes), completedBytes);
        }
    }

    internal readonly record struct LocalFileCopyProgress(long TotalBytes, long CopiedBytes, int Progress, long BytesPerSecond);

    internal readonly record struct LocalFileCopyResult(long TotalBytes, long CompletedBytes);
}
