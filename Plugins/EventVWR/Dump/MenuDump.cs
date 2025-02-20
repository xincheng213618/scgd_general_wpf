using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Dump
{
    public class MenuDump : IMenuItemMeta
    {
        public override string OwnerGuid => MenuItemConstants.Help;

        public override string GuidId => "MenuDumpConfig";

        public override int Order => 10000;

        public override string Header => "转储文件设置";

        DumpConfig DumpConfig = new DumpConfig();
        public override MenuItem MenuItem
        {
            get
            {
                MenuItem MenuDump = new() { Header = Header };
                MenuItem Open = new MenuItem() { Header = "打开Dump文件夹", Command = Command };
                MenuDump.Items.Add(Open);

                foreach (var item in Enum.GetValues(typeof(DumpType)).Cast<DumpType>())
                {
                    MenuItem ThemeItem = new();
                    ThemeItem.Header = item;
                    ThemeItem.Click += (s, e) =>
                    {
                        DumpConfig.DumpType = item;
                        DumpConfig.SetDump();
                    };
                    ThemeItem.Tag = item;
                    ThemeItem.IsChecked = DumpConfig.DumpType == item;
                    MenuDump.Items.Add(ThemeItem);
                }


                RelayCommand relayCommand = new RelayCommand(A => DumpConfig.ClearDump());
                MenuItem Clear = new MenuItem() { Header = "设置为默认", Command = relayCommand };
                MenuDump.Items.Add(Clear);



                MenuItem Save = new MenuItem() { Header = "保存为", Command = new RelayCommand(A => DumpConfig.SaveDump()) };
                MenuDump.Items.Add(Save);

                MenuDump.Loaded += (s, e) =>
                {
                    foreach (var item in MenuDump.Items.OfType<MenuItem>())
                    {
                        if (item.Tag is DumpType DumpType)
                            item.IsChecked = DumpConfig.DumpType == DumpType;
                    }
                };
                return MenuDump;
            }
        }

    }
}
