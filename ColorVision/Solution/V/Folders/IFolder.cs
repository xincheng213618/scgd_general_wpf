using System.Windows.Media;
using System.Windows.Controls;
using System.Drawing;

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
}
