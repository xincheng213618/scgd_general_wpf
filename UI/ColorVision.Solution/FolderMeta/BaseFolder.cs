using ColorVision.Common.NativeMethods;
using System.IO;

namespace ColorVision.Solution.FolderMeta
{
    /// <summary>
    /// Generic folder meta that applies to all directories as a fallback.
    /// Uses the new attribute-based registration system.
    /// </summary>
    [GenericFolderMeta(name: "Generic Folder")]
    public class BaseFolder : FolderMetaBase
    {
        public BaseFolder()
        {
        }
        
        public BaseFolder(DirectoryInfo directoryInfo)
        {
            DirectoryInfo  = directoryInfo;
            Icon = FileIcon.GetDirectoryIconImageSource();
        }

    }
}
