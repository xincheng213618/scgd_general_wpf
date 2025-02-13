using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        public override void Open()
        {
            PlatformHelper.Open(FileInfo.FullName);

        }

    }

}
