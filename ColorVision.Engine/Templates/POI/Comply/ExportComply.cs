using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Templates.SysDictionary;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.POI.Comply
{
    public class ExportComply : MenuItemBase
    {
        public override string OwnerGuid => "Template";
        public override string GuidId => "Comply";
        public override int Order => 4;
        public override string Header => Properties.Resources.MenuValidue;
    }

    public class MenuItemProviderSensor : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            List<MenuItemMetadata> items = new List<MenuItemMetadata>();

            var mods = SysDictionaryModDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "mod_type", 110 } });

            foreach (var item in mods)
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
                menuItemMetadata.Order = 1;
                menuItemMetadata.Header = item.Name;
                menuItemMetadata.OwnerGuid = "Comply";
                menuItemMetadata.Command = new RelayCommand(a =>
                {
                    new TemplateEditorWindow(new TemplateComplyParam(item.Code)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                });
                new TemplateComplyParam(item.Code).Load();
                items.Add(menuItemMetadata);
            }
            var mod1s = SysDictionaryModDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "mod_type", 111 } });
            foreach (var item in mod1s)
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
                menuItemMetadata.Order = 2;
                menuItemMetadata.Header = item.Name;
                menuItemMetadata.OwnerGuid = "Comply";
                menuItemMetadata.Command = new RelayCommand(a =>
                {
                    new TemplateEditorWindow(new TemplateComplyParam(item.Code)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                });
                new TemplateComplyParam(item.Code).Load();
                items.Add(menuItemMetadata);
            }
            return items;
        }
    }

}
