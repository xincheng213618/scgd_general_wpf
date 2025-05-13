using System.Collections.Generic;

namespace ColorVision.UI.Menus
{
    public interface IMenuItemProvider
    {
        IEnumerable<MenuItemMetadata> GetMenuItems();
    }

    public interface IRightMenuItemProvider
    {
        IEnumerable<MenuItemMetadata> GetMenuItems();
    }

}
