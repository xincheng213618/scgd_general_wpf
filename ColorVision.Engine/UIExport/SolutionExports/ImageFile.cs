using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Extension;
using ColorVision.Media;
using ColorVision.Solution.V.Files;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.UIExport.SolutionExports
{
    public class ImageFile : ViewModelBase, IFile
    {
        public ImageFile() { }
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public RelayCommand AttributesCommand { get; set; }
        public string Extension { get => ".jpg|.png|.jpeg|.tif|.bmp"; }
        public ImageFile(FileInfo fileInfo)
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
            ImageView imageView = new();
            Window window = new() { };
            window.Content = imageView;
            _ = RunAsync(imageView);
            window.Show();
        }
        public async Task<Task> RunAsync(ImageView imageView)
        {
            await Task.Delay(100);
            imageView.OpenImage(FileInfo.FullName);
            return Task.CompletedTask;
        }


        public void ReName()
        {
            throw new NotImplementedException();
        }
    }

}
