using System.Windows.Media;
using System.Windows.Controls;

namespace ColorVision.Solution.V.Files
{
    public interface IFile
    {
        string Name { get; set; }

        string FullName { get; set; }

        string ToolTip { get; set; }
        ImageSource Icon { get; set; }

        string FileSize { get; set; }
        ContextMenu ContextMenu { get; set; }


        void Open();
        void Copy();
        void ReName();
        void Delete();
    }



}
