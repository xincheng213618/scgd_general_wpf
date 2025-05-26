using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using ColorVision.Solution.FileMeta;
using System.IO;

namespace ColorVision.Solution.V.Files
{
    public class CommonFile : FileMetaBase
    {
        public override string Extension { get => ".*"; }
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
