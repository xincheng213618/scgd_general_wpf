using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Solution;
using ColorVision.Solution.V.Files;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Impl.CommonImage
{
    public class FileImage : ViewModelBase, IFileMeta
    {
        public FileImage() { }
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public RelayCommand AttributesCommand { get; set; }
        public string Extension { get => ".jpg|.png|.jpeg|.tif|.bmp"; }
        public FileImage(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            FullName = FileInfo.FullName;
            Icon = FileIcon.GetFileIconImageSource(fileInfo.FullName);

        }

        public string Name { get; set; }
        public string FullName { get; set; }
        public string ToolTip { get; set; }
        public ImageSource Icon { get; set; }

        public string FileSize { get => _FileSize; set { _FileSize = value; NotifyPropertyChanged(); } }
        private string _FileSize;


        public void Copy()
        {
            throw new NotImplementedException();
        }

        public void Delete()
        {
            try
            {
                File.Delete(FullName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public void Open()
        {
            SolutionProcessImage fileControl = new SolutionProcessImage() { Name = Name, FullName = FileInfo.FullName, IconSource = Icon };
            SolutionManager.GetInstance().OpenFileWindow(fileControl);
        }


        public void ReName()
        {
            throw new NotImplementedException();
        }
    }

}
