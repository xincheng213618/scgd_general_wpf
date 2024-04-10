using ColorVision.NativeMethods;
using ColorVision.Extension;

using System.IO;
using System.Windows.Media;
using ColorVision.Common.MVVM;
using System.Windows.Controls;

namespace ColorVision.Solution.V.Files
{
    public class CVcieFile:ViewModelBase, IFile
    {
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }

        public string Extension { get => FileInfo.Extension; }

        public CVcieFile(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            Name = FileInfo.Name;
            FullName = FileInfo.FullName;
            var icon = FileIcon.GetFileIcon(fileInfo.FullName);
            if (icon != null)
                Icon = icon.ToImageSource();

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resource.Open, Command = new RelayCommand((e) => Open()) });
        }

        public string Name { get; set; }
        public string FullName { get; set; }
        public string ToolTip { get; set; }
        public ImageSource Icon { get; set; }

        public string FileSize { get => FileInfo.Length.ToString(); set { NotifyPropertyChanged(); } }

        public void Open()
        {
            //ImageView imageView = new ImageView();
            //Window window = new Window() { };
            //window.Content = imageView;
            ////Task.Run(async () => {
            ////    await Task.Delay(10);
            ////    Application.Current.Dispatcher.Invoke(() => { imageView.OpenCVCIE(FileInfo.FullPath); });
            ////});
            //window.IsShow();

        }


        public void Copy()
        {
            throw new System.NotImplementedException();
        }

        public void Delete()
        {
            throw new System.NotImplementedException();
        }



        public void ReName()
        {
            throw new System.NotImplementedException();
        }

    }



}
