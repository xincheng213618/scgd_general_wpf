using ColorVision.Common.NativeMethods;
using System.ComponentModel;
using System.IO;

namespace ColorVision.Solution.FileMeta
{
    [GenericFile]
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
