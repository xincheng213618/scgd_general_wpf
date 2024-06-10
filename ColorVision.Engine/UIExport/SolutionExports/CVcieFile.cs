using ColorVision.Common.Extension;
using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Properties;
using ColorVision.UI.Extension;
using ColorVision.Engine.Media;
using ColorVision.Net;
using ColorVision.Solution.V.Files;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColorVision.Solution;

namespace ColorVision.Engine.UIExport.SolutionExports
{

    public class CVCIEFileOpen : IFileControl
    {
        public ImageView ImageView { get; set; } = new ImageView();

        public string Name { get; set; }

        public Control UserControl => ImageView;

        public void Close()
        {
            ImageView.ToolBarTop.ClearImage();
            ImageView = null;
        }

        public void Open()
        {
            
        }
    }

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
                CVCIEFileOpen fileControl = new CVCIEFileOpen() { Name = Name };

                CVFileUtil.ReadCVRaw(FileInfo.FullName, out CVCIEFile fileInfo);

                fileControl.ImageView.OpenImage(new NetFileUtil().OpenLocalCVFile(FileInfo.FullName));
                SolutionManager.GetInstance().OpenFileWindow(fileControl);
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
