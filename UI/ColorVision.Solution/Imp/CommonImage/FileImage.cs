using ColorVision.Common.NativeMethods;
using ColorVision.Solution.V.Files;
using ColorVision.UI.Menus;
using System.IO;

namespace ColorVision.Solution.Imp.CommonImage
{
    
    
    public class FileImage : FileMetaBase
    {
        public override string Extension { get => ".jpg|.png|.jpeg|.tif|.bmp|.tiff|"; }
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
