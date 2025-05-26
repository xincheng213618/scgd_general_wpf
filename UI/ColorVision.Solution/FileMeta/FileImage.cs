using ColorVision.Common.NativeMethods;
using ColorVision.UI.Menus;
using System.IO;

namespace ColorVision.Solution.FileMeta
{
    [FileExtension(".jpg|.png|.jpeg|.tif|.bmp|.tiff")]
    public class FileImage : FileMetaBase
    {
        public FileImage() { }

        public FileImage(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }

        public override IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return base.GetMenuItems();
        }

    }

}
