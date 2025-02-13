using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Solution.V.Files
{
    public class CommonFile : ViewModelBase, IFileMeta
    {
        public CommonFile() { }
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public string Extension { get => ".*"; }
        public CommonFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);
        }

        public string Name { get; set; }
        public string ToolTip { get; set; }
        public ImageSource Icon { get; set; }

        public int Order { get; set; } = 1;

        public string FileSize { get => _FileSize; set { _FileSize = value; NotifyPropertyChanged(); } }
        private string _FileSize;

        public void Open()
        {
            PlatformHelper.Open(FileInfo.FullName);
        }

    }

}
