using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Abstractions;

/// <summary>Opt-in for file openers whose source availability and metadata can be read on a worker.</summary>
public interface IPreparedImageOpen : IAsyncImageOpen
{
    /// <summary>Called on a worker. A cached length identifies an available source without a disk lookup.</summary>
    ImageFileMetadata ReadFileMetadata(string filePath) => GetCachedFileLength(filePath) is long length
        ? new(Path.GetFileName(filePath), length, null, null)
        : ImageFileMetadata.Read(filePath);

    /// <summary>Called on the editor dispatcher after source validation.</summary>
    Task OpenPreparedImageAsync(EditorContext context, string filePath, ImageFileMetadata file, CancellationToken cancellationToken)
        => OpenImageAsync(context, filePath, cancellationToken);
}

public readonly record struct ImageFileMetadata(string Name, long Length, DateTime? CreationTime, DateTime? LastWriteTime)
{
    internal static ImageFileMetadata Read(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists) throw new FileNotFoundException("Image file does not exist.", path);
        return new(file.Name, file.Length, file.CreationTime, file.LastWriteTime);
    }

    internal void Apply(EditorContext context, string path, string owner)
    {
        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSource, path, owner, "打开器接收到的源文件路径");
        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileName, Name, owner, "当前文件名");
        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSize, Length, owner, "当前文件大小（字节）");
        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileCreationTime, CreationTime, owner, "当前文件创建时间");
        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileModifiedTime, LastWriteTime, owner, "当前文件修改时间");
    }
}
