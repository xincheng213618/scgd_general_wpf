using ColorVision.Common.NativeMethods;
using ColorVision.Solution.FileMeta;
using System.ComponentModel;
using System.IO;

namespace ColorVision.Engine.Templates.Flow
{
    [FileExtension(".stn", ".cvflow")]
    public class FlowFile : FileMetaBase
    {
        public FlowFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;  
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }
    }



}
