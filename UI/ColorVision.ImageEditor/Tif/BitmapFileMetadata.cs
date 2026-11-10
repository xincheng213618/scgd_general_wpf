using ColorVision.ImageEditor.Abstractions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Tif;

internal interface IPreparedBitmapOpen : IAsyncImageOpen
{
    BitmapFileMetadata ReadFileMetadata(string filePath) => BitmapFileMetadata.Read(filePath);

    Task OpenPreparedImageAsync(EditorContext context, string filePath, BitmapFileMetadata file, CancellationToken cancellationToken);
}

internal readonly record struct BitmapFileMetadata(string Name, long Length, DateTime CreationTime, DateTime LastWriteTime)
{
    public static BitmapFileMetadata Read(string path)
    {
        FileInfo file = new(path);
        if (!file.Exists) throw new FileNotFoundException("Image file does not exist.", path);
        return new(file.Name, file.Length, file.CreationTime, file.LastWriteTime);
    }

    public void Apply(EditorContext context, string path, string owner)
    {
        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSource, path, owner, "打开器接收到的源文件路径");
        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileName, Name, owner, "当前文件名");
        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSize, Length, owner, "当前文件大小（字节）");
        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileCreationTime, CreationTime, owner, "当前文件创建时间");
        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileModifiedTime, LastWriteTime, owner, "当前文件修改时间");
    }
}
