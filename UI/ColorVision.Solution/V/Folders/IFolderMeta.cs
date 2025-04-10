using System.Windows.Media;
using System.IO;
using ColorVision.UI.Menus;
using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ColorVision.Solution.V.Folders
{
    public interface IFolderMeta
    {
        string Name { get; set; }
        public DirectoryInfo DirectoryInfo { get; set; }
        IEnumerable<MenuItemMetadata> GetMenuItems();
        public ObservableCollection<VObject> VisualChildren { get; set; }
        string ToolTip { get; set; }
        ImageSource? Icon { get; set; }
        void Open();
        void GenChild();
    }

    public abstract class FolderMetaBase : ViewModelBase, IFolderMeta, IObject
    {
        public virtual string Name { get; set; }
        public virtual DirectoryInfo DirectoryInfo { get; set; }
        public virtual ImageSource? Icon { get; set; }
        public virtual string ToolTip { get; set; }
        public ObservableCollection<VObject> VisualChildren { get; set; } = new ObservableCollection<VObject>();
        public VObject Parent { get; set; }

        public virtual void Open()
        {
        }

        public virtual void GenChild()
        {
        }

        public virtual IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            return new List<MenuItemMetadata>();
        }

        public void AddChild(VObject vObject)
        {
            if (vObject == null) return;
            VisualChildren.Add(vObject);
        }

        public void RemoveChild(VObject vObject)
        {
            if (vObject == null) return;
            VisualChildren.Remove(vObject);
        }
    }

}
