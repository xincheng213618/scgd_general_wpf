using ColorVision.Common.NativeMethods;
using System.IO;

namespace ColorVision.Solution.FolderMeta
{
    public class BaseFolder : FolderMetaBase
    {
        public BaseFolder(DirectoryInfo directoryInfo)
        {
            DirectoryInfo  = directoryInfo;
            Icon = FileIcon.GetDirectoryIconImageSource();
        }

    }
}
