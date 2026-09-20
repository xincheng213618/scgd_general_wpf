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
            string destination = Path.GetFullPath(path);
            string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (StreamWriter writer = new(temporary, false, new UTF8Encoding(true)))
                {
                    write(writer);
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
