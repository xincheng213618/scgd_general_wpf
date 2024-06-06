using System.Windows.Media;
using System.Windows.Controls;
using System.IO;

namespace ColorVision.Solution.V.Files
{
    public interface IFile
    {
        string Name { get; set; }

        string FullName { get; set; }

        public FileInfo FileInfo {get;set;}

        string ToolTip { get; set; }
        ImageSource Icon { get; set; }

        string FileSize { get; set; }
        ContextMenu ContextMenu { get; set; }

        string Extension { get; }

        void Open();
        void Copy();
        void ReName();
        void Delete();
    }



}
