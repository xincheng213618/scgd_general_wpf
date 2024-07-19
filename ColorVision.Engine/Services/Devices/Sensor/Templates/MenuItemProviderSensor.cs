using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using ColorVision.Themes;
using System.Windows.Controls;
using ColorVision.Engine.Services.SysDictionary;
using SkiaSharp;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    public class MenuItemProviderSensor : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            var mods = SysDictionaryModDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "mod_type", 5 } });

            List<MenuItemMetadata> items = new List<MenuItemMetadata>();
            foreach (var item in mods)
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
                menuItemMetadata.Order = 1;
                menuItemMetadata.Header = item.Name;
                menuItemMetadata.OwnerGuid = "TemplateSensor";
                menuItemMetadata.Command = new RelayCommand(a =>
                {
                    new WindowTemplate( new TemplateSensor(item.Code)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                });
                items.Add(menuItemMetadata);
            }
            return items;
        }
    }

}
