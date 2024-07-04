namespace ColorVision.UI.Menus
{
    public interface IMenuItemProvider
    {
        IEnumerable<MenuItemMetadata> GetMenuItems();
    }

}
