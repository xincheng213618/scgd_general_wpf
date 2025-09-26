using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;
using System.IO;
using System.Windows.Media;

namespace ColorVision.Solution.FolderMeta
{
    /// <summary>
    /// Attribute to register folder meta for specific directory types or patterns
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class FolderMetaForPatternAttribute : Attribute
    {
        public string? Name { get; }
        public string[] Patterns { get; }
        public bool IsDefault { get; }

        public FolderMetaForPatternAttribute(string patterns, string? name = null, bool isDefault = false)
        {
            Name = name;
            Patterns = patterns.Split('|');
            IsDefault = isDefault;
        }
    }

    /// <summary>
    /// Attribute to register generic folder meta that applies to all directories
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GenericFolderMetaAttribute : Attribute
    {
        public string? Name { get; }
        
        public GenericFolderMetaAttribute(string? name = null)
        {
            Name = name;
        }
    }

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
