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

    public class MenuTheme:GlobalMenuBase
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

            foreach (Theme item in ThemeManager.SupportedThemes)
            {
                RelayCommand relayCommand = new RelayCommand(a =>
                {
                    ThemeConfig.Instance.Theme = item;
                    Application.Current.ApplyTheme(item);

                    MenuManager.GetInstance().RefreshMenuItemsByGuid("MenuTheme");
                });

                MenuItemMetadata menuItemMeta = new MenuItemMetadata
                {
                    TargetName = MenuItemConstants.GlobalTarget,
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
            Theme currentTheme = ThemeManager.Current.CurrentTheme ?? Theme.UseSystem;
            int currentThemeIndex = ThemeManager.SupportedThemes.IndexOf(currentTheme);
            Theme newTheme = ThemeManager.SupportedThemes[(currentThemeIndex + 1) % ThemeManager.SupportedThemes.Count];
            ThemeConfig.Instance.Theme = newTheme;

            // 应用新主题
            Application.Current.ApplyTheme(newTheme);
        }
    }
}
