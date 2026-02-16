using System.Collections.ObjectModel;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    public class MenuItemManagerConfig : IConfig
    {
        public static MenuItemManagerConfig Instance => ConfigService.Instance.GetRequiredService<MenuItemManagerConfig>();

        public ObservableCollection<MenuItemSetting> Settings { get; set; } = new();
    }
}
