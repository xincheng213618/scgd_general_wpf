using ColorVision.NativeMethods;
using System.IO;
using ColorVision.Extension;
using System.Windows.Media;
using System.Windows.Controls;
using ColorVision.MVVM;
using System.Drawing;
using ColorVision.Themes;
using System.Windows;
using ColorVision.Services.Algorithm;

namespace ColorVision.Solution.V.Folders
{
    public interface IFolder
    {
        string Name { get; set; }
        string ToolTip { get; set; }
        ImageSource Icon { get; set; }

        ContextMenu ContextMenu { get; set; }

        void Open();
        void Copy();
        void ReName();
        void Delete();
    }

    public class HistoryFolder : IFolder
    {
        public string Name { get; set; }
        public string ToolTip { get; set; }
        public ContextMenu ContextMenu { get; set; }
        public ImageSource Icon { get; set; }

        public HistoryFolder(string Name)
        {
            this.Name = Name;
            if (Application.Current.TryFindResource("HistoryDrawingImage") is DrawingImage drawingImage)
                Icon = drawingImage;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource("HistoryDrawingImage") is DrawingImage drawingImage)
                    Icon = drawingImage;
            };
        }



        public void Open()
        {
            WindowSolution windowSolution = new WindowSolution();
            windowSolution.Show();
        }

        public void Copy()
        {
            throw new System.NotImplementedException();
        }

        public void ReName()
        {
            throw new System.NotImplementedException();
        }

        public void Delete()
        {
            throw new System.NotImplementedException();
        }
    }


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
            OpenExplorer = new RelayCommand(a =>
            System.Diagnostics.Process.Start("explorer.exe", DirectoryInfo.FullName));
        }

        public void GeneralContextMenu()
        {
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "打开文件夹", Command = OpenExplorer };
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
