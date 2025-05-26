using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using System.ComponentModel;
using System.IO;

namespace ColorVision.Solution.FileMeta
{
    [FileExtension(".txt", ".cs")]
    public class FileText : FileMetaBase
    {
        public override int Order => 1;
        public FileText() { }
        public RelayCommand AttributesCommand { get; set; }
        public FileText(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }
    }

}
