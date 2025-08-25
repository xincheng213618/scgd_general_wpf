using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using ColorVision.Engine.Templates.SysDictionary;
using ColorVision.Database;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    public class MenuItemProviderSensor : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var mods = SysDictionaryModMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "mod_type", 5 } });

            List<MenuItemMetadata> items = new List<MenuItemMetadata>();
            foreach (var item in mods)
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
                menuItemMetadata.Order = 1;
                menuItemMetadata.Header = item.Name;
                menuItemMetadata.OwnerGuid = nameof(MenuTemplateSensor);
                menuItemMetadata.Command = new RelayCommand(a =>
                {
                    new TemplateEditorWindow( new TemplateSensor(item.Code)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                });
                items.Add(menuItemMetadata);
            }
            return items;
        }
    }

}
