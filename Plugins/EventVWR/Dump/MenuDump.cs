using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;


namespace  EventVWR.Dump
{

    public class MenuDump : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 10000;
        public override string Header => Properties.Resources.DumpFileSettings;
    }


    public class MenuThemeProvider : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {

            List<MenuItemMetadata> menuItemMetas = new List<MenuItemMetadata>();
            DumpConfig DumpConfig = new DumpConfig();

            foreach (var item in Enum.GetValues(typeof(DumpType)).Cast<DumpType>())
            {
                RelayCommand relayCommand = new RelayCommand(a =>
                {
                    DumpConfig.DumpType = item;
                    DumpConfig.SetDump();
                    MenuService.Instance.RefreshMenuItemsByGuid("MenuDump");
                });

                MenuItemMetadata menuItemMeta = new MenuItemMetadata
                {
                    OwnerGuid = nameof(MenuDump),
                    GuidId = item.ToString(),
                    Header = item.ToString(),
                    Icon = null, // Set your icon here if needed
                    Order = 1000 + (int)item, // Adjust order based on the enum value
                    Command = relayCommand,
                    IsChecked = DumpConfig.DumpType == item
                };
                menuItemMetas.Add(menuItemMeta);
            }


            RelayCommand clearCommand = new RelayCommand(A => DumpConfig.ClearDump());
            RelayCommand saveCommand = new RelayCommand(A => DumpConfig.SaveDump());


            MenuItemMetadata menuItemMetaclear = new MenuItemMetadata
            {
                OwnerGuid = nameof(MenuDump),
                Header = "清空Dmp",
                Icon = null, // Set your icon here if needed
                Order = 10000, // Adjust order based on the enum value
                Command = clearCommand,
            };
            menuItemMetas.Add(menuItemMetaclear);

            MenuItemMetadata menuItemMetasave = new MenuItemMetadata
            {
                OwnerGuid = nameof(MenuDump),
                Header = "保存Dmp",
                Icon = null, // Set your icon here if needed
                Order = 10000, // Adjust order based on the enum value
                Command = clearCommand,
            };
            menuItemMetas.Add(menuItemMetasave);

            return menuItemMetas;
        }
    }
}
