using System.Windows.Media;
using System.IO;
using ColorVision.Common.MVVM;

namespace ColorVision.Solution.V.Files
{
    public interface IFileMeta
    {
        int Order { get; }
        string Name { get; set; }

        public FileInfo FileInfo {get;set;}

        string ToolTip { get;}

        ImageSource? Icon { get; set; }

        string Extension { get; }

        void Open();
    }


    public abstract class FileMetaBase : ViewModelBase, IFileMeta
    {
        public virtual int Order { get; } = 1;
        public abstract string Extension { get; }
        public virtual string Name { get; set; }
        public FileInfo FileInfo { get; set; }
        public ImageSource? Icon { get; set; }
        public virtual string ToolTip { get;}
        public virtual void Open()
        {

        }

    }



}
