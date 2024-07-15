using ColorVision.UI.Menus;
using System.Collections.Generic;

namespace ColorVision.Solution
{
    public interface IContextMenuProvider
    {
        IEnumerable<MenuItemMetadata> GetMenuItems();
    }
}