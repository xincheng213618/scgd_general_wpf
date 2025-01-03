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

        public string FileSize { get => _FileSize; set { _FileSize = value; NotifyPropertyChanged(); } }
        private string _FileSize;


        public void Copy()
        {
            throw new System.NotImplementedException();
        }

        public void Delete()
        {
            try
            {
                File.Delete(FileInfo.FullName);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Open()
        {
            PlatformHelper.Open(FileInfo.FullName);
        }

        public void ReName()
        {
            throw new System.NotImplementedException();
        }
    }

}
