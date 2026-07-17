using ColorVision.Common.NativeMethods;
using System.IO;

namespace ColorVision.Solution.FileMeta
{
    /// <summary>
    /// File meta for image file types.
    /// Updated to use the new FileMetaForExtensionAttribute registration system.
    /// </summary>
    [FileMetaForExtension(".jpg|.png|.jpeg|.tif|.bmp|.tiff", name: "Image File", isDefault: true)]
    public class FileImage : FileMetaBase
    {
        public FileImage() { }

        public FileImage(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }
    }
}
