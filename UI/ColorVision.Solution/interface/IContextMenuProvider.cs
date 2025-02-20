using ColorVision.UI.Menus;

namespace ColorVision.Solution
{
    public interface IContextMenuProvider
    {
        IEnumerable<MenuItemMetadata> GetMenuItems();
    }
}