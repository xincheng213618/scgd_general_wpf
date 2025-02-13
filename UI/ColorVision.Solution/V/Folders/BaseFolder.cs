using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Solution.V.Folders
{
    public class BaseFolder : IFolder
    {
        public DirectoryInfo DirectoryInfo { get; set; }


        public RelayCommand OpenExplorer { get; set; }


        public BaseFolder(DirectoryInfo directoryInfo)
        {
            DirectoryInfo  = directoryInfo;
            Name = directoryInfo.Name;
            Icon = FileIcon.GetDirectoryIconImageSource();
        }
        public string Name { get; set; }
        public string ToolTip { get; set; }
        public ImageSource? Icon { get; set; }


        public void Delete()
        {
        }

        public void Open()
        {
        }
        public void GenChild()
        {

        }

    }
}
