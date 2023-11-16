using ColorVision.NativeMethods;
using System.IO;
using ColorVision.Extension;
using System.Windows.Media;
using System.Windows.Controls;

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

    public class BaseFolder : IFolder
    {
        public DirectoryInfo DirectoryInfo { get; set; }

        public ContextMenu ContextMenu { get; set; }

        public BaseFolder(DirectoryInfo directoryInfo)
        {
            DirectoryInfo  = directoryInfo;
            Name = directoryInfo.Name;
            var icon = FileIcon.GetDirectoryIcon();
            if (icon != null)
            Icon = icon.ToImageSource();
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
            throw new System.NotImplementedException();
        }

        public void ReName()
        {
            throw new System.NotImplementedException();
        }
    }
}
