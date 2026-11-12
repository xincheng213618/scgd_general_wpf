using System;
using System.IO;

namespace ColorVision.Engine.Media;

internal readonly record struct CvcieFileRevision(long Length, DateTime LastWriteTimeUtc, DateTime CreationTimeUtc)
{
    public static CvcieFileRevision Capture(string path)
    {
        FileInfo file = new(path);
        return new(file.Length, file.LastWriteTimeUtc, file.CreationTimeUtc);
    }

    public FileStream OpenRead(string path)
    {
        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            // Check the opened handle so replacement cannot race a separate path lookup.
            var current = new CvcieFileRevision(stream.Length, File.GetLastWriteTimeUtc(stream.SafeFileHandle),
                File.GetCreationTimeUtc(stream.SafeFileHandle));
            if (current != this) throw new CvcieFileChangedException(path);
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }
}

internal sealed class CvcieFileChangedException(string path) : IOException($"CVCIE 文件已改变，请重新打开图像：{path}");
