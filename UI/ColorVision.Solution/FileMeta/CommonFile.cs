using ColorVision.Common.NativeMethods;
using System.IO;

namespace ColorVision.Solution.FileMeta
{
    /// <summary>
    /// Generic file meta that applies to all file types as a fallback.
    /// Updated to use the new attribute-based registration system.
    /// </summary>
    [GenericFileMeta(name: "Generic File")]
    public class CommonFile : FileMetaBase
    {
        public override int Order => 1;
        
        public CommonFile() { }
        
        public CommonFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }
    }
}
