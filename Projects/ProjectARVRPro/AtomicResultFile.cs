using System.IO;

namespace ProjectARVRPro;

internal static class AtomicResultFile
{
    internal static void Write(string filePath, Action<string> write, CancellationToken cancellationToken = default)
    {
        string destinationPath = Path.GetFullPath(filePath);
        // Stage beside the destination so promotion stays on the same volume.
        string stagingPath = Path.Combine(Path.GetDirectoryName(destinationPath)!, $".{Guid.NewGuid():N}.arvr.tmp");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            write(stagingPath);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(stagingPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(stagingPath))
                File.Delete(stagingPath);
        }
    }
}
