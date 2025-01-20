using ColorVision.Common.MVVM;
using ColorVision.Common.NativeMethods;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            Icon = FileIcon.GetDirectoryIconImageSource();

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
        public ImageSource? Icon { get; set; }

        public void Copy()
        {
            throw new System.NotImplementedException();
        }

        public void Delete()
        {
            try
            {
                DirectoryInfo.Delete(true);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
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
