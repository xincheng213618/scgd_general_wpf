using ColorVision.Common.NativeMethods;
using ColorVision.Extension;
using System.IO;
using System.Windows.Media;
using ColorVision.Common.MVVM;
using System.Windows.Controls;
using ColorVision.Media;
using ColorVision.Net;
using System.Windows;
using ColorVision.Common.Utilities;
using ColorVision.Common.Extension;
using ColorVision.Solution.V.Files;

namespace ColorVision.Services
{
    public class CVcieFile : ViewModelBase, IFile
    {
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }

        public string Extension { get => ".cvraw|.cvcie"; }

        public CVcieFile()
        {

        }

        public CVcieFile(FileInfo fileInfo)
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

        public string FileSize { get => FileInfo.Length.ToString(); set { NotifyPropertyChanged(); } }

        public void Open()
        {
            if (File.Exists(FileInfo.FullName))
            {
                ImageView imageView = new();

                CVFileUtil.ReadCVRaw(FileInfo.FullName, out CVCIEFile fileInfo);
                Window window = new() { Title = Properties.Resource.QuickPreview, Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                window.Content = imageView;
                imageView.OpenImage(new NetFileUtil().OpenLocalCVFile(FileInfo.FullName));

                window.Show();
                window.DelayClearImage(() => Application.Current.Dispatcher.Invoke(() =>
                {
                    imageView.ToolBarTop.ClearImage();
                }));
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到文件", "ColorVision");
            }

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
