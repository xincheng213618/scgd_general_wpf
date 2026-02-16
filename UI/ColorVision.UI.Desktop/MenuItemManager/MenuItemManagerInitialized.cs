using ColorVision.UI.Menus;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    /// <summary>
    /// Apply MenuItemManager settings at startup so saved visibility, order, and OwnerGuid overrides take effect on first launch.
    /// </summary>
    public class MenuItemManagerInitialized : MainWindowInitializedBase
    {
        public override int Order { get; set; } = 0;

        public override Task Initialize()
        {
            var service = MenuItemManagerService.GetInstance();
            service.ApplySettings();

            // Rebuild menu to apply the saved settings
            MenuManager.GetInstance().LoadMenuItemFromAssembly();

            // Apply hotkeys to main window
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                service.ApplyHotkeys(mainWindow);
            }

            return Task.CompletedTask;
        }
    }
}
