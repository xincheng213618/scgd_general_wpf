using System.Windows.Controls;

namespace ColorVision.UI.Menus
{
    public interface IMenuItemMeta : IMenuItem
    {
        public MenuItem MenuItem { get; }
    }

}
