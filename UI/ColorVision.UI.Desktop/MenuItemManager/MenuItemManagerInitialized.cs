using ColorVision.UI.Menus;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    /// <summary>
    /// Apply MenuItemManager settings at startup so saved visibility, order, and OwnerGuid overrides take effect on first launch.
    /// </summary>
    public class MenuItemManagerInitialized : MainWindowInitializedBase
    {
        public override int Order { get; set; }

        public override Task Initialize()
        {
            if (MenuItemManagerService.ApplySettings())
            {
                MenuManager.GetInstance().RebuildAllMenus();
            }

            return Task.CompletedTask;
        }
    }
}
