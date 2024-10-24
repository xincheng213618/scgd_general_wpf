using System.Windows.Media;
using System.IO;

namespace ColorVision.Solution.V.Files
{
    public interface IFileMeta
    {
        string Name { get; set; }

        string FullName { get; set; }

        public FileInfo FileInfo {get;set;}

        string ToolTip { get; set; }

        ImageSource Icon { get; set; }

        string FileSize { get; set; }

        string Extension { get; }

        void Open();
    }



}
