using ColorVision.Common.MVVM;
using ColorVision.UI.Extension;
using ColorVision.Common.NativeMethods;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System;
using System.Windows;

namespace ColorVision.Solution.V.Files
{
    public class CommonFile : ViewModelBase, IFile
    {
        public CommonFile() { }
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public string Extension { get => ".*"; }
        public CommonFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            FullName = FileInfo.FullName;
            var icon = FileIcon.GetFileIcon(fileInfo.FullName);
            if (icon != null)
                Icon = icon.ToImageSource();
        }

        public string Name { get; set; }
        public string FullName { get; set; }
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
                File.Delete(FullName);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Open()
        {
            Common.Utilities.PlatformHelper.Open(FullName);
        }

        public void ReName()
        {
            throw new System.NotImplementedException();
        }
    }

}
