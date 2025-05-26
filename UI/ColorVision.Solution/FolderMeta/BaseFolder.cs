using ColorVision.Common.NativeMethods;
using System.IO;

namespace ColorVision.Solution.FolderMeta
{
    public class BaseFolder : FolderMetaBase
    {
        public BaseFolder(DirectoryInfo directoryInfo)
        {
            DirectoryInfo  = directoryInfo;
            Name = directoryInfo.Name;
            Icon = FileIcon.GetDirectoryIconImageSource();
        }

    }
}
