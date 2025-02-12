using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.SysDictionary;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.Templates.Validate
{
    public class ExportComply : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override int Order => 4;
        public override string Header => Properties.Resources.MenuValidue;
    }
    public class ExportComplyPoint : MenuItemBase
    {
        public override string OwnerGuid => nameof(ExportComply);
        public override int Order => 2;
        public override string Header => "点";
    }
    public class ExportComplyPointList : MenuItemBase
    {
        public override string OwnerGuid => nameof(ExportComply);
        public override int Order => 2;
        public override string Header => "点集";
    }

    public class MenuItemProviderSensor : IMenuItemProvider
    {
        public IEnumerable<MenuItemMetadata> GetMenuItems()
        {
            List<MenuItemMetadata> items = new List<MenuItemMetadata>();

            var mods = SysDictionaryModMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "mod_type", 110 }, { "is_delete", false } });

            foreach (var item in mods)
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
                menuItemMetadata.Order = 1;
                menuItemMetadata.Header = item.Name;
                menuItemMetadata.OwnerGuid = nameof(ExportComplyPoint);
                menuItemMetadata.Command = new RelayCommand(a =>
                {
                    new TemplateEditorWindow(new TemplateComplyParam(item.Code)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                });
                new TemplateComplyParam(item.Code).Load();
                items.Add(menuItemMetadata);
            }
            var mod1s = SysDictionaryModMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "mod_type", 111 }, { "is_delete", false } });
            foreach (var item in mod1s)
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
                menuItemMetadata.Order = 20;
                menuItemMetadata.Header = item.Name;
                menuItemMetadata.OwnerGuid = nameof(ExportComplyPointList);
                menuItemMetadata.Command = new RelayCommand(a =>
                {
                    new TemplateEditorWindow(new TemplateComplyParam(item.Code)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                });
                new TemplateComplyParam(item.Code).Load();
                items.Add(menuItemMetadata);
            }
            var jjnds = SysDictionaryModMasterDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "mod_type", 120 }, { "is_delete", false } });
            foreach (var item in jjnds)
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata();
                menuItemMetadata.Order = 50;
                menuItemMetadata.Header = item.Name;
                menuItemMetadata.OwnerGuid = nameof(ExportComplyPoint);
                menuItemMetadata.Command = new RelayCommand(a =>
                {
                    new TemplateEditorWindow(new TemplateComplyParam(item.Code, 1)) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                });
                new TemplateComplyParam(item.Code).Load();
                items.Add(menuItemMetadata);
            }
            return items;
        }
    }

}
