using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{
    public class MenuThirdPartyAlgorithms: MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override string Header => ColorVision.Engine.Properties.Resources.EditThirdPartyAlgorithmTemplate;
        public override int Order => 4;
    }


    public class MenuItemProviderSensor : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            List<MenuItemMetadata> items = new List<MenuItemMetadata>();
            var dlls = SysResourceTpaDLLDao.Instance.GetAll();
            foreach (var dll in dlls)
            {
                MenuItemMetadata menuitemdll = new MenuItemMetadata();
                menuitemdll.Header = dll.Name;
                menuitemdll.GuidId = dll.Code;
                menuitemdll.OwnerGuid = nameof(MenuThirdPartyAlgorithms);
                items.Add(menuitemdll);

                var mods = ThirdPartyAlgorithmsDao.Instance.GetAllByPid(dll.Id);
                foreach (var mod in mods)
                {
                    if (mod.Code == null) continue;

                    MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
                    menuItemMetadata.Header = mod.Name;
                    menuItemMetadata.GuidId = mod.Code;
                    menuItemMetadata.OwnerGuid = dll.Code;
                    menuItemMetadata.Command = new RelayCommand(a =>
                    {
                        new TemplateEditorWindow(new TemplateThirdParty(mod.Code)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                    });
                    new TemplateThirdParty(mod.Code).Load();
                    items.Add(menuItemMetadata);
                }
            }
            return items;
        }
    }




}
