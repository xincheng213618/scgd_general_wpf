using ColorVision.Common.NativeMethods;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution.V.Files;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Impl.CVFlow
{
    public class FlowFile : FileMetaBase
    {
        public override string Extension { get => ".stn"; }
        public FlowFile() { }
        public FlowFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }
    }



}
