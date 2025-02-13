using System.Windows.Media;
using System.Windows.Controls;
using System.IO;

namespace ColorVision.Solution.V.Folders
{
    public interface IFolder
    {
        string Name { get; set; }
        public DirectoryInfo DirectoryInfo { get; set; }
        string ToolTip { get; set; }
        ImageSource? Icon { get; set; }
        void Open();
        void GenChild();
    }
}
