using System.Windows.Media;
using System.IO;
using ColorVision.UI.Menus;
using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using ColorVision.Solution.V;

namespace ColorVision.Solution.FolderMeta
{
    public interface IFolderMeta
    {
        public DirectoryInfo DirectoryInfo { get; set; }
        IEnumerable<MenuItemMetadata> GetMenuItems();
        ImageSource? Icon { get; set; }
    }

    public abstract class FolderMetaBase : ViewModelBase, IFolderMeta
    {
        public virtual DirectoryInfo DirectoryInfo { get; set; }
        public virtual ImageSource? Icon { get; set; }

        public virtual IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return new List<MenuItemMetadata>();
        }

    }

}
