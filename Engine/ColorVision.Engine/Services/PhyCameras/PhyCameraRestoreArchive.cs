using System;
using System.IO;
using System.IO.Compression;

namespace ColorVision.Engine.Services.PhyCameras
{
    internal static class PhyCameraRestoreArchive
    {
        public static void CreateOrReplace(string sourceDirectory, string destinationPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

            string fullDestinationPath = Path.GetFullPath(destinationPath);
            string destinationDirectory = Path.GetDirectoryName(fullDestinationPath)
                ?? throw new InvalidOperationException("无法确定恢复点目录。");
            string temporaryArchivePath = Path.Combine(
                destinationDirectory,
                $".{Path.GetFileName(fullDestinationPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                ZipFile.CreateFromDirectory(sourceDirectory, temporaryArchivePath, CompressionLevel.NoCompression, false);
                File.Move(temporaryArchivePath, fullDestinationPath, true);
            }
            finally
            {
                if (File.Exists(temporaryArchivePath))
                {
                    File.Delete(temporaryArchivePath);
                }
            }
        }
    }
}
