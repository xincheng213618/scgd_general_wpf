using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Solution.V.Folders
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
