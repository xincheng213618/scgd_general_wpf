using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using ColorVision.UI.Extension;
using ColorVision.Solution.V.Files;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Flow
{
    public class FlowFile : ViewModelBase, IFileMeta
    {
        public FileInfo FileInfo { get; set; }
        public ContextMenu ContextMenu { get; set; }

        public string Extension { get => ".stn"; }
        public FlowFile() { }

        public FlowFile(FileInfo fileInfo)
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
            bool IsOpen = false;
            foreach (var item in Application.Current.Windows)
            {
                if (item is WindowFlowEngine WindowFlowEngine)
                {
                    WindowFlowEngine.OpenFlow(FullName);
                    WindowFlowEngine.Activate();
                    IsOpen = true;
                    break;
                }
            }
            if (!IsOpen)
                new WindowFlowEngine() { Owner = null }.Show();
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
