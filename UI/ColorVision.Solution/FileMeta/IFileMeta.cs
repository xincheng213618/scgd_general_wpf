using System.Windows.Media;
using System.IO;
using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;


namespace ColorVision.Solution.FileMeta
{
    public interface IFileMeta
    {
        IEnumerable<MenuItemMetadata> GetMenuItems();
        int Order { get; }
        string Name { get; set; }

        public FileInfo FileInfo {get;set;}

        string ToolTip { get;}

        ImageSource? Icon { get; set; }
    }


    public abstract class FileMetaBase : ViewModelBase, IFileMeta
    {
        public virtual int Order { get; } = 1;
        public virtual string Name { get; set; }
        public FileInfo FileInfo { get; set; }
        public ImageSource? Icon { get; set; }
        public virtual string ToolTip { get;}

        public virtual IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return new List<MenuItemMetadata>();
        }

    }



}
