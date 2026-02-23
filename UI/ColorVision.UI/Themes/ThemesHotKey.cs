using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Desktop.Themes
{

    public class MenuTheme:MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override int Order => 1000;
        public override string Header => ColorVision.Themes.Properties.Resources.MenuTheme;
        public override string InputGestureText => "Ctrl + Shift + T";
    }


    public class MenuThemeProvider : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {

            List<MenuItemMetadata> menuItemMetas = new List<MenuItemMetadata>();

            foreach (var item in Enum.GetValues(typeof(Theme)).Cast<Theme>())
            {
                RelayCommand relayCommand = new RelayCommand(a =>
                {
                    ThemeConfig.Instance.Theme = item;
                    Application.Current.ApplyTheme(item);

                    MenuManager.GetInstance().RefreshMenuItemsByGuid("MenuTheme");
                });

                MenuItemMetadata menuItemMeta = new MenuItemMetadata
                {
                    OwnerGuid =nameof(MenuTheme),
                    GuidId = item.ToString(),
                    Header = ColorVision.Themes.Properties.Resources.ResourceManager.GetString(item.ToDescription(), CultureInfo.CurrentUICulture) ?? "",
                    Icon = null, // Set your icon here if needed
                    Order = 1000 + (int)item, // Adjust order based on the enum value
                    Command = relayCommand,
                    IsChecked = ThemeManager.Current.CurrentTheme == item
                };
                menuItemMetas.Add(menuItemMeta);
            }
           return menuItemMetas;
        }
    }


    public class ThemesHotKey :  IHotKey
    {
        public HotKeys HotKeys => new(ColorVision.Themes.Properties.Resources.Theme, new Hotkey(Key.T, ModifierKeys.Control | ModifierKeys.Shift), Execute);

        private void Execute()
        {
            // 获取当前主题的索引
            int currentThemeIndex = (int)(ThemeManager.Current.CurrentTheme ?? Theme.UseSystem);

            // 获取主题总数，缓存以避免重复计算
            int themeCount = Enum.GetValues(typeof(Theme)).Length;

            // 计算下一个主题的索引
            int nextThemeIndex = (currentThemeIndex + 1) % themeCount;

            // 更新当前主题
            Theme newTheme = (Theme)nextThemeIndex;
            ThemeConfig.Instance.Theme = newTheme;

            // 应用新主题
            Application.Current.ApplyTheme(newTheme);
        }
    }
}
