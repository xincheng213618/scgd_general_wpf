using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Templates
{
    public class ExportMenuItemThirdPartyAlgorithms: MenuItemBase
    {
        public override string OwnerGuid => "Template";
        public override string GuidId => "ThirdPartyAlgorithms";
        public override string Header => "ThirdPartyAlgorithms";
        public override int Order => 3;
    }


    public class MenuItemProviderSensor : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            List<MenuItemMetadata> items = new List<MenuItemMetadata>();

            var mods = ThirdPartyAlgorithmsDao.Instance.GetAll();

            foreach (var item in mods)
            {
                if (item.Code == null) continue;

                MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
                menuItemMetadata.Order = 1;
                menuItemMetadata.Header = item.Name;
                menuItemMetadata.OwnerGuid = "ThirdPartyAlgorithms";
                menuItemMetadata.Command = new RelayCommand(a =>
                {
                    new WindowTemplate(new TemplateThirdParty(item.Code)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                });
                new TemplateThirdParty(item.Code).Load();
                items.Add(menuItemMetadata);
            }

            return items;
        }
    }


}
