﻿using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using ColorVision.Engine.Services.SysDictionary;

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
