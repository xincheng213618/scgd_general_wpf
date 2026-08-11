using System;
using System.IO;

namespace ColorVision.Engine.Services.PhyCameras
{
    internal sealed class CalibrationUploadWorkspace : IDisposable
    {
        private bool _disposed;

        private CalibrationUploadWorkspace(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }

        public string DirectoryPath { get; }

        public static CalibrationUploadWorkspace Create(string? cacheRoot = null)
        {
            string root = cacheRoot ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ColorVision",
                "Cache");
            string directoryPath = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directoryPath);
            return new CalibrationUploadWorkspace(directoryPath);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                if (Directory.Exists(DirectoryPath))
                {
                    Directory.Delete(DirectoryPath, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
