using ColorVision.Common.NativeMethods;
using System.IO;
using ColorVision.Extension;
using System.Windows.Media;
using System.Windows.Controls;
using ColorVision.Common.MVVM;

namespace ColorVision.Solution.V.Folders
{
    public class BaseFolder : IFolder
    {
        public DirectoryInfo DirectoryInfo { get; set; }

        public ContextMenu ContextMenu { get; set; }

        public RelayCommand OpenExplorer { get; set; }


        public BaseFolder(DirectoryInfo directoryInfo)
        {
            DirectoryInfo  = directoryInfo;
            Name = directoryInfo.Name;
            var icon = FileIcon.GetDirectoryIcon();
            if (icon != null)
            Icon = icon.ToImageSource();

            GeneralRelayCommand();
            GeneralContextMenu();
        }
        public void GeneralRelayCommand()
        {
            OpenExplorer = new RelayCommand(a =>  System.Diagnostics.Process.Start("explorer.exe", DirectoryInfo.FullName));
        }

        public void GeneralContextMenu()
        {
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new() { Header = "打开文件夹", Command = OpenExplorer };
            ContextMenu.Items.Add(menuItem);
        }

        public string Name { get; set; }
        public string ToolTip { get; set; }
        public ImageSource Icon { get; set; }

        public void Copy()
        {
            throw new System.NotImplementedException();
        }

        public void Delete()
        {
            throw new System.NotImplementedException();
        }

        public void Open()
        {
            System.Diagnostics.Process.Start("explorer.exe", DirectoryInfo.FullName);
        }

        public void ReName()
        {
            throw new System.NotImplementedException();
        }
    }
}
