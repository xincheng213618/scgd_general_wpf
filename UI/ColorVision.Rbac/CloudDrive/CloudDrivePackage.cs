using System.IO;
using System.IO.Compression;
using System.Diagnostics;

namespace ColorVision.Rbac.CloudDrive;

public static class CloudDrivePackage
{
    // Never follow junctions or symbolic links outside the explicitly selected directory.
    public static async Task CreateAsync(string source, string destination, long maxBytes, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        var root = new DirectoryInfo(source);
        if (!root.Exists) throw new DirectoryNotFoundException("原文件夹不存在，请重新选择。");
        if ((root.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("请直接选择原文件夹，不能打包目录链接。");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string partial = destination + ".partial";
        try
        {
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 65536, true))
            {
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    long copied = 0;
                    var progressClock = Stopwatch.StartNew();
                    var buffer = new byte[65536];
                    var directories = new Stack<DirectoryInfo>();
                    directories.Push(root);
                    while (directories.TryPop(out var directory))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string relativeDirectory = Path.GetRelativePath(root.FullName, directory.FullName);
                        if (relativeDirectory != ".") archive.CreateEntry(relativeDirectory.Replace('\\', '/') + "/");
                        foreach (var child in directory.EnumerateFileSystemInfos())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if ((child.Attributes & FileAttributes.ReparsePoint) != 0)
                                throw new IOException($"文件夹包含链接：{child.Name}。请移除链接或单独选择原文件。");
                            if (child is DirectoryInfo nested) { directories.Push(nested); continue; }
                            var file = (FileInfo)child;
                            var entry = archive.CreateEntry(Path.GetRelativePath(root.FullName, file.FullName).Replace('\\', '/'), CompressionLevel.Fastest);
                            var modified = file.LastWriteTime;
                            if (modified.Year >= 1980 && modified.Year <= 2107) entry.LastWriteTime = modified;
                            await using var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, true);
                            await using var target = entry.Open();
                            int count;
                            while ((count = await input.ReadAsync(buffer, cancellationToken)) > 0)
                            {
                                await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                                copied += count;
                                if (progressClock.ElapsedMilliseconds >= 100)
                                {
                                    progress?.Report(copied);
                                    progressClock.Restart();
                                }
                                if (output.Length > maxBytes) throw new IOException($"压缩包超过服务器限制 {CloudDriveItem.FormatSize(maxBytes)}，请分批选择文件。");
                            }
                        }
                    }
                }
                if (output.Length > maxBytes) throw new IOException($"压缩包超过服务器限制 {CloudDriveItem.FormatSize(maxBytes)}。");
            }
            File.Move(partial, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }
}
