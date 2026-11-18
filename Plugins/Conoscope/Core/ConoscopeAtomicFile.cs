using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Conoscope.Core
{
    internal static class ConoscopeAtomicFile
    {
        // The temporary file shares the destination volume. A failed/cancelled write
        // never replaces an existing export, and only our own temporary file is removed.
        public static void Write(string path, Action<StreamWriter> write, CancellationToken cancellationToken = default)
        {
            WriteStream(path, stream =>
            {
                using StreamWriter writer = new(stream, new UTF8Encoding(true), leaveOpen: true);
                write(writer);
            }, cancellationToken);
        }

        public static void WriteStream(string path, Action<Stream> write, CancellationToken cancellationToken = default)
        {
            string destination = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write))
                {
                    write(stream);
                }
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, destination, true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
