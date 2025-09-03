namespace ColorVision.UI.Languages
{      
    //这里隐藏掉，因为我认为频繁切换语言是不需要的


    //public class MenuLanguage : MenuItemBase
    //{
    //    public override string OwnerGuid => MenuItemConstants.Tool;
    //    public override int Order => 1000;
    //    public override string Header => Properties.Resources.MenuLanguage;
    //    public override string InputGestureText => "Ctrl + Shift + T";
    //}


    //public class MenuThemeProvider : IMenuItemProvider
    //{
    //    public IEnumerable<MenuItemMetadata> GetMenuItems()
    //    {
    //        List<MenuItemMetadata> menuItemMetas = new List<MenuItemMetadata>();
    //        int i = 1;
    //        foreach (var item in LanguageManager.Current.Languages)
    //        {
    //            i++;
    //            RelayCommand relayCommand = new RelayCommand(a =>
    //            {
    //                string temp = Thread.CurrentThread.CurrentUICulture.Name;
    //                LanguageConfig.Instance.UICulture = item;
    //                bool sucess = LanguageManager.Current.LanguageChange(item);
    //                if (!sucess)
    //                {
    //                    LanguageConfig.Instance.UICulture = temp;
    //                }

    //                MenuManager.GetInstance().RefreshMenuItemsByGuid(nameof(MenuLanguage));
    //            });


    //            MenuItemMetadata menuItemMeta = new MenuItemMetadata
    //            {
    //                OwnerGuid = nameof(MenuLanguage),
    //                GuidId = item,
    //                Header = LanguageManager.keyValuePairs.TryGetValue(item, out string value) ? value : item,
    //                Icon = null, // Set your icon here if needed
    //                Order = i,
    //                Command = relayCommand,
    //                IsChecked = Thread.CurrentThread.CurrentUICulture.Name == item
    //            };
    //            menuItemMetas.Add(menuItemMeta);
    //        }

    //        return menuItemMetas;
    //    }
    //}
}
